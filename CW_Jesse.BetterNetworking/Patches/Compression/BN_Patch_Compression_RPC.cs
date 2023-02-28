using System;

namespace CW_Jesse.BetterNetworking {
    public partial class BN_Patch_Compression {
        private const string RPC_COMPRESSION_VERSION = "CW_Jesse.BetterNetworking.CompressionVersion";
        private const string RPC_COMPRESSION_ENABLED = "CW_Jesse.BetterNetworking.CompressionEnabled";
        private const string RPC_COMPRESSION_STARTED = "CW_Jesse.BetterNetworking.CompressedStarted";

        private static void RegisterRPCs(ZNetPeer peer) {
            peer.m_rpc.Register<int>(RPC_COMPRESSION_VERSION, RPC_CompressionVersion);
            peer.m_rpc.Register<bool>(RPC_COMPRESSION_ENABLED, RPC_CompressionEnabled);
            peer.m_rpc.Register<bool>(RPC_COMPRESSION_STARTED, RPC_CompressionStarted);
        }

        public static void SendCompressionVersion(ZRpc rpc) {
            rpc.Invoke(RPC_COMPRESSION_VERSION, new object[] { CompressionStatus.ourStatus.version });
        }
        private static void RPC_CompressionVersion(ZRpc rpc, int version) {
            ISocket socket = rpc.GetSocket();
            CompressionStatus.SetVersion(socket, version);

            if (CompressionStatus.ourStatus.version == version) {
                BN_Logger.LogMessage($"Compression: Compatible with {BN_Utils.GetPeerName(socket)}");
            } else if (CompressionStatus.ourStatus.version > version) {
                BN_Logger.LogWarning($"Compression: {BN_Utils.GetPeerName(socket)} ({version}) has an older version of Better Networking; they should update");
            } else if (version > 0) {
                BN_Logger.LogError($"Compression: {BN_Utils.GetPeerName(socket)} ({version}) has a newer version of Better Networking; you should update");
            }

            if (CompressionStatus.GetIsCompatibleWith(socket)) {
                SendCompressionEnabledStatus(rpc);
            }
        }

        private static void SendCompressionEnabledStatus() {
            if (ZNet.instance == null) { return; }
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                if (CompressionStatus.GetIsCompatibleWith(peer.m_socket)) {
                    SendCompressionEnabledStatus(peer.m_rpc);
                }
            }
        }
        private static void SendCompressionEnabledStatus(ZRpc rpc) {
            if (ZNet.instance == null) { return; }
            rpc.Invoke(RPC_COMPRESSION_ENABLED, new object[] { CompressionStatus.ourStatus.compressionEnabled });
            if (CompressionStatus.ourStatus.compressionEnabled && CompressionStatus.GetCompressionEnabled(rpc.GetSocket())) {
                SendCompressionStarted(rpc, true);
            } else {
                SendCompressionStarted(rpc, false); // don't start compression if either peer has it disabled
            }
        }

        private static void RPC_CompressionEnabled(ZRpc rpc, bool peerCompressionEnabled) {
            CompressionStatus.SetCompressionEnabled(rpc.GetSocket(), peerCompressionEnabled);
            if (CompressionStatus.ourStatus.compressionEnabled && peerCompressionEnabled) {
                SendCompressionStarted(rpc, true);
            } else {
                SendCompressionStarted(rpc, false); // don't start compression if either peer has it disabled
            }
        }

        private static void SendCompressionStarted(ZRpc rpc, bool started) {
            if (ZNet.instance == null) { return; }
            if (CompressionStatus.GetSendCompressionStarted(rpc.GetSocket()) == started) { return; } // don't do anything if nothing's changed
            Flush(rpc);
            rpc.Invoke(RPC_COMPRESSION_STARTED, new object[] { started });
            Flush(rpc);
            CompressionStatus.SetSendCompressionStarted(rpc.GetSocket(), started);
            BN_Logger.LogMessage($"Compression: Compression to {BN_Utils.GetPeerName(rpc.GetSocket())}: {started}");
        }

        private static void Flush(ZRpc rpc) {
            switch (ZNet.m_onlineBackend) {
                case OnlineBackendType.Steamworks:
                    rpc.GetSocket().Flush(); // since we compress the entire send queue, flush existing send queue before starting/stopping compression
                    break;
                case OnlineBackendType.PlayFab:
                    BN_Patch_Compression_PlayFab.FlushQueue(rpc.GetSocket());    
                    break;
            }
        }
        private static void RPC_CompressionStarted(ZRpc rpc, bool peerCompressionStarted) {
            Flush(rpc);
            CompressionStatus.SetReceiveCompressionStarted(rpc.GetSocket(), peerCompressionStarted);
            Flush(rpc);
            BN_Logger.LogMessage($"Compression: Compression from {BN_Utils.GetPeerName(rpc.GetSocket())}: {peerCompressionStarted}");
        }
    }
}