using Steamworks;

namespace CW_Jesse.BetterNetworking {
    class BN_Utils {
        public static string GetPeerName(ISocket socket) {
            if (socket == null) return "null peer";
            if (socket.IsHost()) return "[server]";
            if (!string.IsNullOrEmpty(socket.GetHostName())) return socket.GetHostName();
            if (!string.IsNullOrEmpty(socket.GetEndPointString())) return socket.GetEndPointString();
            return "unknown peer";
        }

        public static bool isDedicated = false;
    }
}
