using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_QueueSize {
        public enum Options_NetworkQueueSize {
            [Description("300% (30 KB)")]
            _300,
            [Description("200% (20 KB)")]
            _200,
            [Description("150% (15 KB)")]
            _150,
            [Description("100% (10 KB)")]
            _100,
            [Description("80% (8 KB)")]
            _80,
            [Description("60% (6 KB)")]
            _60,
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkQueueSize = config.Bind(
                "Networking",
                "Queue Size",
                Options_NetworkQueueSize._100,
                new ConfigDescription(
                    "With low upload speeds, lowering your queue size allows Valheim to better prioritize outgoing data. Listed values are correct as of patch 0.203.11."
                ));
        }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.GetSendQueueSize))]
        static void Postfix(ref int __result) {

#if DEBUG
            int originalQueueSize = __result;
#endif
            switch (BetterNetworking.configNetworkQueueSize.Value) {
                case Options_NetworkQueueSize._300:
                    __result /= 3;
                    break;
                case Options_NetworkQueueSize._200:
                    __result /= 2;
                    break;
                case Options_NetworkQueueSize._150:
                    __result = (int)(__result / 1.5);
                    break;
                case Options_NetworkQueueSize._80:
                    __result = (int)(__result / 0.8);
                    break;
                case Options_NetworkQueueSize._60:
                    __result = (int)(__result / 0.6);
                    break;
            }

#if DEBUG
            BN_Logger.LogInfo($"Queue size reported as {__result} instead of {originalQueueSize}");
#endif
        }
    }
}
