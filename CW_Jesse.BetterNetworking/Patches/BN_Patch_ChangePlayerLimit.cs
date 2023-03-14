using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;

namespace CW_Jesse.BetterNetworking {
    
    [HarmonyPatch]
    public class BN_Patch_ChangePlayerLimit {
        
        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configPlayerLimit = config.Bind(
                "Dedicated Server",
                "Player Limit",
                10,
                new ConfigDescription(
                    "Requires restart. Changes player limit for dedicated servers."
                , new AcceptableValueRange<int>(1, 127)));
        }

        [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SetPlayerLimit(IEnumerable<CodeInstruction> instructions) {
            foreach (CodeInstruction i in instructions) {
                if (BN_Utils.isDedicated && i.Is(OpCodes.Ldc_I4_S, (sbyte)10)) {
                    yield return new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)BetterNetworking.configPlayerLimit.Value);
                } else {
                    yield return i;
                }
            }
        }
        
    }
}