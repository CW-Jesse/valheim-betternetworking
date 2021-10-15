using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System;

namespace CW_Jesse.BetterNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "0.6.3")]
    [BepInProcess("valheim.exe")]
    public class BetterNetworking : BaseUnityPlugin {

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        public static ConfigEntry<bool> configLogMessages;
        public static ConfigEntry<BN_Patch_UpdateRate.Options_NetworkUpdateRates> configNetworkUpdateRate;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRate> configNetworkSendRateMin;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRate> configNetworkSendRateMax;

        private static ConfigEntry<Options_NetworkQueueSize> configNetworkQueueSize;
        private enum Options_NetworkQueueSize {
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

        void Awake() {
            BN_Logger.logger = base.Logger;
            configLogMessages = Config.Bind(
                "Logging",
                "Log Info Messages",
                false,
                "True: Verbose logs.\nFalse: Only log warnings and errors.");

            BN_Patch_UpdateRate.InitConfig(Config);
            BN_Patch_SendRate.InitConfig(Config);

            configNetworkQueueSize = Config.Bind(
                "Networking",
                "Queue Size",
                Options_NetworkQueueSize._100,
                new ConfigDescription(
                    "With low upload speeds, lowering your queue size allows Valheim to better prioritize outgoing data. Listed values are correct as of patch 0.203.11."
                ));

            harmony.PatchAll();

        }

        

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.GetSendQueueSize))]
        class NetworkQueueSize_Patch {
            static void Postfix(ref int __result) {
#if DEBUG
                int originalQueueSize = __result;
#endif

                switch (configNetworkQueueSize.Value) {
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
}
