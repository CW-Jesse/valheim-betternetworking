using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    public class BN_Patch_UpdateRate {
        public enum Options_NetworkUpdateRates {
            [Description("100% (20 updates/second)")]
            _100,
            [Description("80% (16 updates/second)")]
            _80,
            [Description("60% (12 updates/second)")]
            _60,
            [Description("40% (8 updates/second)")]
            _40,
            [Description("20% (4 updates/second)")]
            _20
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configNetworkUpdateRate = config.Bind(
                "Networking",
                "Update Rate",
                Options_NetworkUpdateRates._100,
                new ConfigDescription(
                    "You can reduce network strain by reducing how frequently your computer sends out updates. Listed values are correct as of patch 0.203.11."
                ));
        }



        [HarmonyPatch(typeof(ZDOMan))]
        class NetworkUpdateFrequency_Patch {

            [HarmonyPatch("SendZDOToPeers")]
            static void Prefix(ref float dt) {
                float networkUpdateRate = 1.0f;
                switch (BetterNetworking.configNetworkUpdateRate.Value) {
                    case Options_NetworkUpdateRates._80:
                        networkUpdateRate = 0.8f;
                        break;
                    case Options_NetworkUpdateRates._60:
                        networkUpdateRate = 0.6f;
                        break;
                    case Options_NetworkUpdateRates._40:
                        networkUpdateRate = 0.4f;
                        break;
                    case Options_NetworkUpdateRates._20:
                        networkUpdateRate = 0.2f;
                        break;
                }

                dt *= networkUpdateRate;
            }
        }
    }
}
