using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    public class BN_Patch_UpdateRate {
        public enum Options_NetworkUpdateRates {
            [Description("100% (20 updates/second) <b>[default]</b>")]
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
                    "If your <i>character</i> is lagging for others, decrease your update rate and/or queue size."
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
