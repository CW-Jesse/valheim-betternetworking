using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_ForceCrossplay {
        public enum Options_ForceCrossplay {
            [Description("Vanilla behaviour <b>[default]</b>")]
            vanilla,
            [Description("Crossplay ENABLED")]
            playfab,
            [Description("Crossplay DISABLED")]
            steamworks
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configForceCrossplay = config.Bind(
                "Dedicated Server",
                "Force Crossplay",
                Options_ForceCrossplay.vanilla,
                new ConfigDescription(
                    "Requires restart.\nplayfab (crossplay enabled): Forces dedicated servers to use new PlayFab networking stack.\nsteamworks (crossplay disabled): Forces dedicated servers to use Steamworks network stack.\nvanilla: Listen for -crossplay flag."
                ));
        }

        [HarmonyPatch(typeof(FejdStartup), "ParseServerArguments")]
        [HarmonyPostfix]
        private static void ForceCrossplay() {
            if (BN_Utils.isDedicated) {
                if (BetterNetworking.configForceCrossplay.Value == Options_ForceCrossplay.playfab) {
                    ZNet.m_onlineBackend = OnlineBackendType.PlayFab;
                    ZPlayFabMatchmaking.LookupPublicIP();
                }
                if (BetterNetworking.configForceCrossplay.Value == Options_ForceCrossplay.steamworks) {
                    ZNet.m_onlineBackend = OnlineBackendType.Steamworks;
                }
            }
        }
    }
}
