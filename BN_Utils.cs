using Steamworks;

namespace CW_Jesse.BetterNetworking {
    class BN_Utils {
        public static ZNetPeer GetPeer(ZRpc rpc) {
            foreach (ZNetPeer znetPeer in ZNet.instance.GetPeers()) {
                if (znetPeer.m_rpc == rpc) {
                    return znetPeer;
                }
            }
            return null;
        }
        public static ZNetPeer GetPeer(ZSteamSocket steamSocket) {
            return ZNet.instance.GetPeerByHostName(steamSocket.GetHostName());
        }

        public static string GetPeerHostname(ZSteamSocket steamSocket) {
            return steamSocket.GetHostName();
        }
        public static string GetPeerHostname(ZNetPeer peer) {
            return peer.m_socket.GetHostName();
        }
        public static string GetPeerHostname(ZRpc rpc) {
            return GetPeerHostname((ZSteamSocket)rpc.GetSocket());
        }

        public static bool IsDedicated() {
            return ZNet.instance.IsDedicated();
        }
    }
}
