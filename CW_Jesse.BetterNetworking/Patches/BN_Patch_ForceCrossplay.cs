using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_ForceCrossplay {
        public enum Options_ForceCrossplay {
            [Description("Crossplay ENABLED <b>[default]</b>")]
            playfab,
            [Description("Crossplay DISABLED")]
            steamworks,
            [Description("Vanilla behaviour")]
            vanilla
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configForceCrossplay = config.Bind(
                "Networking",
                "Force Crossplay",
                Options_ForceCrossplay.playfab,
                new ConfigDescription(
                    "Requires restart./nCrossplay enabled: Forces dedicated servers to use new PlayFab networking stack./nCrossplay disabled: Forces dedicated servers to use Steamworks network stack./nVanilla behaviour: Listen for -crossplay flag."
                ));
        }

        [HarmonyPatch(typeof(FejdStartup), "ParseServerArguments")]
        [HarmonyPostfix]
        private static void ForceCrossplay() {
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
