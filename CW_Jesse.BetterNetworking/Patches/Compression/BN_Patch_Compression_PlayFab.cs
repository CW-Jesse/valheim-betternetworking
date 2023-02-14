using System.Collections.Generic;

using HarmonyLib;
using PlayFab.Party;

using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public static class BN_Patch_Compression_PlayFab {

        [HarmonyPatch(typeof(ZPlayFabSocket), "InternalSend")]
        [HarmonyPrefix]
        private static bool PlayFab_InternalSend(ref ZPlayFabSocket __instance, ref bool ___m_useCompression, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue, byte[] payload) {
            if (!CompressionStatus.GetSendCompressionStarted(BN_Utils.GetPeer(__instance))) { return true; }


            if ((bool)AccessTools.Method(typeof(ZPlayFabSocket), "PartyResetInProgress").Invoke(__instance, null))
                return false;

            AccessTools.Method(typeof(ZPlayFabSocket), "IncSentBytes").Invoke(__instance, new object[] { payload.Length });
            if (ZNet.instance != null) {
                AccessTools.Method(typeof(ZPlayFabSocket), "LateUpdate").Invoke(__instance, null); // process queued (non-BN) messages, if any
                AccessTools.Method(typeof(ZPlayFabSocket), "InternalSendCont").Invoke(__instance, new object[] { Compress(payload) });
            } else {
                ((Queue<byte[]>)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "m_outCompress").GetValue(___m_zlibWorkQueue)).Enqueue(Compress(payload));
            }

            return false;
        }

        [HarmonyPatch(typeof(ZPlayFabSocket), "OnDataMessageReceived")]
        [HarmonyPrefix]
        private static bool PlayFab_OnDataMessageReceived(
            ref ZPlayFabSocket __instance,
            ref bool ___m_useCompression, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue, ref string ___m_remotePlayerId, ref bool ___m_isClient, ref bool ___m_didRecover,
            object sender, PlayFabPlayer from, byte[] compressedBuffer) {

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            byte[] decompressedResult;

            try {
                decompressedResult = Decompress(compressedBuffer);
                if (!CompressionStatus.GetReceiveCompressionStarted(peer)) {
                    BN_Logger.LogWarning($"Compression (PlayFab): Received unexpected compressed message from {BN_Utils.GetPeerName(peer)}; assuming compression started");
                    CompressionStatus.SetReceiveCompressionStarted(peer, true);
                }
            } catch {
                if (CompressionStatus.GetReceiveCompressionStarted(peer)) {
                    BN_Logger.LogWarning($"Compression (PlayFab): Received unexpected decompressed message from {BN_Utils.GetPeerName(peer)}; assuming compression stopped");
                    CompressionStatus.SetReceiveCompressionStarted(peer, false);
                }
                return true;
            }

            

            if (!(from.EntityKey.Id == ___m_remotePlayerId))
                return false;

            AccessTools.Method(typeof(ZPlayFabSocket), "DelayedInit").Invoke(__instance, null);

            AccessTools.Method(typeof(ZPlayFabSocket), "LateUpdate").Invoke(__instance, null); // process queued (non-BN) messages, if any
            if (!___m_isClient && ___m_didRecover)
                AccessTools.Method(typeof(ZPlayFabSocket), "CheckReestablishConnection").Invoke(__instance, new object[] { compressedBuffer });
            else {
                AccessTools.Method(typeof(ZPlayFabSocket), "OnDataMessageReceivedCont").Invoke(__instance, new object[] { decompressedResult });
            }

            return false;
        }

        [HarmonyPatch(typeof(ZPlayFabSocket), "CheckReestablishConnection")]
        [HarmonyPrefix]
        private static bool PlayFab_CheckReestablishConnection(ref ZPlayFabSocket __instance, byte[] maybeCompressedBuffer) {
            ZNetPeer peer = BN_Utils.GetPeer(__instance);

            try {
                AccessTools.Method(typeof(ZPlayFabSocket), "OnDataMessageReceivedCont").Invoke(__instance, new object[] { Decompress(maybeCompressedBuffer) });
            } catch {
                if (CompressionStatus.GetReceiveCompressionStarted(peer)) {
                    BN_Logger.LogWarning($"Compression (PlayFab): Could not decompress message from {BN_Utils.GetPeerName(peer)}; did they lose internet or Alt+F4?");
                }
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(ZPlayFabSocket), "ResetAll")]
        [HarmonyPostfix]
        private static void PlayFab_ResetAll(ref ZPlayFabSocket __instance) {
            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (peer == null) return; // ResetAll is called even when connection is closing, which means null peer

            if (CompressionStatus.IsPeerExist(peer)) { // only reset peer info we already have peer info
                CompressionStatus.RemovePeer(peer);
                CompressionStatus.AddPeer(peer);
                BN_Logger.LogMessage($"Compression (PlayFab): reset connection with {BN_Utils.GetPeerName(peer)}");
                SendCompressionVersion(peer);
            }
        }
    }
}
