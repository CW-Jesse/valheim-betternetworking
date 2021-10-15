using BepInEx.Configuration;
using BepInEx.Logging;

namespace CW_Jesse.BetterNetworking {
    class BN_Logger {
        private static ManualLogSource logger;

        public static void Init(ManualLogSource logger, ConfigFile config) {
            BN_Logger.logger = logger;
            BetterNetworking.configLogMessages = config.Bind(
                "Logging",
                "Log Info Messages",
                false,
                "True: Verbose logs.\nFalse: Only log warnings and errors.");
        }

        public static void LogError(object data) {
            BN_Logger.logger.LogError(data);
        }
        public static void LogWarning(object data) {
            BN_Logger.logger.LogWarning(data);
        }
        public static void LogMessage(object data) {
            BN_Logger.logger.LogMessage(data);
        }
        public static void LogInfo(object data) {
            if (BetterNetworking.configLogMessages.Value == true) {
                BN_Logger.logger.LogInfo(data);
            }
        }

    }
}
