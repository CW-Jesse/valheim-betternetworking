using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CW_Jesse.BetterNetworking.Patches {

    [HarmonyPatch]
    public class BN_Patch_NewConnectionBuffer {
        private static readonly List<ZPackage> packageBuffer = new List<ZPackage>();

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        private class StartBufferingOnNewConnection {
            private static void Postfix(ZNet __instance, ZNetPeer peer) {
                if (!__instance.IsServer()) {
                    peer.m_rpc.Register<ZPackage>("ZDOData", (nullPeer, package) => packageBuffer.Add(package)); // overwritten by ZDOMan.AddPeer
                }
            }
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.AddPeer))]
        private class SendBufferOnAddPeer {
            private static void Postfix(ZDOMan __instance, ZNetPeer netPeer) {
                foreach (ZPackage package in packageBuffer) {
                    AccessTools.Method(typeof(ZDOMan), "RPC_ZDOData").Invoke(__instance, new object[] { netPeer.m_rpc, package });
                }
                packageBuffer.Clear();
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
        private class ClearBufferOnShutdown {
            private static void Postfix() => packageBuffer.Clear();
        }
    }
}
