using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;

namespace CW_Jesse.BetterNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "2.3.2")]
    [BepInIncompatibility("org.bepinex.plugins.network")]
    [BepInIncompatibility("be.sebastienvercammen.valheim.netcompression")]
    [BepInIncompatibility("com.github.dalayeth.Networkfix")]
    [BepInIncompatibility("Steel.ValheimMod")]
    public class BetterNetworking : BaseUnityPlugin {

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        public static ConfigEntry<BN_Logger.Options_Logger_LogLevel> configLogMessages;
        public static ConfigEntry<BN_Patch_ForceCrossplay.Options_ForceCrossplay> configForceCrossplay;
        public static ConfigEntry<int> configPlayerLimit;
        public static ConfigEntry<BN_Patch_Compression.Options_NetworkCompression> configCompressionEnabled;
        public static ConfigEntry<BN_Patch_UpdateRate.Options_NetworkUpdateRates> configNetworkUpdateRate;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRateMin> configNetworkSendRateMin;
        public static ConfigEntry<BN_Patch_SendRate.Options_NetworkSendRateMax> configNetworkSendRateMax;
        public static ConfigEntry<BN_Patch_QueueSize.Options_NetworkQueueSize> configNetworkQueueSize;

        void Awake() {
            BN_Logger.Init(base.Logger, Config);

            BN_Patch_Compression.InitCompressor();

            BN_Patch_Compression.InitConfig(Config);
            BN_Patch_UpdateRate.InitConfig(Config);
            BN_Patch_SendRate.InitConfig(Config);
            BN_Patch_QueueSize.InitConfig(Config);
            BN_Patch_DedicatedServer.InitConfig(Config);

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

    [HarmonyPatch]
    public class BN_Patch_DedicatedServer {

        private static ConfigFile config;

        public static void InitConfig(ConfigFile configFile) {
            config = configFile;
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.IsDedicated))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> DedicatedServerInit(IEnumerable<CodeInstruction> instructions) {
            bool isDedicated = false;

            foreach (var instruction in instructions) {
                if (instruction.opcode == OpCodes.Ldc_I4_1) {
                    isDedicated = true;

                    BN_Patch_ForceCrossplay.InitConfig(config);
                    BN_Patch_ChangePlayerLimit.InitConfig(config);
                }
            }

            BN_Utils.isDedicated = isDedicated;
            return instructions;
        }
    }

}
