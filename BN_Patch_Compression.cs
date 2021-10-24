using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Steamworks;
using K4os.Compression.LZ4;
using System.IO;
using BepInEx.Configuration;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_Compression {
        private const int COMPRESSION_VERSION = 2;
        private const string RPC_COMPRESSION_VERSION = "CW_Jesse.BetterNetworking.CompressionVersion";
        private const string RPC_COMPRESSION_STATUS  = "CW_Jesse.BetterNetworking.CompressionStatus";
        
        private const string RPC_COMPRESSION_VERSION_1 = "CW_Jesse.BetterNetworking.GetCompressionVersion"; // backwards compatibility

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configCompressionEnabled = config.Bind(
                "Networking",
                "Compression Enabled",
                true,
                new ConfigDescription("Most people will want to keep this enabled."));

            BetterNetworking.configCompressionEnabled.SettingChanged += ConfigCompressionEnabled_SettingChanged;
        }
        private static void ConfigCompressionEnabled_SettingChanged(object sender, EventArgs e) {
            SetCompressionEnabledFromConfig();
        }

        private static void SetCompressionEnabledFromConfig() {
            int newCompressionStatus  = 0;

            if (BetterNetworking.configCompressionEnabled.Value) {
                newCompressionStatus = CompressionStatus.COMPRESSION_STATUS_ENABLED;
                BN_Logger.LogMessage($"Compression: Enabling");
            } else {
                newCompressionStatus = CompressionStatus.COMPRESSION_STATUS_DISABLED;
                BN_Logger.LogMessage($"Compression: Disabling");
            }

            if (ZNet.instance != null) {
                foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                    if (CompressionStatus.VersionCompatibleWith(peer)) {
                        BN_Logger.LogMessage($"Compression: Sending {BN_Utils.GetPeerName(peer)} our compression status: ({newCompressionStatus})");
                        peer.m_rpc.Invoke(RPC_COMPRESSION_STATUS, new object[] { newCompressionStatus });
                    }
                }
            }

            CompressionStatus.status.enabled = newCompressionStatus;
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ref ZNetPeer peer) {
            CompressionStatus.Add(peer);

            peer.m_rpc.Register<int>(RPC_COMPRESSION_VERSION, new Action<ZRpc, int>(RPC_CompressionVersion));
            peer.m_rpc.Register<int>(RPC_COMPRESSION_VERSION_1, new Action<ZRpc, int>(RPC_CompressionVersion)); // backwards compatibility
            peer.m_rpc.Register<int>(RPC_COMPRESSION_STATUS, new Action<ZRpc, int>(RPC_CompressionEnabled));

            int compressionVersion = CompressionStatus.status.version;
            peer.m_rpc.Invoke(RPC_COMPRESSION_VERSION, new object[] { compressionVersion });
            BN_Logger.LogMessage($"Compression: Version sent to {BN_Utils.GetPeerName(peer)}: {compressionVersion}");
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPrefix]
        private static void OnDisconnect(ZNetPeer peer) {
            CompressionStatus.Remove(peer);
        }
        private static void RPC_CompressionVersion(ZRpc rpc, int version) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            BN_Logger.LogMessage($"Received: Received compression version from {BN_Utils.GetPeerName(peer)}: {version}");
            if (CompressionStatus.SetVersion(peer, version)) {
                if (CompressionStatus.VersionCompatibleWith(peer)) {
                    BN_Logger.LogMessage($"Compression: Sending {BN_Utils.GetPeerName(peer)} our compression status: ({CompressionStatus.status.enabled})");
                    peer.m_rpc.Invoke(RPC_COMPRESSION_STATUS, new object[] { CompressionStatus.status.enabled });
                }
            }
        }
        private static void RPC_CompressionEnabled(ZRpc rpc, int enabled) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            if (CompressionStatus.SetEnabled(peer, enabled)) {

            }
        }
        private static void SendCompressionEnabledStatus(ZNetPeer peer, int enabled) {
            if (ZNet.instance != null) {
                BN_Logger.LogMessage($"Compression: Sending compression status to {BN_Utils.GetPeerName(peer)}: {enabled}");
                peer.m_rpc.Invoke(RPC_COMPRESSION_STATUS, new object[] { CompressionStatus.COMPRESSION_STATUS_DISABLED });
            }
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

            while (___m_sendQueue.Count > 0) {

                byte[][] packageArray = ___m_sendQueue.ToArray();
                int uncompressedPackagesLength = 0;
                byte[] compressedPackages;

                using (MemoryStream compressedPackagesStream = new MemoryStream()) {
                    using (BinaryWriter compressedPackagesWriter = new BinaryWriter(compressedPackagesStream)) {

                        compressedPackagesWriter.Write(packageArray.Length); // number of packages

                        for (int i = 0; i < packageArray.Length; i++) {
                            compressedPackagesWriter.Write(packageArray[i].Length); // length of package
                            compressedPackagesWriter.Write(packageArray[i]); // package
                            uncompressedPackagesLength += packageArray[i].Length;
                        }

                        compressedPackagesWriter.Flush();
                        compressedPackages = LZ4Pickler.Pickle(compressedPackagesStream.ToArray());
                    }
                }

                IntPtr intPtr = Marshal.AllocHGlobal(compressedPackages.Length);
                Marshal.Copy(compressedPackages, 0, intPtr, compressedPackages.Length);

                EResult eresult;
                long messagesSentCount;
                if (BN_Utils.IsDedicated()) {
                    eresult = SteamGameServerNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedPackages.Length, 8, out messagesSentCount);
                } else {
                    eresult = SteamNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedPackages.Length, 8, out messagesSentCount);
                }

                Marshal.FreeHGlobal(intPtr);

                if (eresult != EResult.k_EResultOK) {
                    BN_Logger.LogError($"Compressed Send ({BN_Utils.GetPeerName(peer)}): ERROR {eresult}; please notify the mod author");
                    return true;
                }
                ___m_totalSent += compressedPackages.Length;
                for (int i = 0; i < packageArray.Length; i++) {
                    ___m_sendQueue.Dequeue(); // TODO: inefficient
                }

                if (BetterNetworking.configLogMessages.Value) {
                    if (uncompressedPackagesLength > 256) { // small messages don't compress well but they also don't matter
                        float compressedSizePercentage = ((float)compressedPackages.Length / (float)uncompressedPackagesLength) * 100;
                        BN_Logger.LogInfo($"Compressed Send ({BN_Utils.GetPeerName(peer)}): {uncompressedPackagesLength} B compressed to {compressedSizePercentage.ToString("0")}%");
                    }
                }
            }

            return false;
         }

        private static Queue<ZPackage> packages = new Queue<ZPackage>();

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

                byte[] uncompressedPackages;
                try {
                    uncompressedPackages = LZ4Pickler.Unpickle(compressedPackages);
                } catch {
                    BN_Logger.LogInfo($"Compressed Receive ({BN_Utils.GetPeerName(peer)}): Couldn't decompress message; assuming uncompressed");

                    ZPackage zpackage = new ZPackage(compressedPackages);
                    steamNetworkingMessage_t.m_pfnRelease = array[0];
                    steamNetworkingMessage_t.Release();
                    ___m_totalRecv += zpackage.Size();
                    ___m_gotData = true;
                    __result = zpackage;
                    return false;
                }

                if (BetterNetworking.configLogMessages.Value) {
                    if (uncompressedPackages.Length > 256) { // small messages don't compress well but they also don't matter
                        float compressedSizePercentage = ((float)steamNetworkingMessage_t.m_cbSize / (float)uncompressedPackages.Length) * 100;
                        BN_Logger.LogInfo($"Compressed Receive ({BN_Utils.GetPeerName(peer)}): {uncompressedPackages.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
                    }
                }

                steamNetworkingMessage_t.m_pfnRelease = array[0];
                steamNetworkingMessage_t.Release();

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

            public static PeerCompressionStatus status = new PeerCompressionStatus() { version = COMPRESSION_VERSION, enabled = BetterNetworking.configCompressionEnabled.Value ? 1 : 0 };
            private static Dictionary<ZNetPeer, PeerCompressionStatus> peerStatuses = new Dictionary<ZNetPeer, PeerCompressionStatus>();
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
                BN_Logger.LogError($"Compression: Peer not added: {BN_Utils.GetPeerName(peer)}");
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
                    BN_Logger.LogError($"Compression: Tried to set enabled status for unadded peer {BN_Utils.GetPeerName(peer)}: {enabled}");
                    return false;
                }

                if (GetEnabled(peer) == enabled) {
                    BN_Logger.LogError($"Compression: Didn't change enabled status for {BN_Utils.GetPeerName(peer)}: {enabled}");
                    return true;
                }

                peerStatuses[peer].enabled = enabled;

                switch (enabled) {
                    case COMPRESSION_STATUS_ENABLED:
                        BN_Logger.LogMessage($"Compression: {BN_Utils.GetPeerName(peer)} has enabled compression");
                        break;
                    case COMPRESSION_STATUS_DISABLED:
                        BN_Logger.LogMessage($"Compression: {BN_Utils.GetPeerName(peer)} has disabled compression");
                        break;
                }

                if (EnabledWith(peer)) {
                    BN_Logger.LogMessage($"Compression: Enabled with {BN_Utils.GetPeerName(peer)}");
                } else {
                    BN_Logger.LogMessage($"Compression: Disabled with {BN_Utils.GetPeerName(peer)}");
                }

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
