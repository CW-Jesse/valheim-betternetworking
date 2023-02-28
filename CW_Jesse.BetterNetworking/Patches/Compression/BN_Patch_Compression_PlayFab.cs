using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using PlayFab.Party;

using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public static class BN_Patch_Compression_PlayFab {
        private static Dictionary<PlayFabZLibWorkQueue, ZPlayFabSocket> workQueueSockets = new Dictionary<PlayFabZLibWorkQueue, ZPlayFabSocket>();

        [HarmonyPatch(typeof(ZPlayFabSocket), MethodType.Constructor)]
        [HarmonyPostfix]
        private static void SocketOpen0(ref ZPlayFabSocket __instance, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue) { SocketOpen(ref __instance, ref ___m_zlibWorkQueue); }
        
        [HarmonyPatch(typeof(ZPlayFabSocket), MethodType.Constructor, new Type[] { typeof(string), typeof(Action<PlayFabMatchmakingServerData>) })]
        [HarmonyPostfix]
        private static void SocketOpen1(ref ZPlayFabSocket __instance, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue) { SocketOpen(ref __instance, ref ___m_zlibWorkQueue); }
        
        [HarmonyPatch(typeof(ZPlayFabSocket), MethodType.Constructor, new Type[] { typeof(PlayFabPlayer) })]
        [HarmonyPostfix]
        private static void SocketOpen2(ref ZPlayFabSocket __instance, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue) { SocketOpen(ref __instance, ref ___m_zlibWorkQueue); }

        private static void SocketOpen(ref ZPlayFabSocket __instance, ref PlayFabZLibWorkQueue ___m_zlibWorkQueue) {
            workQueueSockets.Add(___m_zlibWorkQueue, __instance);
            BN_Logger.LogMessage($"PlayFab: Added socket {BN_Utils.GetPeerName(__instance)}");
        }
        
        [HarmonyPatch(typeof(ZPlayFabSocket), nameof(ZPlayFabSocket.Dispose))]
        [HarmonyPostfix]
        private static void SocketClose(ref PlayFabZLibWorkQueue ___m_zlibWorkQueue) {
            workQueueSockets.Remove(___m_zlibWorkQueue);
            BN_Logger.LogMessage($"PlayFab: Removed socket");
        }

        [HarmonyPatch(typeof(PlayFabZLibWorkQueue), "DoCompress")]
        [HarmonyPrefix]
        private static bool PlayFab_Compress(ref PlayFabZLibWorkQueue __instance, ref Queue<byte[]> ___m_inCompress, ref Queue<byte[]> ___m_outCompress) {
            if (!workQueueSockets.TryGetValue(__instance, out ZPlayFabSocket socket)) return true;
            if (!CompressionStatus.GetSendCompressionStarted(socket)) return true;
            
            while (___m_inCompress.Count > 0) {
                try {
                    ___m_outCompress.Enqueue(Compress(___m_inCompress.Dequeue()));
                } catch {
                    BN_Logger.LogError($"PlayFab: Failed BN compress");
                }
            }

            return false;
        }
        
        
        [HarmonyPatch(typeof(PlayFabZLibWorkQueue), "DoUncompress")]
        [HarmonyPrefix]
        private static bool PlayFab_Decompress(ref PlayFabZLibWorkQueue __instance, ref Queue<byte[]> ___m_inDecompress, ref Queue<byte[]> ___m_outDecompress) {
            bool bnCompression = false;
            if (workQueueSockets.TryGetValue(__instance, out ZPlayFabSocket socket)) {
                bnCompression = CompressionStatus.GetReceiveCompressionStarted(socket);
            }
            
            while (___m_inDecompress.Count > 0) {
                byte[] dataToDecompress = ___m_inDecompress.Dequeue();
                try {
                    ___m_outDecompress.Enqueue(Decompress(dataToDecompress));
                    if (!CompressionStatus.GetReceiveCompressionStarted(socket)) {
                        BN_Logger.LogMessage($"PlayFab: Received unexpected compressed message from {BN_Utils.GetPeerName(socket)}");
                        CompressionStatus.SetReceiveCompressionStarted(socket, true);
                    }
                } catch {
                    if (bnCompression) BN_Logger.LogInfo($"PlayFab: Failed BN decompress");
                    try {
                        ___m_outDecompress.Enqueue((byte[])AccessTools.Method(typeof(PlayFabZLibWorkQueue), "UncompressOnThisThread").Invoke(__instance, new object[] { dataToDecompress }));
                        if (CompressionStatus.GetReceiveCompressionStarted(socket)) {
                            BN_Logger.LogMessage($"PlayFab: Received unexpected vanilla message from {BN_Utils.GetPeerName(socket)}");
                            CompressionStatus.SetReceiveCompressionStarted(socket, false);
                        }
                    } catch {
                        BN_Logger.LogMessage($"PlayFab: Failed vanilla decompress; keeping data (this data would have been lost without Better Networking)");
                        ___m_outDecompress.Enqueue(dataToDecompress);
                    }
                }
            }

            return false;
        }
        
        public static void FlushQueue(ISocket socket) {
            
            // get parts needed to execute queue
            PlayFabZLibWorkQueue zlibWorkQueue = (PlayFabZLibWorkQueue)AccessTools.Field(typeof(ZPlayFabSocket), "m_zlibWorkQueue").GetValue(socket);
            // SemaphoreSlim workSemaphore = (SemaphoreSlim)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "s_workSemaphore").GetValue(zlibWorkQueue);
            Mutex workersMutex = (Mutex)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "s_workersMutex").GetValue(zlibWorkQueue);
            List<PlayFabZLibWorkQueue> workers = (List<PlayFabZLibWorkQueue>)AccessTools.Field(typeof(PlayFabZLibWorkQueue), "s_workers").GetValue(zlibWorkQueue);
            MethodInfo execute = AccessTools.Method(typeof(PlayFabZLibWorkQueue), "Execute");
            
            // compress/decompress messages
            workersMutex.WaitOne();
            foreach (PlayFabZLibWorkQueue worker in workers) {
                execute.Invoke(worker, new object[] { });
            }
            workersMutex.ReleaseMutex();
            
            // send/receive messages
            AccessTools.Method(typeof(ZPlayFabSocket), "LateUpdate").Invoke(socket, null);
        }
    }
}
