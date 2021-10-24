using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_QueueSize {
        private const int DEFAULT_QUEUE_SIZE = 10240;
        private const int DEFAULT_MINIMUM_QUEUE_SIZE = 2048;

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
                Options_NetworkQueueSize._1200,
                new ConfigDescription(
                    "If Person A is experiencing desync/lag while Person B is around, but Person B's character seems fine, <b>Person B</b> needs to <b>increase</b> their queue size.\n" +
                    "---\n" +
                    "If Person A is experiencing desync/lag while Person B is around, <i>including for Person B's character</i>, <b>Person B</b> needs to <b>decrease</b> their update rate and/or queue size."
                ));
        }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.GetSendQueueSize))]
        static void Postfix(ref int __result) {

#if DEBUG
            int originalQueueSize = __result;
#endif
            switch (BetterNetworking.configNetworkQueueSize.Value) {
                case Options_NetworkQueueSize._1200:
                    __result -= DEFAULT_QUEUE_SIZE * 11;
                    break;
                case Options_NetworkQueueSize._900:
                    __result -= DEFAULT_QUEUE_SIZE * 8;
                    break;
                case Options_NetworkQueueSize._600:
                    __result -= DEFAULT_QUEUE_SIZE * 5;
                    break;
                case Options_NetworkQueueSize._450:
                    __result -= (int)(DEFAULT_QUEUE_SIZE * 3.5);
                    break;
                case Options_NetworkQueueSize._300:
                    __result -= DEFAULT_QUEUE_SIZE * 2;
                    break;
                case Options_NetworkQueueSize._200:
                    __result -= DEFAULT_QUEUE_SIZE * 1;
                    break;
                case Options_NetworkQueueSize._150:
                    __result -= (int)(DEFAULT_QUEUE_SIZE * 0.5);
                    break;
                case Options_NetworkQueueSize._80:
                    __result += (int)(DEFAULT_QUEUE_SIZE / 0.2);
                    break;
                case Options_NetworkQueueSize._60:
                    __result += (int)(DEFAULT_QUEUE_SIZE / 0.4);
                    break;
            }

#if DEBUG
            BN_Logger.LogInfo($"Queue size reported as {__result} instead of {originalQueueSize}");
#endif
        }
    }
}
