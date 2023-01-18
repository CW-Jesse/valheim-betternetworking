using System;

namespace CW_Jesse.BetterNetworking {
    public partial class BN_Patch_Compression {
        private const string RPC_COMPRESSION_VERSION = "CW_Jesse.BetterNetworking.CompressionVersion";
        private const string RPC_COMPRESSION_ENABLED = "CW_Jesse.BetterNetworking.CompressionEnabled";
        private const string RPC_COMPRESSION_STARTED = "CW_Jesse.BetterNetworking.CompressedStarted";

        public static void RegisterRPCs(ZNetPeer peer) {
            peer.m_rpc.Register<int>(RPC_COMPRESSION_VERSION, new Action<ZRpc, int>(RPC_CompressionVersion));
            peer.m_rpc.Register<bool>(RPC_COMPRESSION_ENABLED, new Action<ZRpc, bool>(RPC_CompressionEnabled));
            peer.m_rpc.Register<bool>(RPC_COMPRESSION_STARTED, new Action<ZRpc, bool>(RPC_CompressionStarted));
        }

        public static void SendCompressionVersion(ZNetPeer peer, int compressionVersion) {
            if (ZNet.instance == null) { return; }
            peer.m_rpc.Invoke(RPC_COMPRESSION_VERSION, new object[] { compressionVersion });
        }
        private static void RPC_CompressionVersion(ZRpc rpc, int version) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            CompressionStatus.SetVersion(peer, version);
            if (CompressionStatus.GetIsCompatibleWith(peer)) {
                SendCompressionEnabledStatus(peer, CompressionStatus.ourStatus.compressionEnabled);
            }
        }

        public static void SendCompressionEnabledStatus(bool compressionEnabled) {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                if (CompressionStatus.GetIsCompatibleWith(peer)) {
                    SendCompressionEnabledStatus(peer, compressionEnabled);
                }
            }
        }
        private static void SendCompressionEnabledStatus(ZNetPeer peer, bool compressionEnabled) {
            if (ZNet.instance == null) { return; }
            peer.m_rpc.Invoke(RPC_COMPRESSION_ENABLED, new object[] { compressionEnabled });
        }

        private static void RPC_CompressionEnabled(ZRpc rpc, bool enabled) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            CompressionStatus.SetCompressionEnabled(peer, enabled);
            if (CompressionStatus.ourStatus.compressionEnabled && CompressionStatus.GetCompressionEnabled(peer)) {
                SendCompressionStarted(peer, enabled);
            } else {
                SendCompressionStarted(peer, false); // don't start compression if either peer has it disabled
            }
        }

        private static void SendCompressionStarted(ZNetPeer peer, bool started) {
            if (ZNet.instance == null) { return; }
            peer.m_rpc.Invoke(RPC_COMPRESSION_STARTED, new object[] { started });
            CompressionStatus.SetSendCompressionStarted(peer, started);
        }
        private static void RPC_CompressionStarted(ZRpc rpc, bool started) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            CompressionStatus.SetReceiveCompressionStarted(peer, started);
        }
    }
}