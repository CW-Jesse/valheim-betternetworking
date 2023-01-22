using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

using HarmonyLib;
using Steamworks;
using BepInEx.Configuration;
using PlayFab.Party;
using BepInEx;
using System.Reflection;
using System.Linq;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public partial class BN_Patch_Compression {
        private const int COMPRESSION_VERSION = 4;

        private const int k_nSteamNetworkingSend_Reliable = 8;                       // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int k_cbMaxSteamNetworkingSocketsMessageSizeSend = 512 * 1024; // https://partner.steamgames.com/doc/api/steamnetworkingtypes

        public static Assembly zstdNet;
        private static object compressor, decompressor;
        private static MethodInfo wrap, unwrap;

        public enum Options_NetworkCompression {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        private static string ZSTD_RESOURCE_NAME64 = "CW_Jesse.BetterNetworking.x64.cw_jesse.betternetworking.libzstd.dll";
        private static string ZSTD_RESOURCE_NAME32 = "CW_Jesse.BetterNetworking.x86.cw_jesse.betternetworking.libzstd.dll";
        private static string ZSTD_PLUGIN_PATH = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location).FullName;
        private static string ZSTD_FILE_NAME = "cw_jesse.betternetworking.libzstd.dll";
        private static string ZSTD_FILE_FULL_PATH = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location).FullName + Path.DirectorySeparatorChar + ZSTD_FILE_NAME;
        //private static string ZSTD_PLUGIN_PATH = BepInEx.Paths.PluginPath + Path.DirectorySeparatorChar;
        private static string ZSTD_DICT_RESOURCE_NAME = "CW_Jesse.BetterNetworking.dict.small";

        public static void InitCompressor() {
            //BN_Logger.LogError($"Resources: {Assembly.GetExecutingAssembly().GetManifestResourceNames().Join()}");
            Directory.SetCurrentDirectory(ZSTD_PLUGIN_PATH); // allows ZstdNet to search the directory where the dll is

            string zstdResourceName = Environment.Is64BitProcess ? ZSTD_RESOURCE_NAME64 : ZSTD_RESOURCE_NAME32;
            using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(zstdResourceName)) {
                using (FileStream f = new FileStream(ZSTD_FILE_FULL_PATH, FileMode.Create)) {
                    s.CopyTo(f);
                }
            }

            byte[] compressionDict;
            using (Stream dictStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(ZSTD_DICT_RESOURCE_NAME)) {
                compressionDict = new byte[dictStream.Length];
                dictStream.Read(compressionDict, 0, (int)dictStream.Length);
            }

            // this doesn't work:
            // Type compressorOptionsType = zstdNet.GetType("CompressionOptions")
            Type compressorOptionsType = zstdNet.GetTypes().First(t => t.Name == "CompressionOptions");
            Type decompressorOptionsType = zstdNet.GetTypes().First(t => t.Name == "DecompressionOptions");
            Type compressorType = zstdNet.GetTypes().First(t => t.Name == "Compressor");
            Type decompressorType = zstdNet.GetTypes().First(t => t.Name == "Decompressor");

            object compressorOptions = Activator.CreateInstance(compressorOptionsType, new object[] { compressionDict, 1 });
            object decompressorOptions = Activator.CreateInstance(decompressorOptionsType, new object[] { compressionDict });

            compressor = Activator.CreateInstance(compressorType, new object[] { compressorOptions });
            decompressor = Activator.CreateInstance(decompressorType, new object[] { decompressorOptions });
            //compressor = new Compressor(new CompressionOptions(compressionDict, 1));
            //decompressor = new Decompressor(new DecompressionOptions(compressionDict));

            wrap = compressorType.GetMethod("Wrap", new Type[] { typeof(byte[]) });
            unwrap = decompressorType.GetMethod("Unwrap", new Type[] { typeof(byte[]), typeof(int) });

            AppDomain.CurrentDomain.ProcessExit += UninitCompressor;
        }
        private static void UninitCompressor(object sender, EventArgs e) {
            //BN_Logger.LogError($"{Paths.BepInExAssemblyDirectory}, {Paths.BepInExAssemblyPath}, {Paths.BepInExConfigPath}, {Paths.BepInExRootPath}, {Paths.DllSearchPaths.Join()}, {Paths.GameRootPath}, {Paths.ExecutablePath}");
            //wrap = null;
            //unwrap = null;
            //compressor = null;
            //decompressor = null;
            //zstdNet = null;
            //GC.Collect();
            //GC.WaitForPendingFinalizers();
            Directory.SetCurrentDirectory(ZSTD_PLUGIN_PATH);
            try {
                File.Delete(ZSTD_FILE_NAME);
            } catch (Exception ex) {
                //BN_Logger.LogInfo($"Left behind file: {ZSTD_FILE_NAME} ({ex.GetType().Name})");
            }
        }

        private static byte[] Compress(byte[] buffer) {
            return (byte[])wrap.Invoke(compressor, new object[] { buffer } );
        }
        private static byte[] Decompress(byte[] compressedBuffer) {
            return (byte[])unwrap.Invoke(decompressor, new object[] { compressedBuffer, int.MaxValue });
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configCompressionEnabled = config.Bind(
                "Networking",
                "Compression Enabled",
                Options_NetworkCompression.@true,
                new ConfigDescription("Most people will want to keep this enabled.\n" +
                "---\n" +
                "PlayFab/Steamworks: Increases speed and strength of network compression.\nSteamworks: Adds network compression."));

            BetterNetworking.configCompressionEnabled.SettingChanged += ConfigCompressionEnabled_SettingChanged;
        }
        private static void ConfigCompressionEnabled_SettingChanged(object sender, EventArgs e) {
            SetCompressionEnabledFromConfig();
        }

        private static void SetCompressionEnabledFromConfig() {
            bool newCompressionStatus;

            if (BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true) {
                newCompressionStatus = true;
                BN_Logger.LogMessage($"Compression: Enabling");
            } else {
                newCompressionStatus = false;
                BN_Logger.LogMessage($"Compression: Disabling");
            }

            SendCompressionEnabledStatus(newCompressionStatus);

            CompressionStatus.ourStatus.compressionEnabled = newCompressionStatus;
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ref ZNetPeer peer) {
            CompressionStatus.AddPeer(peer);

            RegisterRPCs(peer);
            SendCompressionVersion(peer, CompressionStatus.ourStatus.version);
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPostfix]
        private static void OnDisconnect(ZNetPeer peer) {
            CompressionStatus.RemovePeer(peer);
        }

        //private static int capCount = 0;
        //[HarmonyPatch(typeof(ZPlayFabSocket), "InternalSend")]
        //[HarmonyPostfix]
        //private static void PlayFab_SendCompressedPackage(byte[] payload) {
        //    string CaptureFolderName = "cap";
        //    BN_Logger.LogWarning(payload.Length);
        //    //return true;
        //    Directory.CreateDirectory(CaptureFolderName);
        //    File.WriteAllBytes(CaptureFolderName + Path.DirectorySeparatorChar + capCount, payload);
        //    capCount++;
        //}

        [HarmonyPatch(typeof(ZPlayFabSocket), "InternalSend")]
        [HarmonyPrefix]
        private static bool PlayFab_Send(ref ZPlayFabSocket __instance, ref bool ___m_useCompression, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue, byte[] payload) {
            if (!CompressionStatus.GetSendCompressionStarted(BN_Utils.GetPeer(__instance))) { return true; }

            if ((bool)AccessTools.Method(typeof(ZPlayFabSocket), "PartyResetInProgress").Invoke(__instance, null))
                return false;

            AccessTools.Method(typeof(ZPlayFabSocket), "IncSentBytes").Invoke(__instance, new object[] { payload.Length });
            if ((UnityEngine.Object)ZNet.instance != (UnityEngine.Object)null && ZNet.instance.HaveStopped)
                AccessTools.Method(typeof(ZPlayFabSocket), "InternalSendCont").Invoke(__instance, new object[] { Compress(payload) });
            else
                ((Queue<byte[]>)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "m_outCompress").GetValue(___m_zlibWorkQueue)).Enqueue(Compress(payload));

            return false;
        }

        [HarmonyPatch(typeof(ZPlayFabSocket), "OnDataMessageReceived")]
        [HarmonyPrefix]
        private static bool PlayFab_Receive(
            ref ZPlayFabSocket __instance,
            ref bool ___m_useCompression, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue, ref string ___m_remotePlayerId, ref bool ___m_isClient, ref bool ___m_didRecover,
            object sender, PlayFabPlayer from, byte[] compressedBuffer) {
            if (!CompressionStatus.GetReceiveCompressionStarted(BN_Utils.GetPeer(__instance))) { return true; }

            if (!(from.EntityKey.Id == ___m_remotePlayerId))
                return false;

            AccessTools.Method(typeof(ZPlayFabSocket), "DelayedInit").Invoke(__instance, null);

            if (!___m_isClient && ___m_didRecover)
                AccessTools.Method(typeof(ZPlayFabSocket), "CheckReestablishConnection").Invoke(__instance, new object[] { compressedBuffer });
            else
                ((Queue<byte[]>)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "m_outDecompress").GetValue(___m_zlibWorkQueue)).Enqueue(Decompress(compressedBuffer));

            return false;
        }

        [HarmonyPatch(typeof(ZPlayFabSocket), "CheckReestablishConnection")]
        [HarmonyPrefix]
        private static bool PlayFab_CheckReestablishConnection(ref ZPlayFabSocket __instance, byte[] maybeCompressedBuffer) {
            try {
                AccessTools.Method(typeof(ZPlayFabSocket), "OnDataMessageReceivedCont").Invoke(__instance, new object[] { Decompress(maybeCompressedBuffer) });
            } catch {
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static bool Steamworks_SendCompressedPackages(ref ZSteamSocket __instance, ref Queue<Byte[]> ___m_sendQueue, ref int ___m_totalSent, ref HSteamNetConnection ___m_con) {
            if (!__instance.IsConnected()) {
                return false;
            }

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetSendCompressionStarted(peer)) {
#if DEBUG
                BN_Logger.LogInfo($"Compressed Send: Sending uncompressed message to {BN_Utils.GetPeerName(peer)}");
#endif
                return true;
            }
#if DEBUG
            BN_Logger.LogInfo($"Compressed Send: Sending compressed message to {BN_Utils.GetPeerName(peer)}");
#endif

            lock (___m_sendQueue) {

                while (___m_sendQueue.Count > 0) {

                    int packagesToSendLength = 0;

                    // determine how many packages to send in a single message

                    List<byte[]> packagesToSendList = new List<byte[]>();
                    foreach (byte[] package in ___m_sendQueue) {
                        if (packagesToSendList.Count > 0 && // send at least one package
                            packagesToSendLength + package.Length > k_cbMaxSteamNetworkingSocketsMessageSizeSend) { // packages must not exceed steam message send size limit uncompressed (assumes successful compression)
                            BN_Logger.LogMessage($"Compressed Send ({BN_Utils.GetPeerName(peer)}): Reached send limit: {packagesToSendList.Count} packages size: {packagesToSendLength}/{k_cbMaxSteamNetworkingSocketsMessageSizeSend}; sending {___m_sendQueue.Count - packagesToSendList.Count} queued packages in another message");
                            break;
                        }

                        packagesToSendLength += package.Length;
                        packagesToSendList.Add(package);
                    }

                    // compress message

                    byte[][] packagesToSendArray = packagesToSendList.ToArray();
                    byte[] compressedMessage;

                    using (MemoryStream compressedPackagesStream = new MemoryStream()) {
                        using (BinaryWriter compressedPackagesWriter = new BinaryWriter(compressedPackagesStream)) {

                            compressedPackagesWriter.Write(packagesToSendArray.Length); // number of packages

                            for (int i = 0; i < packagesToSendArray.Length; i++) {
                                compressedPackagesWriter.Write(packagesToSendArray[i].Length); // length of package
                                compressedPackagesWriter.Write(packagesToSendArray[i]); // package
                            }

                            compressedPackagesWriter.Flush();
                            compressedMessage = Compress(compressedPackagesStream.ToArray());
                        }
                    }

#if DEBUG
                    BN_Logger.LogInfo($"Compressed Send {BN_Utils.GetPeerName(peer)}: Message reduced from {packagesToSendLength} B to {compressedMessage.Length} B");
#endif

                    // send message

                    IntPtr intPtr = Marshal.AllocHGlobal(compressedMessage.Length);
                    Marshal.Copy(compressedMessage, 0, intPtr, compressedMessage.Length);

                    EResult eresult;
                    long messagesSentCount;
                    if (BN_Utils.IsDedicated()) {
                        eresult = SteamGameServerNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedMessage.Length, k_nSteamNetworkingSend_Reliable, out messagesSentCount);
                    } else {
                        eresult = SteamNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedMessage.Length, k_nSteamNetworkingSend_Reliable, out messagesSentCount);
                    }

                    Marshal.FreeHGlobal(intPtr);

                    // ensure message was sent

                    if (eresult != EResult.k_EResultOK) {
                        BN_Logger.LogWarning($"Compressed Send ({BN_Utils.GetPeerName(peer)}): {eresult};");
                        return true;
                    }

                    // remove sent messages from queue

                    ___m_totalSent += compressedMessage.Length;
                    for (int i = 0; i < packagesToSendArray.Length; i++) {
                        ___m_sendQueue.Dequeue();
                    }

                    // log result

                    if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info) {
                        if (packagesToSendLength > 256) { // small messages don't compress well but they also don't matter
                            float compressedSizePercentage = ((float)compressedMessage.Length / (float)packagesToSendLength) * 100;
                            BN_Logger.LogInfo($"Compressed Send ({BN_Utils.GetPeerName(peer)}): {packagesToSendLength} B compressed to {compressedSizePercentage.ToString("0")}%");
                        }
                    }
                }
            }

            return false;
         }

        private readonly static Queue<ZPackage> packages = new Queue<ZPackage>();

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPrefix]
        private static bool ReceiveCompressedPackages(ref ZPackage __result, ref ZSteamSocket __instance, ref HSteamNetConnection ___m_con, ref int ___m_totalRecv, ref bool ___m_gotData) {
            if (packages.Count > 0) {
                BN_Logger.LogInfo("Compressed Receive: Dequeueing previously received package");
                ZPackage package = packages.Dequeue();
                ___m_totalRecv += package.Size();
                ___m_gotData = true;

                __result = package;
                return false;
            }
            
            if (!__instance.IsConnected()) {
                __result = null;
                return false;
            }

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetReceiveCompressionStarted(peer)) {
#if DEBUG
                BN_Logger.LogInfo($"Compressed Receive: Receiving uncompressed message from {BN_Utils.GetPeerName(peer)}");
#endif
                return true;
            }
