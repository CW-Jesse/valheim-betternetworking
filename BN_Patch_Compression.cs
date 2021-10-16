using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Steamworks;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public class BN_Patch_Compression {
        public const int VERSION = 0;
        private const string RPC_GET_COMPRESSION_VERSION = "CW_Jesse.BetterNetworking.GetCompressionVersion";
        public static Dictionary<ZNetPeer, bool> peers = new Dictionary<ZNetPeer, bool>();


        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ZNetPeer peer) {
            BN_Logger.LogMessage($"Attempting to notify {peer.m_playerName} of our compression version");

            peer.m_rpc.Register<int>(RPC_GET_COMPRESSION_VERSION, new Action<ZRpc, int>(RPC_GetCompressionVersion));
            peer.m_rpc.Invoke(RPC_GET_COMPRESSION_VERSION, new object[] {
                BN_Patch_Compression.VERSION
            });
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPrefix]
        private static void OnDisconnect(ZNetPeer peer) {
            BN_Patch_Compression.RemovePeer(peer);
        }
        private static void RPC_GetCompressionVersion(ZRpc rpc, int compressionVersion) {
            ZNetPeer peer = BN_Utils.GetPeer(rpc);
            if (peer == null) { return; }

            BN_Logger.LogMessage($"Compression version received from {peer.m_playerName}: {compressionVersion}");

            BN_Patch_Compression.AddPeer(peer, compressionVersion);
        }

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static bool SendQueuedCompressedPackages(ZSteamSocket __instance, Queue<Byte[]> ___m_sendQueue, int ___m_totalSent, HSteamNetConnection ___m_con) {
            BN_Logger.LogMessage("Successfully hijacked data sending");

            if (!__instance.IsConnected()) {
                return false;
            }
            while (___m_sendQueue.Count > 0) {
                byte[] array = ___m_sendQueue.Peek();
                IntPtr intPtr = Marshal.AllocHGlobal(array.Length);
                Marshal.Copy(array, 0, intPtr, array.Length);
                long num;
                EResult eresult;
                if (BN_Utils.IsDedicated()) {
                    eresult = SteamGameServerNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)array.Length, 8, out num);
                } else {
                    eresult = SteamNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)array.Length, 8, out num);
                }
                Marshal.FreeHGlobal(intPtr);
                if (eresult != EResult.k_EResultOK) {
                    ZLog.Log("Failed to send data " + eresult);
                    return false;
                }
                ___m_totalSent += array.Length;
                ___m_sendQueue.Dequeue();
            }

            return false;
         }

        public static void AddPeer(ZNetPeer peer, int compressionVersion) {
            bool peerHasCompression = compressionVersion == VERSION;

            peers.Add(peer, peerHasCompression);

            if (peerHasCompression) {
                BN_Logger.LogMessage($"Compression enabled for {peer.m_playerName}");
            } else {
                if (compressionVersion > VERSION) {
                    BN_Logger.LogError($"Can't use network compression for {peer.m_playerName}: you need to download the latest version of Better Networking");
                } else {
                    BN_Logger.LogWarning($"Can't use network compression for {peer.m_playerName}: they are using an older version of Better Networking");
                }
            }
        }
        public static void RemovePeer(ZNetPeer peer) {
            peers.Remove(peer);
        }

        public static bool PeerHasCompression(ZNetPeer peer) {
            if (peers.ContainsKey(peer)) {
                return peers[peer];
            }
            return false;
        }
    }
}
