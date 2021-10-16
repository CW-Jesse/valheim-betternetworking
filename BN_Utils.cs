using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static bool IsDedicated() {
            return ZNet.instance.IsDedicated();
        }
    }
}
