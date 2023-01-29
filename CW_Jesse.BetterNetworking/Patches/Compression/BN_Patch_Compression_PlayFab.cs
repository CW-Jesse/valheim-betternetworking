using System.Collections.Generic;

using HarmonyLib;
using PlayFab.Party;

using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public static class BN_Patch_Compression_PlayFab {

        [HarmonyPatch(typeof(ZPlayFabSocket), "InternalSend")]
        [HarmonyPrefix]
        private static bool PlayFab_Send(ref ZPlayFabSocket __instance, ref bool ___m_useCompression, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue, byte[] payload) {
            if (!CompressionStatus.GetSendCompressionStarted(BN_Utils.GetPeer(__instance))) { return true; }

            if ((bool)AccessTools.Method(typeof(ZPlayFabSocket), "PartyResetInProgress").Invoke(__instance, null))
                return false;

            AccessTools.Method(typeof(ZPlayFabSocket), "IncSentBytes").Invoke(__instance, new object[] { payload.Length });
            if ((UnityEngine.Object)ZNet.instance != (UnityEngine.Object)null && ZNet.instance.HaveStopped)
                AccessTools.Method(typeof(ZPlayFabSocket), "InternalSendCont").Invoke(__instance, new object[] { Compress(payload) });
            else
                ((Queue<byte[]>)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "m_outCompress").GetValue(___m_zlibWorkQueue)).Enqueue(Compress(payload));

            return false;
        }

        [HarmonyPatch(typeof(ZPlayFabSocket), "OnDataMessageReceived")]
        [HarmonyPrefix]
        private static bool PlayFab_Receive(
            ref ZPlayFabSocket __instance,
            ref bool ___m_useCompression, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue, ref string ___m_remotePlayerId, ref bool ___m_isClient, ref bool ___m_didRecover,
            object sender, PlayFabPlayer from, byte[] compressedBuffer) {

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetReceiveCompressionStarted(peer)) { return true; }

            if (!(from.EntityKey.Id == ___m_remotePlayerId))
                return false;

            AccessTools.Method(typeof(ZPlayFabSocket), "DelayedInit").Invoke(__instance, null);

            if (!___m_isClient && ___m_didRecover)
                AccessTools.Method(typeof(ZPlayFabSocket), "CheckReestablishConnection").Invoke(__instance, new object[] { compressedBuffer });
            else {

                byte[] decompressedResult;
                try {
                    decompressedResult = Decompress(compressedBuffer);
                } catch {
                    BN_Logger.LogError($"Could not decompress message from {peer} (PlayFab)");
                    return true;
                }

                ((Queue<byte[]>)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "m_outDecompress").GetValue(___m_zlibWorkQueue)).Enqueue(decompressedResult);
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
                BN_Logger.LogError($"Could not decompress message from {peer} (PlayFab ReestablishConnection)");
                return true;
            }
            return false;
        }
    }
}
