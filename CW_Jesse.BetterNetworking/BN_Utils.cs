using Steamworks;

namespace CW_Jesse.BetterNetworking {
    class BN_Utils {
        public static ZNetPeer GetPeer(ZRpc rpc) {
            foreach (ZNetPeer znetPeer in ZNet.instance.GetPeers()) {
                if (znetPeer.m_rpc == rpc) {
                    return znetPeer;
                }
            }
            BN_Logger.LogError("Utils: Didn't find peer by RPC");
            return null;
        }
        public static ZNetPeer GetPeer(ZPlayFabSocket socket) {
            foreach (ZNetPeer znetPeer in ZNet.instance.GetPeers()) {
                if (znetPeer.m_socket.GetHostName() == socket.GetHostName()) {
                    return znetPeer;
                }
            }
            BN_Logger.LogInfo($"Utils: Didn't find peer by socket: {socket.GetHostName()}");
            return null;
        }
        public static ZNetPeer GetPeer(ZSteamSocket socket) {
            foreach (ZNetPeer znetPeer in ZNet.instance.GetPeers()) {
                if (znetPeer.m_socket.GetHostName() == socket.GetHostName()) {
                    return znetPeer;
                }
            }
            BN_Logger.LogInfo($"Utils: Didn't find peer by socket: {socket.GetHostName()}");
            return null;
        }
        public static string GetPeerName(ZSteamSocket socket) {
            return GetPeerName(GetPeer(socket));
        }
        public static string GetPeerName(ZNetPeer peer) {
            if (peer == null) {
                return "null peer";
            }
            if (peer.m_server) {
                return "[server]";
            }
            if (!string.IsNullOrEmpty(peer.m_playerName)) {
                return $"{peer.m_playerName}[{peer.m_socket.GetHostName()}]";
            }
            if (!string.IsNullOrEmpty(peer.m_socket.GetHostName())) {
                return $"[{peer.m_socket.GetHostName()}]";
            }
            return "unknown peer";
        }

        public static bool IsDedicated() {
            return ZNet.instance.IsDedicated();
        }
    }
}
