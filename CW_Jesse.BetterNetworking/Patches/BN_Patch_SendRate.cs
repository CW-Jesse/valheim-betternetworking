using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_SendRate {
        // k_cbMaxSteamNetworkingSocketsMessageSizeSend is limited to 512 * 1024
        // setting the send buffer size above that, which is also its default value, does nothing
        // https://github.com/ValveSoftware/GameNetworkingSockets/blob/dbe5a29a94badf5bc1d5d0d9c28383880fc3c59b/include/steam/steamnetworkingtypes.h#L823
        private const int DEFAULT_SEND_BUFFER_SIZE = 524288; // 524288 is the Steam default and Valheim does not currently change it
        private const int SEND_BUFFER_SIZE = DEFAULT_SEND_BUFFER_SIZE; // setting this higher causes a crash


        public enum Options_NetworkSendRateMin {
            [Description("1024 KB/s | 8 Mbit/s")]
            _1024KB,
            [Description("768 KB/s | 6 Mbit/s")]
            _768KB,
            [Description("512 KB/s | 4 Mbit/s")]
            _512KB,
            [Description("256 KB/s | 2 Mbit/s <b>[default]</b>")]
            _256KB,
            [Description("150 KB/s | 1.2 Mbit/s [Valheim default]")]
            _150KB
        }
        public enum Options_NetworkSendRateMax {
            [Description("1024 KB/s | 8 Mbit/s <b>[default]</b>")]
            _1024KB,
            [Description("768 KB/s | 6 Mbit/s")]
            _768KB,
            [Description("512 KB/s | 4 Mbit/s")]
            _512KB,
            [Description("256 KB/s | 2 Mbit/s")]
            _256KB,
            [Description("150 KB/s | 1.2 Mbit/s [Valheim default]")]
            _150KB
        }

        public static void InitConfig(ConfigFile config) {

            BetterNetworking.configNetworkSendRateMin = config.Bind(
                "Networking (Steamworks)",
                "Minimum Send Rate",
                Options_NetworkSendRateMin._256KB,
                new ConfigDescription(
                    "Steamworks: The minimum speed Steam will <i>attempt</i> to send data.\n" +
                    "<b>Lower this below your internet upload speed.</b>\n"
                ));
            BetterNetworking.configNetworkSendRateMax = config.Bind(
                "Networking (Steamworks)",
                "Maximum Send Rate",
                Options_NetworkSendRateMax._1024KB,
                new ConfigDescription(
                    "Steamworks: The maximum speed Steam will <i>attempt</i> to send data.\n" +
                    "If you have a low upload speed, lower this <i>below</i> your internet upload speed.\n"
                ));

            ConfigNetworkSendRateSettings_Listen();
        }

        public static void ConfigNetworkSendRateSettings_Listen() {
            BetterNetworking.configNetworkSendRateMin.SettingChanged += ConfigNetworkSendRateMin_SettingChanged;
            BetterNetworking.configNetworkSendRateMax.SettingChanged += ConfigNetworkSendRateMax_SettingChanged;
        }

        private static void ConfigNetworkSendRateMin_SettingChanged(object sender, EventArgs e) {
            if ((int)BetterNetworking.configNetworkSendRateMin.Value+1 < (int)BetterNetworking.configNetworkSendRateMax.Value) {
                BetterNetworking.configNetworkSendRateMax.Value = (Options_NetworkSendRateMax)(BetterNetworking.configNetworkSendRateMin.Value+1);
            }
            NetworkSendRate_Patch.SetSendRateMinFromConfig();
        }
        private static void ConfigNetworkSendRateMax_SettingChanged(object sender, EventArgs e) {
            if ((int)BetterNetworking.configNetworkSendRateMax.Value > (int)BetterNetworking.configNetworkSendRateMin.Value+1) {
                BetterNetworking.configNetworkSendRateMin.Value = (Options_NetworkSendRateMin)(BetterNetworking.configNetworkSendRateMax.Value-1);
            }
            NetworkSendRate_Patch.SetSendRateMaxFromConfig();
        }

        [HarmonyPatch(typeof(SteamNetworkingUtils))]
        [HarmonyPatch(typeof(SteamGameServerNetworkingUtils))]
        class NetworkSendRate_Patch {

            public static int SendRateMin {
                get {
                    switch (BetterNetworking.configNetworkSendRateMin.Value) {
                        case Options_NetworkSendRateMin._1024KB:
                            return 1024 * 1024;
                        case Options_NetworkSendRateMin._768KB:
                            return 768 * 1024;
                        case Options_NetworkSendRateMin._512KB:
                            return 512 * 1024;
                        case Options_NetworkSendRateMin._256KB:
                            return 256 * 1024;
                        default:
                            return 150 * 1024;
                    }
                }
            }
            public static int SendRateMax {
                get {
                    switch (BetterNetworking.configNetworkSendRateMax.Value) {
                        case Options_NetworkSendRateMax._1024KB:
                            return 1024 * 1024;
                        case Options_NetworkSendRateMax._768KB:
                            return 768 * 1024;
                        case Options_NetworkSendRateMax._512KB:
                            return 512 * 1024;
                        case Options_NetworkSendRateMax._256KB:
                            return 256 * 1024;
                        default:
                            return 150 * 1024;
                    }
                }
            }

            public static void SetSendRateMinFromConfig() {
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMin, SendRateMin);
            }
            public static void SetSendRateMaxFromConfig() {
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendRateMax, SendRateMax);
            }
            public static void SetSendBufferSize() {
                SetSteamNetworkConfig(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_SendBufferSize, SEND_BUFFER_SIZE);
            }
            private static Int32 GetSteamNetworkConfig(ESteamNetworkingConfigValue valueType) {
                if (ZNet.instance == null) {
                    BN_Logger.LogInfo($"Steamworks: Unable to get net config while disconnected: {valueType}");
                    return -1;
                }
                Int32 val;

                ulong valSize = 4;
                byte[] valBuffer = new byte[valSize];
                ESteamNetworkingConfigDataType dataType;

                GCHandle pinnedVal = GCHandle.Alloc(valBuffer, GCHandleType.Pinned);
                try {
                    if (BN_Utils.isDedicated) {
                        SteamGameServerNetworkingUtils.GetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            out dataType,
                            pinnedVal.AddrOfPinnedObject(),
                            out valSize
                        );
                    } else {
                        SteamNetworkingUtils.GetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            out dataType,
                            pinnedVal.AddrOfPinnedObject(),
                            out valSize
                        );
                    }
                } catch {
                    BN_Logger.LogError($"Steamworks: Unable to get net config: {valueType}");
                }
                pinnedVal.Free();

                val = BitConverter.ToInt32(valBuffer, 0);
                return val;
            }

            private static void SetSteamNetworkConfig(ESteamNetworkingConfigValue valueType, Int32 value) {
                if (ZNet.instance == null) {
                    BN_Logger.LogInfo($"Steamworks: Unable to set net config while disconnected: {valueType}");
                    return;
                }

                int oldVal = GetSteamNetworkConfig(valueType);

                GCHandle pinned_SendRate = GCHandle.Alloc(value, GCHandleType.Pinned);
                try {
                    if (BN_Utils.isDedicated) {
                        SteamGameServerNetworkingUtils.SetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                            pinned_SendRate.AddrOfPinnedObject()
                            );
                    } else {
                        SteamNetworkingUtils.SetConfigValue(
                            valueType,
                            ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                            IntPtr.Zero,
                            ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                            pinned_SendRate.AddrOfPinnedObject()
                            );
                    }
                } catch {
                    BN_Logger.LogError($"Steamworks: Unable to set net config: {valueType}");
                }
                pinned_SendRate.Free();

                BN_Logger.LogMessage($"Steamworks: {valueType}: {oldVal} -> {GetSteamNetworkConfig(valueType)} (attempted {value})");
            }


            [HarmonyPatch("SetConfigValue")]
            static void Prefix(
                ESteamNetworkingConfigValue eValue,
                ESteamNetworkingConfigScope eScopeType,
                IntPtr scopeObj,
                ESteamNetworkingConfigDataType eDataType,
                ref IntPtr pArg) {

                BN_Logger.LogInfo($"Steamworks: {eValue}: {GetSteamNetworkConfig(eValue)} -> {Marshal.ReadInt32(pArg)}");
            }
        }

        [HarmonyPatch(typeof(ZSteamSocket), "RegisterGlobalCallbacks")]
        class PreventValheimControlOfNetworkRate_Patch {

            static void Postfix() {
                NetworkSendRate_Patch.SetSendRateMinFromConfig();
                NetworkSendRate_Patch.SetSendRateMaxFromConfig();
                //NetworkSendRate_Patch.SetSendBufferSize();
            }
        }
    }
}
