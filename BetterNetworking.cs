using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace CW_Jesse.BetterNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "1.2.0")]
    [BepInIncompatibility("Steel.ValheimMod")]
    [BepInIncompatibility("com.github.dalayeth.Networkfix")]
    public class BetterNetworking : BaseUnityPlugin {

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        public static ConfigEntry<bool> configLogMessages;
        public static ConfigEntry<bool> configCompressionEnabled;
        public static ConfigEntry<BN_Patch_UpdateRate.Options_NetworkUpdateRates> configNetworkUpdateRate;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRate> configNetworkSendRateMin;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRate> configNetworkSendRateMax;
        public static ConfigEntry<BN_Patch_QueueSize.Options_NetworkQueueSize> configNetworkQueueSize;

        void Awake() {

            BN_Logger.Init(base.Logger, Config);
            BN_Patch_Compression.InitConfig(Config);
            BN_Patch_UpdateRate.InitConfig(Config);
            BN_Patch_SendRate.InitConfig(Config);
            BN_Patch_QueueSize.InitConfig(Config);

            harmony.PatchAll();
        }

#if DEBUG
        void Start() {
            foreach (string pluginGuid in Chainloader.PluginInfos.Keys) {
                string pluginName = Chainloader.PluginInfos[pluginGuid].Metadata.Name;
                System.Version pluginVersion = Chainloader.PluginInfos[pluginGuid].Metadata.Version;
                BN_Logger.LogMessage($"Detected plugin: {pluginName} ({pluginVersion})");
            }
        }
#endif

        void OnDestroy() {
            harmony.UnpatchSelf();
        }
    }
}
