using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Steamworks;

using K4os.Compression.LZ4;
using System.Reflection;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System;
using BepInEx.Logging;

namespace CW_Jesse.BetterNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "0.3.1")]
    [BepInProcess("valheim.exe")]
    public class BetterNetworking : BaseUnityPlugin {

        public static ConfigEntry<bool> configLogMessages;

        private static ConfigEntry<Options_NetworkUpdateRates> configNetworkUpdateRate;
        private enum Options_NetworkUpdateRates {
            [Description("100% (20 updates/second)")]
            _100,
            [Description("80% (16 updates/second)")]
            _80,
            [Description("60% (12 updates/second)")]
            _60,
            [Description("40% (8 updates/second)")]
            _40,
            [Description("20% (4 updates/second)")]
            _20
        }

        private static ConfigEntry<Options_NetworkSendRateMin> configNetworkSendRateMin;
        private enum Options_NetworkSendRateMin {
            [Description("100% (128 KB/s | 1 Mbit/s))")]
            _100,
            [Description("50% (64 KB/s | 0.5 Mbit/s))")]
            _50
        }

        private static ConfigEntry<Options_NetworkSendRateMax> configNetworkSendRateMax;
        private enum Options_NetworkSendRateMax {
            [Description("400% (512 KB/s | 4 Mbit/s)")]
            _400,
            [Description("200% (256 KB/s | 2 Mbit/s)")]
            _200,
            [Description("100% (128 KB/s | 1 Mbit/s)")]
            _100,
            [Description("50% (64 KB/s | 0.5 Mbit/s)")]
            _50
        }

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        void Awake() {
            BN_Logger.logger = base.Logger;

            configLogMessages = Config.Bind(
                "Logging",
                "Log Info Messages",
                false,
                "True: Verbose logs.\nFalse: Only log warnings and errors."
                );

            configNetworkUpdateRate = Config.Bind(
                "Networking",
                "Update Rate",
                Options_NetworkUpdateRates._100,
                new ConfigDescription(
                    "You can reduce network strain by reducing the number of updates your computer sends out. Displayed values are correct as of patch 0.203.11."
                )
            );

            configNetworkSendRateMin = Config.Bind(
                "Networking",
                "Minimum Send Rate",
                Options_NetworkSendRateMin._100,
                new ConfigDescription(
                    "Steam attempts to estimate your bandwidth. Valheim sets the MINIMUM estimation at 128 KB/s as of patch 0.203.11."
                )
            );

            configNetworkSendRateMax = Config.Bind(
                "Networking",
                "Maximum Send Rate",
                Options_NetworkSendRateMax._100,
                new ConfigDescription(
                    "Steam attempts to estimate your bandwidth. Valheim sets the MAXIMUM estimation at 128 KB/s as of patch 0.203.11."
                )
            );

            harmony.PatchAll();

            configNetworkSendRateSettings_Listen();
            }

        private static bool configNetworkSendRates_listening = false;
        public static void configNetworkSendRateSettings_Listen() {
            if (!configNetworkSendRates_listening) {
                configNetworkSendRates_listening = true;
                configNetworkSendRateMin.SettingChanged += ConfigNetworkSendRateMin_SettingChanged;
                configNetworkSendRateMax.SettingChanged += ConfigNetworkSendRateMax_SettingChanged;
                BN_Logger.LogInfo("Started listening for user changes to NetworkSendRates");
            }
        }
        public static void configNetworkSendRateSettings_Unlisten() {
            if (configNetworkSendRates_listening) {
                configNetworkSendRates_listening = false;
                configNetworkSendRateMin.SettingChanged -= ConfigNetworkSendRateMin_SettingChanged;
                configNetworkSendRateMax.SettingChanged -= ConfigNetworkSendRateMax_SettingChanged;
                BN_Logger.LogInfo("Stopped listening for user changes to NetworkSendRates");
            }
        }

        private static void ConfigNetworkSendRateMin_SettingChanged(object sender, EventArgs e) {
            if (configNetworkSendRateMin.Value == Options_NetworkSendRateMin._100 &&
                configNetworkSendRateMax.Value == Options_NetworkSendRateMax._50) {
                configNetworkSendRateMax.Value = Options_NetworkSendRateMax._100;
            }
            configNetworkSendRateSettings_Unlisten();
            NetworkSendRate_Patch.SetSendRateMinFromConfig();
            configNetworkSendRateSettings_Listen();
        }
        private static void ConfigNetworkSendRateMax_SettingChanged(object sender, EventArgs e) {
            if (configNetworkSendRateMax.Value == Options_NetworkSendRateMax._50) {
                configNetworkSendRateMin.Value = Options_NetworkSendRateMin._50;
            }
            configNetworkSendRateSettings_Unlisten();
            NetworkSendRate_Patch.SetSendRateMaxFromConfig();
            configNetworkSendRateSettings_Listen();
        }

        [HarmonyPatch(typeof(ZDOMan), "SendZDOToPeers")]
        class NetworkUpdateFrequency_Patch {

            static void Prefix(ref float dt) {
                float networkUpdateRate = 1.0f;
                switch (configNetworkUpdateRate.Value) {
                    case Options_NetworkUpdateRates._80:
                        networkUpdateRate = 0.8f;
                        break;
                    case Options_NetworkUpdateRates._60:
                        networkUpdateRate = 0.6f;
                        break;
                    case Options_NetworkUpdateRates._40:
                        networkUpdateRate = 0.4f;
                        break;
                    case Options_NetworkUpdateRates._20:
                        networkUpdateRate = 0.2f;
                        break;
                }
                dt *= networkUpdateRate;
            }
        }

        [HarmonyPatch(typeof(SteamNetworkingUtils), nameof(SteamNetworkingUtils.SetConfigValue))]
        class NetworkSendRate_Patch {
            static private int originalNetworkSendRateMin = 0;
            static private bool originalNetworkSendRateMin_set = false;
            static private int originalNetworkSendRateMax = 0;
            static private bool originalNetworkSendRateMax_set = false;


            public static int sendRateMin {
                get {
                    switch (configNetworkSendRateMin.Value) {
                        case Options_NetworkSendRateMin._50:
                            return originalNetworkSendRateMin / 2;
                    }
                    return originalNetworkSendRateMin;
                }
            }
            public static int sendRateMax {
                get {
                    switch (configNetworkSendRateMax.Value) {
                        case Options_NetworkSendRateMax._400:
                            return originalNetworkSendRateMax * 4;
                        case Options_NetworkSendRateMax._200:
                            return originalNetworkSendRateMax * 2;
                        case Options_NetworkSendRateMax._50:
                            return originalNetworkSendRateMax / 2;
                    }
                    return originalNetworkSendRateMax;
                }
            }

            public static void SetSendRateMinFromConfig() {
                if (!originalNetworkSendRateMin_set) {
                    BN_Logger.LogWarning("Attempted to set NetworkSendRateMin before Valheim did");
                    return;
                }

                BN_Logger.LogMessage($"Setting NetworkSendRateMin to {sendRateMin}");
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, sendRateMin);
            }
            public static void SetSendRateMaxFromConfig() {
                if (!originalNetworkSendRateMax_set) {
                    BN_Logger.LogWarning("Attempted to set NetworkSendRateMax before Valheim did");
                    return;
                }

                BN_Logger.LogMessage($"Setting NetworkSendRateMax to {sendRateMax}");
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, sendRateMax);
            }

            private static void SetSteamNetworkConfig(ESteamNetworkingConfigValue valueType, int value) {
                GCHandle pinned_SendRateMax = GCHandle.Alloc(value, GCHandleType.Pinned);
                SteamNetworkingUtils.SetConfigValue(
                    valueType,
                    ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                    IntPtr.Zero,
                    ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                    pinned_SendRateMax.AddrOfPinnedObject()
                    );
                pinned_SendRateMax.Free();
            }

            static void Prefix(
                ESteamNetworkingConfigValue eValue,
                ESteamNetworkingConfigScope eScopeType,
                IntPtr scopeObj,
                ESteamNetworkingConfigDataType eDataType,
                ref IntPtr pArg) {

                if (eScopeType == ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global &&
                    scopeObj == IntPtr.Zero &&
                    eDataType == ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32) {

                    switch (eValue) {
                        case ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin:
                            if (!originalNetworkSendRateMin_set) {
                                originalNetworkSendRateMin_set = true;
                                originalNetworkSendRateMin = Marshal.ReadInt32(pArg);

                                BN_Logger.LogMessage($"Valheim's default NetworkSendRateMin is {originalNetworkSendRateMin}");
                            }
                            break;
                        case ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax:
                            if (!originalNetworkSendRateMax_set) {
                                originalNetworkSendRateMax_set = true;
                                originalNetworkSendRateMax = Marshal.ReadInt32(pArg);
                                BN_Logger.LogMessage($"Valheim's default NetworkSendRateMax is {originalNetworkSendRateMin}");
                            }
                            break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ZSteamSocket), "RegisterGlobalCallbacks")]
        class PreventValheimControlOfNetworkRate_Patch {

            static void Postfix() {
                BN_Logger.LogInfo("Network settings overwritten by Valheim; setting them to Better Networking values");
                BetterNetworking.configNetworkSendRateSettings_Unlisten();

                NetworkSendRate_Patch.SetSendRateMinFromConfig();
                NetworkSendRate_Patch.SetSendRateMaxFromConfig();

                BetterNetworking.configNetworkSendRateSettings_Listen();
            }
        }

    }
}
