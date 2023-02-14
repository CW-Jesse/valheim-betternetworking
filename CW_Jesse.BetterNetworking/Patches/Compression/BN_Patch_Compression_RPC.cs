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

        public static void SendCompressionVersion(ZNetPeer peer) {
            if (ZNet.instance == null) { return; }
            peer.m_rpc.Invoke(RPC_COMPRESSION_VERSION, new object[] { CompressionStatus.ourStatus.version });
        }
        private static void RPC_CompressionVersion(ZRpc rpc, int version) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            CompressionStatus.SetVersion(peer, version);

            if (CompressionStatus.ourStatus.version == version) {
                BN_Logger.LogMessage($"Compression: Compatible with {BN_Utils.GetPeerName(peer)}");
            } else if (CompressionStatus.ourStatus.version > version) {
                BN_Logger.LogWarning($"Compression: {BN_Utils.GetPeerName(peer)} ({version}) has an older version of Better Networking; they should update");
            } else if (version > 0) {
                BN_Logger.LogError($"Compression: {BN_Utils.GetPeerName(peer)} ({version}) has a newer version of Better Networking; you should update");
            }

            if (CompressionStatus.GetIsCompatibleWith(peer)) {
                SendCompressionEnabledStatus(peer);
            }
        }

        public static void SendCompressionEnabledStatus() {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                if (CompressionStatus.GetIsCompatibleWith(peer)) {
                    SendCompressionEnabledStatus(peer);
                }
            }
        }
        private static void SendCompressionEnabledStatus(ZNetPeer peer) {
            if (ZNet.instance == null) { return; }
            peer.m_rpc.Invoke(RPC_COMPRESSION_ENABLED, new object[] { CompressionStatus.ourStatus.compressionEnabled });
            if (CompressionStatus.ourStatus.compressionEnabled && CompressionStatus.GetCompressionEnabled(peer)) {
                SendCompressionStarted(peer, true);
            } else {
                SendCompressionStarted(peer, false); // don't start compression if either peer has it disabled
            }
        }

        private static void RPC_CompressionEnabled(ZRpc rpc, bool peerCompressionEnabled) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            CompressionStatus.SetCompressionEnabled(peer, peerCompressionEnabled);
            if (CompressionStatus.ourStatus.compressionEnabled && peerCompressionEnabled) {
                SendCompressionStarted(peer, true);
            } else {
                SendCompressionStarted(peer, false); // don't start compression if either peer has it disabled
            }
        }

        private static void SendCompressionStarted(ZNetPeer peer, bool started) {
            if (ZNet.instance == null) { return; }
            if (CompressionStatus.GetSendCompressionStarted(peer) == started) { return; } // don't do anything if nothing's changed
            peer.m_rpc.Invoke(RPC_COMPRESSION_STARTED, new object[] { started });
            switch (ZNet.m_onlineBackend) {
                case OnlineBackendType.Steamworks:
                    peer.m_socket.Flush(); // since we compress the entire send queue, flush existing send queue before starting/stopping compression
                    break;
                case OnlineBackendType.PlayFab:
                    //peer.m_socket.Flush(); // not necessary as we don't compress entire queue, would throw NotImplementedException anyway
                    break;
            }
            CompressionStatus.SetSendCompressionStarted(peer, started);
            BN_Logger.LogMessage($"Compression: Compression to {BN_Utils.GetPeerName(peer)}: {started}");
        }
        private static void RPC_CompressionStarted(ZRpc rpc, bool peerCompressionStarted) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            CompressionStatus.SetReceiveCompressionStarted(peer, peerCompressionStarted);
            BN_Logger.LogMessage($"Compression: Compression from {BN_Utils.GetPeerName(peer)}: {peerCompressionStarted}");
        }
    }
}