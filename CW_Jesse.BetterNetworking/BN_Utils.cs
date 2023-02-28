using Steamworks;

namespace CW_Jesse.BetterNetworking {
    class BN_Utils {

        public static ZNetPeer GetPeer(ZRpc rpc) {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                if (peer.m_rpc == rpc) return peer;
            }
            return null;
        }
        public static ZNetPeer GetPeer(ISocket socket) {
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) {
                if (peer.m_socket == socket) return peer;
            }
            return null;
        }
        
        public static string GetPeerName(ZNetPeer peer) {
            if (peer == null) return "[null]";
            if (peer.m_server) return "[server]";
            return $"{peer.m_playerName}[{peer.m_socket.GetHostName()}]";
            // return $"{peer.m_playerName}[{peer.m_socket.GetEndPointString()}]";
        }
        public static string GetPeerName(ISocket socket) {
            return GetPeerName(GetPeer(socket));
        }

        public static bool isDedicated = false;
    }
}
