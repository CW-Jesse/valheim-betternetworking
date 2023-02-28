using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public static class BN_Patch_Compression_Steamworks {

        private const int k_nSteamNetworkingSend_Reliable = 8;                       // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int k_cbMaxSteamNetworkingSocketsMessageSizeSend = 512 * 1024; // https://partner.steamgames.com/doc/api/steamnetworkingtypes

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static bool Steamworks_SendCompressedPackages(ref ZSteamSocket __instance, ref Queue<Byte[]> ___m_sendQueue) {
            if (!__instance.IsConnected()) return false;
            if (!CompressionStatus.GetSendCompressionStarted(__instance)) return true;

            ___m_sendQueue = new Queue<byte[]>(___m_sendQueue.Select(p => Compress(p)));
            return true;
         }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPostfix]
        private static void Steamworks_ReceiveCompressedPackages(ref ZPackage __result, ref ZSteamSocket __instance) {
            if (!__instance.IsConnected()) return;

            if (__result == null) return;

            byte[] decompressedResult;
            try {
                decompressedResult = Decompress(__result.GetArray());
                __result = new ZPackage(decompressedResult);
                if (!CompressionStatus.GetReceiveCompressionStarted(__instance)) {
                    BN_Logger.LogWarning($"Compression (Steamworks): Received unexpected compressed message from {BN_Utils.GetPeerName(__instance)}");
                    // CompressionStatus.SetReceiveCompressionStarted(__instance, true);
                }
            } catch {
                if (CompressionStatus.GetReceiveCompressionStarted(__instance)) {
                    BN_Logger.LogWarning($"Compression (Steamworks): Could not decompress message from {BN_Utils.GetPeerName(__instance)}");
                    // CompressionStatus.SetReceiveCompressionStarted(__instance, false);
                }
            }
        }
    }
}
