using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_QueueSize {
        private const int DEFAULT_QUEUE_SIZE = 10240;
        private const int DEFAULT_MINIMUM_QUEUE_SIZE = 2048;

        public enum Options_NetworkQueueSize {
            [Description("2400% (240 KB)")]
            _2400,
            [Description("1200% (120 KB) <b>[default]</b>")]
            _1200,
            [Description("600% (60 KB)")]
            _600,
            [Description("300% (30 KB)")]
            _300,
            [Description("100% (10 KB)")]
            _100
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkQueueSize = config.Bind(
                "Networking (Steamworks)",
                "Queue Size",
                Options_NetworkQueueSize._1200,
                new ConfigDescription(
                    "Steamworks: If others experience lag/desync for things <i>around</i> you, increase your queue size.\n" +
                    "---\n" +
                    "If your <i>character</i> is lagging for others, decrease your update rate and/or queue size."
                ));
        }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.GetSendQueueSize))]
        static void Postfix(ref int __result) {

#if DEBUG
            int originalQueueSize = __result;
#endif
            switch (BetterNetworking.configNetworkQueueSize.Value) {
                case Options_NetworkQueueSize._2400:
                    __result -= DEFAULT_QUEUE_SIZE * 23;
                    break;
                case Options_NetworkQueueSize._1200:
                    __result -= DEFAULT_QUEUE_SIZE * 11;
                    break;
                case Options_NetworkQueueSize._600:
                    __result -= DEFAULT_QUEUE_SIZE * 5;
                    break;
                case Options_NetworkQueueSize._300:
                    __result -= DEFAULT_QUEUE_SIZE * 2;
                    break;
            }

#if DEBUG
            BN_Logger.LogInfo($"Queue size reported as {__result} instead of {originalQueueSize}");
#endif
        }
    }
}
