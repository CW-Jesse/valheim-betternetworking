using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    public class BN_Patch_UpdateRate {
        public enum Options_NetworkUpdateRates {
            [Description("100% <b>[default]</b>")]
            _100,
            [Description("75%")]
            _75,
            [Description("50%")]
            _50
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configNetworkUpdateRate = config.Bind(
                "Networking",
                "Update Rate",
                Options_NetworkUpdateRates._100,
                new ConfigDescription(
                    "Reducing this can help if your upload speed is low.\n" +
                    "100%: 20 updates/second\n" +
                    "75%: 15 updates/second\n" +
                    "50%: 10 updates/second"
                ));
        }



        [HarmonyPatch(typeof(ZDOMan))]
        class NetworkUpdateFrequency_Patch {

            [HarmonyPatch("SendZDOToPeers2")]
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
