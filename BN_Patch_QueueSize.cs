using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_QueueSize {
        public enum Options_NetworkQueueSize {
            [Description("1200% (120 KB)")]
            _1200,
            [Description("900% (90 KB)")]
            _900,
            [Description("600% (60 KB)")]
            _600,
            [Description("450% (45 KB)")]
            _450,
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
                Options_NetworkQueueSize._600,
                new ConfigDescription(
                    "If Person A is experiencing desync/lag while Person B is around, but their character seems fine, <b>Person B</b> needs to <b>increase</b> their queue size.\n" +
                    "---\n" +
                    "If Person A is experiencing desync/lag while Person B is around, <i>including their character</i>, <b>Person B</b> needs to <b>decrease</b> their update rate and/or queue size."
                ));
        }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.GetSendQueueSize))]
        static void Postfix(ref int __result) {

#if DEBUG
            int originalQueueSize = __result;
#endif
            switch (BetterNetworking.configNetworkQueueSize.Value) {
                case Options_NetworkQueueSize._1200:
                    __result /= 12;
                    break;
                case Options_NetworkQueueSize._900:
                    __result /= 9;
                    break;
                case Options_NetworkQueueSize._600:
                    __result /= 6;
                    break;
                case Options_NetworkQueueSize._450:
                    __result = (int)(__result / 4.5);
                    break;
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
