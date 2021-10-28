using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

using HarmonyLib;
using Steamworks;
using K4os.Compression.LZ4;
using BepInEx.Configuration;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public partial class BN_Patch_Compression {
        private const int COMPRESSION_VERSION = 2;

        private const int k_nSteamNetworkingSend_Reliable = 8;                       // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int k_cbMaxSteamNetworkingSocketsMessageSizeSend = 512 * 1024; // https://partner.steamgames.com/doc/api/steamnetworkingtypes

        public enum Options_NetworkCompression {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configCompressionEnabled = config.Bind(
                "Networking",
                "Compression Enabled",
                Options_NetworkCompression.@true,
                new ConfigDescription("Most people will want to keep this enabled.\n" +
                "---\n" +
                "If your internet is great and your computer isn't, then try lowering your update rate, lowering your queue size, and/or disabling compression."));

            BetterNetworking.configCompressionEnabled.SettingChanged += ConfigCompressionEnabled_SettingChanged;
        }
        private static void ConfigCompressionEnabled_SettingChanged(object sender, EventArgs e) {
            SetCompressionEnabledFromConfig();
        }

        private static void SetCompressionEnabledFromConfig() {
            int newCompressionStatus;

            if (BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true) {
                newCompressionStatus = CompressionStatus.COMPRESSION_STATUS_ENABLED;
                BN_Logger.LogMessage($"Compression: Enabling");
            } else {
                newCompressionStatus = CompressionStatus.COMPRESSION_STATUS_DISABLED;
                BN_Logger.LogMessage($"Compression: Disabling");
            }

            SendCompressionEnabledStatus(newCompressionStatus);

            CompressionStatus.status.enabled = newCompressionStatus;
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ref ZNetPeer peer) {
            CompressionStatus.Add(peer);

            RegisterRPCs(peer);
            SendCompressionVersion(peer, CompressionStatus.status.version);
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPrefix]
        private static void OnDisconnect(ZNetPeer peer) {
            CompressionStatus.Remove(peer);
        }

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static bool SendCompressedPackages(ref ZSteamSocket __instance, ref Queue<Byte[]> ___m_sendQueue, ref int ___m_totalSent, ref HSteamNetConnection ___m_con) {
            if (!__instance.IsConnected()) {
                return false;
            }

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.EnabledWith(peer)) {
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
                            compressedMessage = LZ4Pickler.Pickle(compressedPackagesStream.ToArray());
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
                        BN_Logger.LogError($"Compressed Send ({BN_Utils.GetPeerName(peer)}): ERROR {eresult}; disabling compression for this message; please notify mod author");
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
            if (!CompressionStatus.EnabledWith(peer)) {
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
                    uncompressedPackages = LZ4Pickler.Unpickle(compressedPackages);
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

            public const int COMPRESSION_STATUS_ENABLED = 1;
            public const int COMPRESSION_STATUS_DISABLED = 0;

            public static PeerCompressionStatus status = new PeerCompressionStatus() { version = COMPRESSION_VERSION, enabled = (BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true ? 1 : 0) };
            private readonly static Dictionary<ZNetPeer, PeerCompressionStatus> peerStatuses = new Dictionary<ZNetPeer, PeerCompressionStatus>();
            public class PeerCompressionStatus {
                public int version = COMPRESSION_VERSION_UNKNOWN;
                public int enabled = 0;
            }
            public static bool Add(ZNetPeer peer) {
                if (peer == null) {
                    BN_Logger.LogError("Compression: Tried to add null peer");
                    return false;
                }

                if (peerStatuses.ContainsKey(peer)) {
                    BN_Logger.LogError($"Compression: Tried to add already added peer: {BN_Utils.GetPeerName(peer)}");
                    return true;
                }

                peerStatuses.Add(peer, new PeerCompressionStatus());
                BN_Logger.LogMessage($"Compression: Added {BN_Utils.GetPeerName(peer)}");
                return true;
            }
            public static void Remove(ZNetPeer peer) {
                if (!PeerAdded(peer)) {
                    BN_Logger.LogError($"Compression: Tried to remove non-existent peer: {BN_Utils.GetPeerName(peer)}");
                    return;
                }

                BN_Logger.LogMessage($"Compression: Removing {BN_Utils.GetPeerName(peer)}");
                peerStatuses.Remove(peer);
            }
            public static bool PeerAdded(ZNetPeer peer) {
                if (peer != null && peerStatuses.ContainsKey(peer)) { return true; }
#if DEBUG
                BN_Logger.LogInfo($"Compression: Peer not added: {BN_Utils.GetPeerName(peer)}");
#endif
                return false;
            }

            public static bool EnabledWith(ZNetPeer peer) {
                return ((CompressionStatus.VersionCompatibleWith(peer)) &&
                        (status.enabled   == COMPRESSION_STATUS_ENABLED) &&
                        (GetEnabled(peer) == COMPRESSION_STATUS_ENABLED));
            }
            public static int GetEnabled(ZNetPeer peer) {
                if (!PeerAdded(peer)) { return COMPRESSION_STATUS_DISABLED; }
                return peerStatuses[peer].enabled;
            }
            public static bool SetEnabled(ZNetPeer peer, int enabled) {
                if (!PeerAdded(peer)) {
                    BN_Logger.LogError($"Compression: Could not set status for unadded peer {BN_Utils.GetPeerName(peer)}: {enabled}");
                    return false;
                }

                BN_Logger.LogMessage($"Compression: Received compression status from {BN_Utils.GetPeerName(peer)}: {(enabled == COMPRESSION_STATUS_ENABLED ? "enabled" : "disabled")}");

                if (GetEnabled(peer) == enabled) {
                    BN_Logger.LogMessage($"Compression: Compression for {BN_Utils.GetPeerName(peer)} is already {(enabled == COMPRESSION_STATUS_ENABLED ? "enabled" : "disabled")}");
                    return true;
                }

                peerStatuses[peer].enabled = enabled;

                BN_Logger.LogMessage($"Compression: Compression with {BN_Utils.GetPeerName(peer)}: {(EnabledWith(peer) ? "enabled" : "disabled")}");

                return true;
            }

            public static bool VersionCompatibleWith(ZNetPeer peer) {
                return (status.version > 0 && GetVersion(peer) > 0 && status.version == GetVersion(peer));
            }
            public static int GetVersion(ZNetPeer peer) {
                if (!PeerAdded(peer)) { return 0; }
                return peerStatuses[peer].version;
            }
            public static bool SetVersion(ZNetPeer peer, int version) {
                if (!PeerAdded(peer)) {
                    BN_Logger.LogError($"Compression: Couldn't set version for {BN_Utils.GetPeerName(peer)} ({version})");
                    return false;
                }

                if (GetVersion(peer) == version) {
                    BN_Logger.LogError($"Compression: Version already set for {BN_Utils.GetPeerName(peer)}: {version}");
                    return true;
                }

                peerStatuses[peer].version = version;

                if (status.version == version) {
                    BN_Logger.LogMessage($"Compression: Compression compatible with {BN_Utils.GetPeerName(peer)} ({version})");
                } else if (status.version > version) {
                    BN_Logger.LogWarning($"Compression: {BN_Utils.GetPeerName(peer)} ({version}) has an earlier version of Better Networking; they should update");
                } else {
                    BN_Logger.LogError($"Compression: {BN_Utils.GetPeerName(peer)} ({version}) has a later version of Better Networking; you should update");
                }

                return true;
            }
        }
    }
}
