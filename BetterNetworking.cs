using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace CW_Jesse.BetterNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "1.1.0")]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    public class BetterNetworking : BaseUnityPlugin {

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        public static ConfigEntry<bool> configLogMessages;
        public static ConfigEntry<BN_Patch_UpdateRate.Options_NetworkUpdateRates> configNetworkUpdateRate;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRate> configNetworkSendRateMin;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRate> configNetworkSendRateMax;
        public static ConfigEntry<BN_Patch_QueueSize.Options_NetworkQueueSize> configNetworkQueueSize;

        void Awake() {

            BN_Logger.Init(base.Logger, Config);
            BN_Patch_UpdateRate.InitConfig(Config);
            BN_Patch_SendRate.InitConfig(Config);
            BN_Patch_QueueSize.InitConfig(Config);

            harmony.PatchAll();
        }

        

        
    }
}
