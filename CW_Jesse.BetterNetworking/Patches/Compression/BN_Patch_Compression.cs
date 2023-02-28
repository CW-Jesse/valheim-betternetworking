using System;
using System.ComponentModel;
using System.IO;

using HarmonyLib;
using BepInEx.Configuration;
using System.Reflection;

using ZstdSharp;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public partial class BN_Patch_Compression {

        private static string ZSTD_DICT_RESOURCE_NAME = "CW_Jesse.BetterNetworking.dict.small";
        private static int ZSTD_LEVEL = 1;
        private static Compressor compressor;
        private static Decompressor decompressor;

        public enum Options_NetworkCompression {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        public static void InitCompressor() {
            byte[] compressionDict;
            using (Stream dictStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ZSTD_DICT_RESOURCE_NAME)) {
                compressionDict = new byte[dictStream.Length];
                dictStream.Read(compressionDict, 0, (int)dictStream.Length);
            }

            compressor = new Compressor(ZSTD_LEVEL);
            compressor.LoadDictionary(compressionDict);
            decompressor = new Decompressor();
            decompressor.LoadDictionary(compressionDict);
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configCompressionEnabled = config.Bind(
                "Networking",
                "Compression Enabled",
                Options_NetworkCompression.@true,
                new ConfigDescription("Keep this enabled unless comparing difference.\n" +
                "---\n" +
                "Crossplay enabled: Increases speed and strength of network compression.\nCrossplay disabled: Adds network compression."));

            BetterNetworking.configCompressionEnabled.SettingChanged += ConfigCompressionEnabled_SettingChanged;
        }
        private static void ConfigCompressionEnabled_SettingChanged(object sender, EventArgs e) {
            SetCompressionEnabledFromConfig();
        }

        private static void SetCompressionEnabledFromConfig() {
            bool newCompressionStatus;

            if (BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true) {
                newCompressionStatus = true;
                BN_Logger.LogMessage($"Compression: Enabling");
            } else {
                newCompressionStatus = false;
                BN_Logger.LogMessage($"Compression: Disabling");
            }

            CompressionStatus.ourStatus.compressionEnabled = newCompressionStatus;
            SendCompressionEnabledStatus();
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ref ZNetPeer peer) {
            CompressionStatus.AddSocket(peer.m_socket);

            RegisterRPCs(peer);
        }


        [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        [HarmonyPostfix]
        private static void OnPeerInfoReceived(ref ZRpc rpc) {
            SendCompressionVersion(rpc);
        }
        

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPostfix]
        private static void OnDisconnect(ZNetPeer peer) {
            CompressionStatus.RemoveSocket(peer.m_socket);
            BN_Logger.LogMessage($"Compression: {BN_Utils.GetPeerName(peer.m_socket)} disconnected");
        }

        internal static byte[] Compress(byte[] buffer) {
            byte[] compressedBuffer = compressor.Wrap(buffer).ToArray();
            if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info && buffer.Length > 256) { // small messages don't compress well but they also don't matter
                float compressedSizePercentage = ((float)compressedBuffer.Length / (float)buffer.Length) * 100;
                BN_Logger.LogInfo($"Compression: Sent {buffer.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
            }
            return compressedBuffer;
        }
        internal static byte[] Decompress(byte[] compressedBuffer) {
            byte[] buffer = decompressor.Unwrap(compressedBuffer).ToArray();
            if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info && buffer.Length > 256) { // small messages don't compress well but they also don't matter
                float compressedSizePercentage = ((float)compressedBuffer.Length / (float)buffer.Length) * 100;
                BN_Logger.LogInfo($"Compression: Received {buffer.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
            }
            return buffer;
        }
    }
}
