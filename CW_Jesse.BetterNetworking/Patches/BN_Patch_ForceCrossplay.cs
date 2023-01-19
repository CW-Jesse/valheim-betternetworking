using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_ForceCrossplay {
        public enum Options_ForceCrossplay {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configForceCrossplay = config.Bind(
                "Networking",
                "Force Crossplay",
                Options_ForceCrossplay.@true,
                new ConfigDescription(
                    "Forces dedicated servers to use new Azure PlayFab networking stack. Requires restart."
                ));
        }

        [HarmonyPatch(typeof(FejdStartup), "ParseServerArguments")]
        [HarmonyPostfix]
        private static void ForceCrossplay() {
            if (BetterNetworking.configForceCrossplay.Value == Options_ForceCrossplay.@true) {
                ZNet.m_onlineBackend = OnlineBackendType.PlayFab;
                ZPlayFabMatchmaking.LookupPublicIP();
            }
        }
    }
}
