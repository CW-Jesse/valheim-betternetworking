using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_SendRate {
        public enum Options_NetworkSendRate {
            [Description("400% (600 KB/s | 4.8 Mbit/s)")]
            _400,
            [Description("200% (300 KB/s | 2.4 Mbit/s)")]
            _200,
            [Description("100% (150 KB/s | 1.2 Mbit/s)")]
            _100,
            [Description("50% (75 KB/s | 0.6 Mbit/s)")]
            _50
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkSendRateMin = config.Bind(
                "Networking",
                "Minimum Send Rate",
                Options_NetworkSendRate._100,
                new ConfigDescription(
                    "Steam attempts to estimate your bandwidth. Valheim sets the MINIMUM estimation at 150 KB/s as of patch 0.203.11."
                ));

            BetterNetworking.configNetworkSendRateMax = config.Bind(
                "Networking",
                "Maximum Send Rate",
                Options_NetworkSendRate._100,
                new ConfigDescription(
                    "Steam attempts to estimate your bandwidth. Valheim sets the MAXIMUM estimation at 150 KB/s as of patch 0.203.11."
                ));

            configNetworkSendRateSettings_Listen();
        }

        public static void configNetworkSendRateSettings_Listen() {
            BetterNetworking.configNetworkSendRateMin.SettingChanged += ConfigNetworkSendRateMin_SettingChanged;
            BetterNetworking.configNetworkSendRateMax.SettingChanged += ConfigNetworkSendRateMax_SettingChanged;
            BN_Logger.LogInfo("Started listening for user changes to NetworkSendRates");
        }

        private static void ConfigNetworkSendRateMin_SettingChanged(object sender, EventArgs e) {
            if (BetterNetworking.configNetworkSendRateMin.Value < BetterNetworking.configNetworkSendRateMax.Value) {
                BetterNetworking.configNetworkSendRateMax.Value = BetterNetworking.configNetworkSendRateMin.Value;
                BN_Logger.LogInfo("Maximum network send rate automatically increased");
            }
            NetworkSendRate_Patch.SetSendRateMinFromConfig();
        }
        private static void ConfigNetworkSendRateMax_SettingChanged(object sender, EventArgs e) {
            if (BetterNetworking.configNetworkSendRateMax.Value > BetterNetworking.configNetworkSendRateMin.Value) {
                BetterNetworking.configNetworkSendRateMin.Value = BetterNetworking.configNetworkSendRateMax.Value;
                BN_Logger.LogInfo("Minimum network send rate automatically decreased");
            }
            NetworkSendRate_Patch.SetSendRateMaxFromConfig();
        }
        [HarmonyPatch(typeof(SteamNetworkingUtils))]
        class NetworkSendRate_Patch {
            static private int originalNetworkSendRateMin = 0;
            static private bool originalNetworkSendRateMin_set = false;
            static private int originalNetworkSendRateMax = 0;
            static private bool originalNetworkSendRateMax_set = false;


            public static int sendRateMin {
                get {
                    switch (BetterNetworking.configNetworkSendRateMin.Value) {
                        case Options_NetworkSendRate._400:
                            return originalNetworkSendRateMin * 4;
                        case Options_NetworkSendRate._200:
                            return originalNetworkSendRateMin * 2;
                        case Options_NetworkSendRate._50:
                            return originalNetworkSendRateMin / 2;
                    }
                    return originalNetworkSendRateMin;
                }
            }
            public static int sendRateMax {
                get {
                    switch (BetterNetworking.configNetworkSendRateMax.Value) {
                        case Options_NetworkSendRate._400:
                            return originalNetworkSendRateMax * 4;
                        case Options_NetworkSendRate._200:
                            return originalNetworkSendRateMax * 2;
                        case Options_NetworkSendRate._50:
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
                if (ZNet.instance == null) {
                    BN_Logger.LogWarning("Attempted to set Steam networking config value while disconnected");
                    return;
                }

                GCHandle pinned_SendRate = GCHandle.Alloc(value, GCHandleType.Pinned);

                try {
                    if (BN_Utils.IsDedicated()) {
                        BN_Logger.LogInfo("(dedicated server)");

                        SteamGameServerNetworkingUtils.SetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                            pinned_SendRate.AddrOfPinnedObject()
                            );
                    } else {
                        BN_Logger.LogInfo("(non-dedicated server)");

                        SteamNetworkingUtils.SetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                            pinned_SendRate.AddrOfPinnedObject()
                            );
                    }
                } catch {
                    BN_Logger.LogError("Unable to set networking config; please notify the mod author");
                }

                pinned_SendRate.Free();
            }

            [HarmonyPatch(nameof(SteamNetworkingUtils.SetConfigValue))]
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

                NetworkSendRate_Patch.SetSendRateMinFromConfig();
                NetworkSendRate_Patch.SetSendRateMaxFromConfig();
            }
        }
    }
}
