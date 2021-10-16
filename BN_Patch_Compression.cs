using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Steamworks;
using K4os.Compression.LZ4;
using System.IO;

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
        private static bool SendCompressedPackages(ZSteamSocket __instance, Queue<Byte[]> ___m_sendQueue, int ___m_totalSent, HSteamNetConnection ___m_con) {
            if (!__instance.IsConnected()) {
                return false;
            }
            while (___m_sendQueue.Count > 0) {

                MemoryStream compressedPackagesStream = new MemoryStream();
                BinaryWriter compressedPackagesWriter = new BinaryWriter(compressedPackagesStream);

                int packageCount = ___m_sendQueue.Count;

                compressedPackagesWriter.Write(___m_sendQueue.Count); // number of packages

                int uncompressedDataLength = 0;

                foreach (byte[] packageByteArray in ___m_sendQueue) {
                    compressedPackagesWriter.Write(packageByteArray.Length); // length of package
                    uncompressedDataLength += packageByteArray.Length;
                    compressedPackagesWriter.Write(packageByteArray); // package
                }

                byte[] compressedPackages = LZ4Pickler.Pickle(compressedPackagesStream.ToArray());

                IntPtr intPtr = Marshal.AllocHGlobal(compressedPackages.Length);
                Marshal.Copy(compressedPackages, 0, intPtr, compressedPackages.Length);
                long messagesSent;
                EResult eresult;
                if (BN_Utils.IsDedicated()) {
                    eresult = SteamGameServerNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedPackages.Length, 8, out messagesSent);
                } else {
                    eresult = SteamNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedPackages.Length, 8, out messagesSent);
                }
                Marshal.FreeHGlobal(intPtr);
                if (eresult != EResult.k_EResultOK) {
                    BN_Logger.LogError("Failed to send data: " + eresult);
                    return false;
                }
                ___m_totalSent += compressedPackages.Length;
                for (int i = 0; i < packageCount; i++) {
                    ___m_sendQueue.Dequeue(); // TODO: inefficient
                }

                BN_Logger.LogMessage($"Compressed data sent: {uncompressedDataLength} bytes compressed into {compressedPackages.Length} bytes");
            }

            return false;
         }

        private static Queue<ZPackage> packages = new Queue<ZPackage>();

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPrefix]
        private static bool ReceiveCompressedPackages(ref ZPackage __result, ZSteamSocket __instance, HSteamNetConnection ___m_con, int ___m_totalRecv, bool ___m_gotData) {
            ZPackage package;
            
            if (packages.Count > 0) {
                package = packages.Dequeue();
                ___m_totalRecv += package.Size();
                ___m_gotData = true;

                __result = package;
                return false;
            }
            
            if (!__instance.IsConnected()) {
                __result = null;
                return false;
            }
            IntPtr[] array = new IntPtr[1];

            bool receivedMessages = false;
            if (BN_Utils.IsDedicated()) {
                receivedMessages = SteamGameServerNetworkingSockets.ReceiveMessagesOnConnection(___m_con, array, 1) == 1;
            } else {
                receivedMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(___m_con, array, 1) == 1;
            }

            if (receivedMessages) {
                SteamNetworkingMessage_t steamNetworkingMessage_t = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[0]);


                byte[] compressedPackages = new byte[steamNetworkingMessage_t.m_cbSize];
                Marshal.Copy(steamNetworkingMessage_t.m_pData, compressedPackages, 0, steamNetworkingMessage_t.m_cbSize);

                byte[] uncompressedPackages = LZ4Pickler.Unpickle(compressedPackages);

                BN_Logger.LogMessage($"Compressed data received: {steamNetworkingMessage_t.m_cbSize} decompressed into {uncompressedPackages.Length}");

                steamNetworkingMessage_t.m_pfnRelease = array[0];
                steamNetworkingMessage_t.Release();

                MemoryStream uncompressedPackagesStream = new MemoryStream(uncompressedPackages);
                BinaryReader uncompressedPackagesReader = new BinaryReader(uncompressedPackagesStream);

                int packageCount = uncompressedPackagesReader.ReadInt32();
                for (int i = 0; i < packageCount; i++){
                    int packageLength = uncompressedPackagesReader.ReadInt32();
                    byte[] packageByteArray = uncompressedPackagesReader.ReadBytes(packageLength);
                    packages.Enqueue(new ZPackage(packageByteArray));
                }

                package = packages.Dequeue();
                ___m_totalRecv += package.Size();
                ___m_gotData = true;

                __result = package;
                return false;
            }
            __result = null;
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
