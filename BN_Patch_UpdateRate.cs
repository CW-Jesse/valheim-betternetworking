using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    public class BN_Patch_UpdateRate {
        public enum Options_NetworkUpdateRates {
            [Description("100% (20 updates/second)")]
            _100,
            [Description("75% (15 updates/second)")]
            _75,
            [Description("50% (10 updates/second)")]
            _50
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configNetworkUpdateRate = config.Bind(
                "Networking",
                "Update Rate",
                Options_NetworkUpdateRates._100,
                new ConfigDescription(
                    "If Person A is experiencing desync/lag while Person B is around, <i>including their character</i>, <b>Person B</b> needs to <b>decrease</b> their update rate and/or queue size.\n" +
                    "---\n" +
                    "This mod is CPU-heavy. If you experience performance issues not related to networking, decrease the update rate."
                ));
        }



        [HarmonyPatch(typeof(ZDOMan))]
        class NetworkUpdateFrequency_Patch {

            [HarmonyPatch("SendZDOToPeers")]
            static void Prefix(ref float dt) {
                switch (BetterNetworking.configNetworkUpdateRate.Value) {
                    case Options_NetworkUpdateRates._75:
                        dt *= 0.75f;
                        return;
                    case Options_NetworkUpdateRates._50:
                        dt *= 0.5f;
                        return;
                }
            }
        }
    }
}
