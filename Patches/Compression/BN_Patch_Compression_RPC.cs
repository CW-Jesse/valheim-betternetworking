using System;

namespace CW_Jesse.BetterNetworking {
    public partial class BN_Patch_Compression {
        private const string RPC_COMPRESSION_VERSION = "CW_Jesse.BetterNetworking.CompressionVersion";
        private const string RPC_COMPRESSION_STATUS = "CW_Jesse.BetterNetworking.CompressionStatus";

        private const string RPC_COMPRESSION_VERSION_1 = "CW_Jesse.BetterNetworking.GetCompressionVersion"; // backwards compatibility

        public static void RegisterRPCs(ZNetPeer peer) {
            peer.m_rpc.Register<int>(RPC_COMPRESSION_VERSION, new Action<ZRpc, int>(RPC_CompressionVersion));
            peer.m_rpc.Register<int>(RPC_COMPRESSION_VERSION_1, new Action<ZRpc, int>(RPC_CompressionVersion)); // backwards compatibility
            peer.m_rpc.Register<int>(RPC_COMPRESSION_STATUS, new Action<ZRpc, int>(RPC_CompressionEnabled));
        }

        private static void SendCompressionEnabledStatus(ZNetPeer peer, int compressionEnabled) {
            BN_Logger.LogMessage($"Compression: Sending compression status to {BN_Utils.GetPeerName(peer)}: {compressionEnabled}");
            peer.m_rpc.Invoke(RPC_COMPRESSION_STATUS, new object[] { CompressionStatus.COMPRESSION_STATUS_DISABLED });
        }

        public static void SendCompressionEnabledStatus(int compressionEnabled) {
            if (ZNet.instance != null) {
                foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                    if (CompressionStatus.VersionCompatibleWith(peer)) {
                        SendCompressionEnabledStatus(peer, compressionEnabled);
                    }
                }
            }
        }

        public static void SendCompressionVersion(ZNetPeer peer, int compressionVersion) {
            if (ZNet.instance != null) {
                BN_Logger.LogMessage($"Compression: Sending version to {BN_Utils.GetPeerName(peer)}: {compressionVersion}");
                peer.m_rpc.Invoke(RPC_COMPRESSION_VERSION, new object[] { compressionVersion });
            }
        }

        private static void RPC_CompressionVersion(ZRpc rpc, int version) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            BN_Logger.LogMessage($"Compression: Version received from {BN_Utils.GetPeerName(peer)}: {version}");
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
    }
}