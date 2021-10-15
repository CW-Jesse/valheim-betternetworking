using BepInEx.Logging;

namespace CW_Jesse.BetterNetworking {
    class BN_Logger {
        public static ManualLogSource logger;

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
