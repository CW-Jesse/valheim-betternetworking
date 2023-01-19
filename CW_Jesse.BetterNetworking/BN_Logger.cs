using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.ComponentModel;

namespace CW_Jesse.BetterNetworking {
    public class BN_Logger {
        private static ManualLogSource logger;
        public enum Options_Logger_LogLevel {
            [Description("Errors/Warnings")]
            warning,
            [Description("Errors/Warnings/Messages <b>[default]</b>")]
            message,
            [Description("Errors/Warnings/Messages/Info")]
            info
        }

        public static void Init(ManualLogSource logger, ConfigFile config) {
            BN_Logger.logger = logger;
            BetterNetworking.configLogMessages = config.Bind(
                "Logging",
                "Log Level",
                Options_Logger_LogLevel.message,
                "Better Network's verbosity in console/logs.");
        }

        public static void LogError(object data) {
            BN_Logger.logger.LogError(data);
        }
        public static void LogWarning(object data) {
            BN_Logger.logger.LogWarning(data);
        }
        public static void LogMessage(object data) {
            if (BetterNetworking.configLogMessages.Value >= Options_Logger_LogLevel.message) {
                BN_Logger.logger.LogMessage(data);
            }
        }
        public static void LogInfo(object data) {
            if (BetterNetworking.configLogMessages.Value >= Options_Logger_LogLevel.info) {
                BN_Logger.logger.LogInfo(data);
            }
        }

    }
}
