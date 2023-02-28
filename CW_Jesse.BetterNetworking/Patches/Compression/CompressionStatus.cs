using System.Collections.Generic;
using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {
    internal static class CompressionStatus {

        private const int COMPRESSION_VERSION = 6;
        private const int COMPRESSION_VERSION_UNKNOWN = 0;

        public static SocketCompressionStatus ourStatus = new SocketCompressionStatus() { version = COMPRESSION_VERSION, compressionEnabled = BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true };
        private readonly static Dictionary<ISocket, SocketCompressionStatus> socketStatuses = new Dictionary<ISocket, SocketCompressionStatus>();
        public class SocketCompressionStatus {
            public int version = COMPRESSION_VERSION_UNKNOWN;
            public bool compressionEnabled = false;
            public bool receivingCompressed = false;
            public bool sendingCompressed = false;
        }
        public static bool AddSocket(ISocket socket) {
            if (socket == null) {
                BN_Logger.LogWarning("Compression: Tried to add null peer");
                return false;
            }
            if (IsSocketExist(socket)) {
                BN_Logger.LogWarning($"Compression: Removing existing peer ({BN_Utils.GetPeerName(socket)}); did they lose internet or Alt+F4?");
                RemoveSocket(socket);
            }

            BN_Logger.LogMessage($"Compression: {BN_Utils.GetPeerName(socket)} connected");
            socketStatuses.Add(socket, new SocketCompressionStatus());
            return true;
        }
        public static void RemoveSocket(ISocket socket) {
            if (!IsSocketExist(socket)) {
                BN_Logger.LogWarning($"Compression: Tried to remove non-existent peer: {BN_Utils.GetPeerName(socket)}");
                return;
            }

            socketStatuses.Remove(socket);
        }
        public static bool IsSocketExist(ISocket socket) {
            if (socket != null && socketStatuses.ContainsKey(socket)) { return true; }
            return false;
        }

        public static int GetVersion(ISocket socket) {
            if (!IsSocketExist(socket)) { return 0; }
            return socketStatuses[socket].version;
        }
        public static void SetVersion(ISocket socket, int theirVersion) {
            if (!IsSocketExist(socket)) { return; }
            socketStatuses[socket].version = theirVersion;
        }
        public static bool GetIsCompatibleWith(ISocket socket) {
            if (!IsSocketExist(socket)) { return false; }
            return (ourStatus.version == GetVersion(socket));
        }

        public static bool GetCompressionEnabled(ISocket socket) {
            if (!IsSocketExist(socket)) { return false; }
            return socketStatuses[socket].compressionEnabled;
        }
        public static void SetCompressionEnabled(ISocket socket, bool enabled) {
            if (!IsSocketExist(socket)) { return; }
            socketStatuses[socket].compressionEnabled = enabled;
        }

        public static bool GetSendCompressionStarted(ISocket socket) {
            if (!IsSocketExist(socket)) { return false; }
            return socketStatuses[socket].sendingCompressed;
        }
        public static void SetSendCompressionStarted(ISocket socket, bool started) {
            if (!IsSocketExist(socket)) { return; }
            socketStatuses[socket].sendingCompressed = started;
        }

        public static bool GetReceiveCompressionStarted(ISocket socket) {
            if (!IsSocketExist(socket)) { return false; }
            return socketStatuses[socket].receivingCompressed;
        }
        public static void SetReceiveCompressionStarted(ISocket socket, bool started) {
            if (!IsSocketExist(socket)) { return; }
            socketStatuses[socket].receivingCompressed = started;
        }
    }
}
