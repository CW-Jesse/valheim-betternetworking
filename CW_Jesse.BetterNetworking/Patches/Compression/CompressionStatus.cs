using System.Collections.Generic;
using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {
    internal static class CompressionStatus {

        private const int COMPRESSION_VERSION = 5;
        private const int COMPRESSION_VERSION_UNKNOWN = 0;

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
            if (IsPeerExist(peer)) {
                BN_Logger.LogWarning($"Attempted to add existing peer; assuming disconnection");
                RemovePeer(peer);
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
