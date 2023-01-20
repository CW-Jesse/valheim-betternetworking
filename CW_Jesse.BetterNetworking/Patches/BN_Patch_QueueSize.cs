using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_QueueSize {
        private const int DEFAULT_QUEUE_SIZE = 10240;
        private const int DEFAULT_MINIMUM_QUEUE_SIZE = 2048;

        public enum Options_NetworkQueueSize {
            [Description("10240 KB")]
            _10240,
            [Description("1024 KB <b>[default]</b>")]
            _1024,
            [Description("512 KB")]
            _512,
            [Description("128 KB")]
            _128,
            [Description("10 KB [original]")]
            _10
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkQueueSize = config.Bind(
                "Networking",
                "Queue Size",
                Options_NetworkQueueSize._1024,
                new ConfigDescription(
                    "If others experience lag/desync for things <i>around</i> you, increase your queue size.\n" +
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
                case Options_NetworkQueueSize._10240:
                    __result -= 10240 * 1024 + DEFAULT_QUEUE_SIZE;
                    break;
                case Options_NetworkQueueSize._1024:
                    __result -= 1024 * 1024 + DEFAULT_QUEUE_SIZE;
                    break;
                case Options_NetworkQueueSize._512:
                    __result -= 512 * 1024 + DEFAULT_QUEUE_SIZE;
                    break;
                case Options_NetworkQueueSize._128:
                    __result -= 128 * 1024 + DEFAULT_QUEUE_SIZE;
                    break;
            }

#if DEBUG
            BN_Logger.LogInfo($"Queue size reported as {__result} instead of {originalQueueSize}");
#endif
        }
    }
}
