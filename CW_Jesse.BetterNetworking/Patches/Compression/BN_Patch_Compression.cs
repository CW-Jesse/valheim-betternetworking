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

using ZstdSharp;
using System.Runtime.CompilerServices;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public partial class BN_Patch_Compression {
        private const int COMPRESSION_VERSION = 6;

        private const int k_nSteamNetworkingSend_Reliable = 8;                       // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int k_cbMaxSteamNetworkingSocketsMessageSizeSend = 512 * 1024; // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int chunkSize = k_cbMaxSteamNetworkingSocketsMessageSizeSend - 1; // leaves 1 byte for chunk count

        private static string ZSTD_DICT_RESOURCE_NAME = "CW_Jesse.BetterNetworking.dict.small";
        private static int ZSTD_LEVEL = 1;
        private static Compressor compressor;
        private static Decompressor decompressor;

        public enum Options_NetworkCompression {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        public static void InitCompressor() {
            byte[] compressionDict;
            using (Stream dictStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ZSTD_DICT_RESOURCE_NAME)) {
                compressionDict = new byte[dictStream.Length];
                dictStream.Read(compressionDict, 0, (int)dictStream.Length);
            }

            System.Threading.Thread.Sleep(3000);
            compressor = new Compressor(ZSTD_LEVEL);
            compressor.LoadDictionary(compressionDict);
            decompressor = new Decompressor();
            decompressor.LoadDictionary(compressionDict);

        }

        private static byte[] Compress(byte[] buffer) {
            byte[] compressedBuffer = compressor.Wrap(buffer).ToArray();
            if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info && buffer.Length > 256) { // small messages don't compress well but they also don't matter
                float compressedSizePercentage = ((float)compressedBuffer.Length / (float)buffer.Length) * 100;
                BN_Logger.LogInfo($"Sent {buffer.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
            }
            return compressedBuffer;
        }
        private static byte[] Decompress(byte[] compressedBuffer) {
            byte[] buffer = decompressor.Unwrap(compressedBuffer).ToArray();
            if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info && buffer.Length > 256) { // small messages don't compress well but they also don't matter
                float compressedSizePercentage = ((float)compressedBuffer.Length / (float)buffer.Length) * 100;
                BN_Logger.LogInfo($"Received {buffer.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
            }
            return buffer;
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configCompressionEnabled = config.Bind(
                "Networking",
                "Compression Enabled",
                Options_NetworkCompression.@true,
                new ConfigDescription("Keep this enabled unless comparing difference.\n" +
                "---\n" +
                "Crossplay enabled: Increases speed and strength of network compression.\nCrossplay disabled: Adds network compression."));

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

            CompressionStatus.ourStatus.compressionEnabled = newCompressionStatus;
            SendCompressionEnabledStatus();
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
        private static bool Steamworks_SendCompressedPackages(ref ZSteamSocket __instance, ref Queue<Byte[]> ___m_sendQueue) {
            if (!__instance.IsConnected()) { return false; }
            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetSendCompressionStarted(peer)) { return true; }

            if (BN_Utils.IsDedicated()) {
                byte[] randomPackage = new byte[1024 * 1024];
                new Random(0).NextBytes(randomPackage);
                ___m_sendQueue.Enqueue(randomPackage);
            }

            if (___m_sendQueue.Count <= 0) { return false; }

            int originalQueueLength = ___m_sendQueue.Count;

            ___m_sendQueue = new Queue<byte[]>(___m_sendQueue.SelectMany(p => {
                p = Compress(p);
                int chunkCount = GetChunkCount(p);
                if (chunkCount > 1) {
                    BN_Logger.LogMessage($"message {p.Length} is being chunked into {chunkCount} chunks");
                }
                Queue<byte[]> chunks = new Queue<byte[]>();

                chunks.Enqueue(new byte[1] { (byte)chunkCount }.AddRangeToArray(GetChunk(p, 0)));

                for (int i = 1; i < chunkCount; i++) {
                    chunks.Enqueue(GetChunk(p, i));
                }

                return chunks;
            }
            ));
            if (___m_sendQueue.Count > originalQueueLength) {
                BN_Logger.LogMessage($"queue length chunked from {originalQueueLength} to {___m_sendQueue.Count}");
            }


            return true;
         }
        private static int GetChunkCount(byte[] data) {
            int chunkCount = (data.Length / chunkSize) + 1;
            //BN_Logger.LogMessage($"chunk count: {chunkCount}");
            return chunkCount;
        }
        private static byte[] GetChunk(byte[] data, int chunkNumber) {
            int chunkStart = chunkNumber * chunkSize;

            byte[] chunk = data.Skip(chunkStart).Take(chunkSize).ToArray();

            if (chunkNumber > 0) {
                BN_Logger.LogMessage($"chunk {chunkNumber}: {chunk.Length}/{data.Length}");
            }
            return chunk;
        }

        private static int packageChunksRemaining = 0;
        private static byte[] chunkedPackage = new byte[0];
        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPostfix]
        private static void Steamworks_ReceiveCompressedPackages(ref ZPackage __result, ref ZSteamSocket __instance) {
            if (!__instance.IsConnected()) { return; }
            if (__result == null) { return; }
            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetReceiveCompressionStarted(peer)) { return; }

            byte[] p = __result.GetArray(); // get the received package
            BN_Logger.LogMessage($"received message of size {p.Length} B");
            // if it's part 2 or more, add it to the range and return nothing
            if (packageChunksRemaining > 1) {
                BN_Logger.LogMessage($"received chunk {packageChunksRemaining} of");


                packageChunksRemaining--;
                chunkedPackage = chunkedPackage.AddRangeToArray(p);

                __result = null;
                return;
            }

            if (packageChunksRemaining <= 0) {
                packageChunksRemaining = p[0];
                p = p.Skip(1).ToArray();
                BN_Logger.LogMessage($"received message of {packageChunksRemaining} chunks, {p.Length} B");

                if (packageChunksRemaining > 1) {
                    packageChunksRemaining--;
                    chunkedPackage = chunkedPackage.AddRangeToArray(p);
                    __result = null;
                    return;
                }
            }

            packageChunksRemaining--;
            chunkedPackage = chunkedPackage.AddRangeToArray(p);

            
            try {
                __result = new ZPackage(Decompress(chunkedPackage));
                BN_Logger.LogMessage($"decompressed package of size {chunkedPackage.Length} from {p.Length}");
            } catch (Exception e) {
                BN_Logger.LogError($"failed to decompress package of size {chunkedPackage.Length} from {p.Length}");
            }
            chunkedPackage = new byte[0];
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
