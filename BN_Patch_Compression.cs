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
        public const int COMPRESSION_VERSION = 1;
        private const string RPC_GET_COMPRESSION_VERSION = "CW_Jesse.BetterNetworking.GetCompressionVersion";

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ZNetPeer peer) {
            peer.m_rpc.Register<int>(RPC_GET_COMPRESSION_VERSION, new Action<ZRpc, int>(RPC_GetCompressionVersion));

            BN_Logger.LogMessage($"Sending {peer.m_socket.GetHostName()} our compression version ({COMPRESSION_VERSION})");

            peer.m_rpc.Invoke(RPC_GET_COMPRESSION_VERSION, new object[] {
                    COMPRESSION_VERSION
                });
            BN_Logger.LogMessage($"Compression version sent to {peer}: {COMPRESSION_VERSION}");

            PeerCompressionVersionSent(peer.m_socket);
        }


        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPrefix]
        private static void OnDisconnect(ref ZNetPeer peer) {
            RemovePeer(peer.m_socket);
        }
        private static void RPC_GetCompressionVersion(ZRpc rpc, int compressionVersion) {
            BN_Logger.LogMessage($"Compression version received from {rpc.GetSocket().GetHostName()}: {compressionVersion}");

            PeerCompressionVersionReceived(rpc.GetSocket(), compressionVersion);
        }

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static bool SendCompressedPackages(ZSteamSocket __instance, ref Queue<Byte[]> ___m_sendQueue, ref int ___m_totalSent, ref HSteamNetConnection ___m_con) {
            if (!__instance.IsConnected()) {
                return false;
            }

            if (!PeerHasCompression(__instance)) {
                BN_Logger.LogInfo($"Compressed Send: Sending uncompressed data to {__instance.GetHostName()}");
                return true;
            }

            while (___m_sendQueue.Count > 0) {

                byte[][] packageArray = ___m_sendQueue.ToArray();
                int uncompressedPackagesLength = 0;
                byte[] compressedPackages;

                using (MemoryStream compressedPackagesStream = new MemoryStream()) {
                    using (BinaryWriter compressedPackagesWriter = new BinaryWriter(compressedPackagesStream)) {

                        compressedPackagesWriter.Write(packageArray.Length); // number of packages

                        for (int i = 0; i < packageArray.Length; i++) {
                            compressedPackagesWriter.Write(packageArray[i].Length); // length of package
                            compressedPackagesWriter.Write(packageArray[i]); // package
                            uncompressedPackagesLength += packageArray[i].Length + 1; // +1 for the package length byte
                        }

                        compressedPackagesWriter.Flush();
                        compressedPackages = LZ4Pickler.Pickle(compressedPackagesStream.ToArray());
                    }
                }

                IntPtr intPtr = Marshal.AllocHGlobal(compressedPackages.Length);
                Marshal.Copy(compressedPackages, 0, intPtr, compressedPackages.Length);

                EResult eresult;
                long messagesSentCount;
                if (BN_Utils.IsDedicated()) {
                    eresult = SteamGameServerNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedPackages.Length, 8, out messagesSentCount);
                } else {
                    eresult = SteamNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedPackages.Length, 8, out messagesSentCount);
                }

                Marshal.FreeHGlobal(intPtr);

                if (eresult != EResult.k_EResultOK) {
                    BN_Logger.LogError($"Compressed Send: {eresult}; please notify the mod author");
                    return true;
                }
                ___m_totalSent += compressedPackages.Length;
                for (int i = 0; i < packageArray.Length; i++) {
                    ___m_sendQueue.Dequeue(); // TODO: inefficient
                }

                if (uncompressedPackagesLength > 256) { // small messages don't compress well but they also don't matter
                    float compressedSizePercentage = ((float)compressedPackages.Length / (float)uncompressedPackagesLength) * 100;
                    BN_Logger.LogInfo($"Compressed Send: {uncompressedPackagesLength} B compressed to {compressedSizePercentage.ToString("0")}%");
                }
            }

            return false;
         }

        private static Queue<ZPackage> packages = new Queue<ZPackage>();

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPrefix]
        private static bool ReceiveCompressedPackages(ref ZPackage __result, ZSteamSocket __instance, ref HSteamNetConnection ___m_con, ref int ___m_totalRecv, ref bool ___m_gotData) {
            if (packages.Count > 0) {
                BN_Logger.LogInfo("Compressed Receive: Dequeueing previously received package");
                ZPackage package = packages.Dequeue();
                ___m_totalRecv += package.Size();
                ___m_gotData = true;

                __result = package;
                return false;
            }
            
            if (!__instance.IsConnected()) {
                __result = null;
                return false;
            }

            if (!PeerHasCompression(__instance)) {
                BN_Logger.LogInfo($"Compressed Receive: Receiving uncompressed data from {__instance.GetHostName()}");
                return true;
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

                byte[] uncompressedPackages;
                try {
                    uncompressedPackages = LZ4Pickler.Unpickle(compressedPackages);
                } catch {
                    BN_Logger.LogWarning("Compressed Receive: Couldn't decompress message; assuming uncompressed");

                    ZPackage zpackage = new ZPackage(compressedPackages);
                    steamNetworkingMessage_t.m_pfnRelease = array[0];
                    steamNetworkingMessage_t.Release();
                    ___m_totalRecv += zpackage.Size();
                    ___m_gotData = true;
                    __result = zpackage;
                    return false;
                }

                if (uncompressedPackages.Length > 256) { // small messages don't compress well but they also don't matter
                    float compressedSizePercentage = ((float)steamNetworkingMessage_t.m_cbSize / (float)uncompressedPackages.Length) * 100;
                    BN_Logger.LogInfo($"Compressed Receive: {uncompressedPackages.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
                }

                steamNetworkingMessage_t.m_pfnRelease = array[0];
                steamNetworkingMessage_t.Release();

                using (MemoryStream uncompressedPackagesStream = new MemoryStream(uncompressedPackages)) {
                    using (BinaryReader uncompressedPackagesReader = new BinaryReader(uncompressedPackagesStream)) {
                        int packageCount = uncompressedPackagesReader.ReadInt32();
                        for (int i = 0; i < packageCount; i++) {
                            int packageLength = uncompressedPackagesReader.ReadInt32();
                            byte[] packageByteArray = uncompressedPackagesReader.ReadBytes(packageLength);
                            packages.Enqueue(new ZPackage(packageByteArray));
                        }
                    }
                }

                ZPackage package = packages.Dequeue();
                ___m_totalRecv += package.Size();
                ___m_gotData = true;

                __result = package;
                return false;
            }
            __result = null;
            return false;
        }


        private class PeerCompression {
            public int compressionVersionSent = 0;
            public int compressionVersionReceived = 0;

            public bool hasCompression {
                get {
                    if (compressionVersionReceived == 0 || compressionVersionSent == 0) { return false; }
                    return compressionVersionReceived == compressionVersionSent;
                }
            }

            public bool handshakeComplete {
                get {
                    return (compressionVersionReceived > 0 && compressionVersionSent > 0);
                }
            }
        }

        private static Dictionary<ISocket, PeerCompression> peers = new Dictionary<ISocket, PeerCompression>();


        public static void PeerCompressionVersionReceived(ISocket socket, int compressionVersion) {
            if (!peers.ContainsKey(socket)) {
                peers.Add(socket, new PeerCompression());
            }

            peers[socket].compressionVersionReceived = compressionVersion;

            LogPeerCompressionInfo(socket);
        }

        public static void PeerCompressionVersionSent(ISocket socket) {
            if (!peers.ContainsKey(socket)) {
                peers.Add(socket, new PeerCompression());
            }

            peers[socket].compressionVersionSent = COMPRESSION_VERSION;

            LogPeerCompressionInfo(socket);
        }
        public static void RemovePeer(ISocket socket) {
            peers.Remove(socket);
        }

        public static void LogPeerCompressionInfo(ISocket socket) {
            if (!peers.ContainsKey(socket)) {
                BN_Logger.LogError($"Compression: Unknown peer: {socket.GetHostName()}");
                return;
            }

            if (!peers[socket].handshakeComplete) {
                BN_Logger.LogInfo($"Compression: Handshake incomplete with {socket.GetHostName()}");
                return;
            }

            if (peers[socket].hasCompression) {
                BN_Logger.LogMessage($"Compression enabled for {socket.GetHostName()}");
            } else {
                if (peers[socket].compressionVersionReceived > COMPRESSION_VERSION) {
                    BN_Logger.LogError($"Can't use network compression for {socket.GetHostName()}: you need to download the latest version of Better Networking");
                } else {
                    BN_Logger.LogWarning($"Can't use network compression for {socket.GetHostName()}: they are using an older version of Better Networking");
                }
            }
        }

        public static bool PeerHasCompression(ISocket socket) {
            if (socket != null && peers.ContainsKey(socket)) {
                return peers[socket].hasCompression;
            }

            return false;
        }
    }
}