#if DEBUG
            BN_Logger.LogInfo($"Compressed Receive: Receiving compressed message from {BN_Utils.GetPeerName(peer)}");
#endif

            IntPtr[] array = new IntPtr[1];
            bool receivedMessages = false;
            if (BN_Utils.IsDedicated()) {
                receivedMessages = SteamGameServerNetworkingSockets.ReceiveMessagesOnConnection(___m_con, array, 1) == 1;
            } else {
                receivedMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(___m_con, array, 1) == 1;
            }

            if (receivedMessages) {
                SteamNetworkingMessage_t steamNetworkingMessage_t = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[0]);


                byte[] compressedPackages = new byte[steamNetworkingMessage_t.m_cbSize];
                Marshal.Copy(steamNetworkingMessage_t.m_pData, compressedPackages, 0, steamNetworkingMessage_t.m_cbSize);
                steamNetworkingMessage_t.m_pfnRelease = array[0];
                steamNetworkingMessage_t.Release();

                byte[] uncompressedPackages;
                try {
                    uncompressedPackages = Decompress(compressedPackages);
                } catch {
                    BN_Logger.LogInfo($"Compressed Receive ({BN_Utils.GetPeerName(peer)}): Couldn't decompress message; assuming uncompressed");

                    ZPackage zpackage = new ZPackage(compressedPackages);
                    ___m_totalRecv += zpackage.Size();
                    ___m_gotData = true;
                    __result = zpackage;
                    return false;
                }

                if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info) {
                    if (uncompressedPackages.Length > 256) { // small messages don't compress well but they also don't matter
                        float compressedSizePercentage = ((float)steamNetworkingMessage_t.m_cbSize / (float)uncompressedPackages.Length) * 100;
                        BN_Logger.LogInfo($"Compressed Receive ({BN_Utils.GetPeerName(peer)}): {uncompressedPackages.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
                    }
                }

                using (MemoryStream uncompressedPackagesStream = new MemoryStream(uncompressedPackages)) {
                    using (BinaryReader uncompressedPackagesReader = new BinaryReader(uncompressedPackagesStream)) {
                        int packageCount = uncompressedPackagesReader.ReadInt32();
                        for (int i = 0; i < packageCount; i++) {
                            int packageLength = uncompressedPackagesReader.ReadInt32();
                            byte[] packageByteArray = uncompressedPackagesReader.ReadBytes(packageLength);
                            packages.Enqueue(new ZPackage(packageByteArray));
                        }
                    }
                }

                ZPackage package = packages.Dequeue();
                ___m_totalRecv += package.Size();
                ___m_gotData = true;

                __result = package;
                return false;
            }
            __result = null;
            return false;
        }


        private static class CompressionStatus {

            public const int COMPRESSION_VERSION_UNKNOWN = 0;

            public static PeerCompressionStatus ourStatus = new PeerCompressionStatus() { version = COMPRESSION_VERSION, compressionEnabled = BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true };
            private readonly static Dictionary<ZNetPeer, PeerCompressionStatus> peerStatuses = new Dictionary<ZNetPeer, PeerCompressionStatus>();
            public class PeerCompressionStatus {
                public int version = COMPRESSION_VERSION_UNKNOWN;
                public bool compressionEnabled = false;
                public bool receivingCompressed = false;
                public bool sendingCompressed = false;
            }
            public static bool AddPeer(ZNetPeer peer) {
                if (peer == null) {
                    BN_Logger.LogWarning("Compression: Tried to add null peer");
                    return false;
                }

                peerStatuses.Add(peer, new PeerCompressionStatus());
                return true;
            }
            public static void RemovePeer(ZNetPeer peer) {
                if (!IsPeerExist(peer)) {
                    BN_Logger.LogWarning($"Compression: Tried to remove non-existent peer: {BN_Utils.GetPeerName(peer)}");
                    return;
                }

                peerStatuses.Remove(peer);
            }
            public static bool IsPeerExist(ZNetPeer peer) {
                if (peer != null && peerStatuses.ContainsKey(peer)) { return true; }
                return false;
            }

            public static int GetVersion(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return 0; }
                return peerStatuses[peer].version;
            }
            public static void SetVersion(ZNetPeer peer, int theirVersion) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].version = theirVersion;
            }
            public static bool GetIsCompatibleWith(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return (ourStatus.version == GetVersion(peer));
            }

            public static bool GetCompressionEnabled(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return peerStatuses[peer].compressionEnabled;
            }
            public static void SetCompressionEnabled(ZNetPeer peer, bool enabled) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].compressionEnabled = enabled;
            }

            public static bool GetSendCompressionStarted(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return peerStatuses[peer].sendingCompressed;
            }
            public static void SetSendCompressionStarted(ZNetPeer peer, bool started) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].sendingCompressed = started;
            }

            public static bool GetReceiveCompressionStarted(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return peerStatuses[peer].receivingCompressed;
            }
            public static void SetReceiveCompressionStarted(ZNetPeer peer, bool started) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].receivingCompressed = started;
            }
        }
    }
}
