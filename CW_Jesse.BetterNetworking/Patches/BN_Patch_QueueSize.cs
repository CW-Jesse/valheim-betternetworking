using BepInEx.Configuration;
using HarmonyLib;

using System.ComponentModel;
using static ZPlayFabSocket;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_QueueSize {
        private const int DEFAULT_QUEUE_SIZE = 10240;
        private const int DEFAULT_MINIMUM_QUEUE_SIZE = 2048;


        // higher options are what cause people to run into Steam errors
        public enum Options_NetworkQueueSize {
            [Description("128 KB")]
            _128KB,
            [Description("64 KB <b>[default]</b>")]
            _64KB,
            [Description("32 KB")]
            _32KB,
            [Description("[Valheim default]")]
            _vanilla
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkQueueSize = config.Bind(
                "Networking",
                "Queue Size",
                Options_NetworkQueueSize._64KB,
                new ConfigDescription(
                    "The better your upload speed, the higher you can set this.\n" +
                    "Higher options aren't available as they can cause errors in Steam.\n" +
                    "---\n" +
                    "If others experience lag/desync for things <i>around</i> you, increase your queue size.\n" +
                    "If your <i>character</i> is lagging for others, decrease your update rate and/or queue size."
                ));
        }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.GetSendQueueSize))]
        [HarmonyPostfix]
        static void Steamworks_GetSendQueueSize(ref int __result) {

#if DEBUG
            int originalQueueSize = __result;
#endif
            switch (BetterNetworking.configNetworkQueueSize.Value) {
                case Options_NetworkQueueSize._256KB:
                    __result -= 256 * 1024 - DEFAULT_QUEUE_SIZE;
                    break;
                case Options_NetworkQueueSize._128KB:
                    __result -= 128 * 1024 - DEFAULT_QUEUE_SIZE;
                    break;
                case Options_NetworkQueueSize._64KB:
                    __result -= 64 * 1024 - DEFAULT_QUEUE_SIZE;
                    break;
                case Options_NetworkQueueSize._32KB:
                    __result -= 32 * 1024 - DEFAULT_QUEUE_SIZE;
                    break;
            }
#if DEBUG
            BN_Logger.LogInfo($"Queue size reported as {__result} instead of {originalQueueSize}");
#endif
        }


        [HarmonyPatch(typeof(ZPlayFabSocket), nameof(ZPlayFabSocket.GetSendQueueSize))]
        [HarmonyPrefix]
        static bool PlayFab_GetSendQueueSize(ref int __result, ref InFlightQueue ___m_inFlightQueue) {

            switch (BetterNetworking.configNetworkQueueSize.Value) {
                case Options_NetworkQueueSize._256KB:
                    __result = (int)___m_inFlightQueue.Bytes - (256 * 1024 - DEFAULT_QUEUE_SIZE);
                    return false;
                case Options_NetworkQueueSize._128KB:
                    __result = (int)___m_inFlightQueue.Bytes - (128 * 1024 - DEFAULT_QUEUE_SIZE);
                    return false;
                case Options_NetworkQueueSize._64KB:
                    __result = (int)___m_inFlightQueue.Bytes - (64 * 1024 - DEFAULT_QUEUE_SIZE);
                    return false;
                case Options_NetworkQueueSize._32KB:
                    __result = (int)___m_inFlightQueue.Bytes - (32 * 1024 - DEFAULT_QUEUE_SIZE);
                    return false;
            }
            return true;

        }
    }
}
