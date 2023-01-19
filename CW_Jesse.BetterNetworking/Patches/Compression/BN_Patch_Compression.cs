﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;

using HarmonyLib;
using Steamworks;
using K4os.Compression.LZ4;
using BepInEx.Configuration;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public partial class BN_Patch_Compression {
        private const int COMPRESSION_VERSION = 3;

        private const int k_nSteamNetworkingSend_Reliable = 8;                       // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int k_cbMaxSteamNetworkingSocketsMessageSizeSend = 512 * 1024; // https://partner.steamgames.com/doc/api/steamnetworkingtypes

        public enum Options_NetworkCompression {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configCompressionEnabled = config.Bind(
                "Networking (Steamworks)",
                "Compression Enabled",
                Options_NetworkCompression.@true,
                new ConfigDescription("Most people will want to keep this enabled.\n" +
                "---\n" +
                "If your internet is great and your computer isn't, then try lowering your update rate, lowering your queue size, and/or disabling compression."));

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

            SendCompressionEnabledStatus(newCompressionStatus);

            CompressionStatus.ourStatus.compressionEnabled = newCompressionStatus;
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void OnConnect(ref ZNetPeer peer) {
            CompressionStatus.AddPeer(peer);

            RegisterRPCs(peer);
            SendCompressionVersion(peer, CompressionStatus.ourStatus.version);
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        [HarmonyPostfix]
        private static void OnDisconnect(ZNetPeer peer) {
            CompressionStatus.RemovePeer(peer);
        }

        [HarmonyPatch(typeof(ZSteamSocket), "SendQueuedPackages")]
        [HarmonyPrefix]
        private static bool SendCompressedPackages(ref ZSteamSocket __instance, ref Queue<Byte[]> ___m_sendQueue, ref int ___m_totalSent, ref HSteamNetConnection ___m_con) {
            if (!__instance.IsConnected()) {
                return false;
            }

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetSendCompressionStarted(peer)) {
#if DEBUG
                BN_Logger.LogInfo($"Compressed Send: Sending uncompressed message to {BN_Utils.GetPeerName(peer)}");
#endif
                return true;
            }
#if DEBUG
            BN_Logger.LogInfo($"Compressed Send: Sending compressed message to {BN_Utils.GetPeerName(peer)}");
#endif

            lock (___m_sendQueue) {

                while (___m_sendQueue.Count > 0) {

                    int packagesToSendLength = 0;

                    // determine how many packages to send in a single message

                    List<byte[]> packagesToSendList = new List<byte[]>();
                    foreach (byte[] package in ___m_sendQueue) {
                        if (packagesToSendList.Count > 0 && // send at least one package
                            packagesToSendLength + package.Length > k_cbMaxSteamNetworkingSocketsMessageSizeSend) { // packages must not exceed steam message send size limit uncompressed (assumes successful compression)
                            BN_Logger.LogMessage($"Compressed Send ({BN_Utils.GetPeerName(peer)}): Reached send limit: {packagesToSendList.Count} packages size: {packagesToSendLength}/{k_cbMaxSteamNetworkingSocketsMessageSizeSend}; sending {___m_sendQueue.Count - packagesToSendList.Count} queued packages in another message");
                            break;
                        }

                        packagesToSendLength += package.Length;
                        packagesToSendList.Add(package);
                    }

                    // compress message

                    byte[][] packagesToSendArray = packagesToSendList.ToArray();
                    byte[] compressedMessage;

                    using (MemoryStream compressedPackagesStream = new MemoryStream()) {
                        using (BinaryWriter compressedPackagesWriter = new BinaryWriter(compressedPackagesStream)) {

                            compressedPackagesWriter.Write(packagesToSendArray.Length); // number of packages

                            for (int i = 0; i < packagesToSendArray.Length; i++) {
                                compressedPackagesWriter.Write(packagesToSendArray[i].Length); // length of package
                                compressedPackagesWriter.Write(packagesToSendArray[i]); // package
                            }

                            compressedPackagesWriter.Flush();
                            compressedMessage = LZ4Pickler.Pickle(compressedPackagesStream.ToArray());
                        }
                    }

#if DEBUG
                    BN_Logger.LogInfo($"Compressed Send {BN_Utils.GetPeerName(peer)}: Message reduced from {packagesToSendLength} B to {compressedMessage.Length} B");
#endif

                    // send message

                    IntPtr intPtr = Marshal.AllocHGlobal(compressedMessage.Length);
                    Marshal.Copy(compressedMessage, 0, intPtr, compressedMessage.Length);

                    EResult eresult;
                    long messagesSentCount;
                    if (BN_Utils.IsDedicated()) {
                        eresult = SteamGameServerNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedMessage.Length, k_nSteamNetworkingSend_Reliable, out messagesSentCount);
                    } else {
                        eresult = SteamNetworkingSockets.SendMessageToConnection(___m_con, intPtr, (uint)compressedMessage.Length, k_nSteamNetworkingSend_Reliable, out messagesSentCount);
                    }

                    Marshal.FreeHGlobal(intPtr);

                    // ensure message was sent

                    if (eresult != EResult.k_EResultOK) {
                        BN_Logger.LogError($"Compressed Send ({BN_Utils.GetPeerName(peer)}): ERROR {eresult}; disabling compression for this message; please notify mod author");
                        return true;
                    }

                    // remove sent messages from queue

                    ___m_totalSent += compressedMessage.Length;
                    for (int i = 0; i < packagesToSendArray.Length; i++) {
                        ___m_sendQueue.Dequeue();
                    }

                    // log result

                    if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info) {
                        if (packagesToSendLength > 256) { // small messages don't compress well but they also don't matter
                            float compressedSizePercentage = ((float)compressedMessage.Length / (float)packagesToSendLength) * 100;
                            BN_Logger.LogInfo($"Compressed Send ({BN_Utils.GetPeerName(peer)}): {packagesToSendLength} B compressed to {compressedSizePercentage.ToString("0")}%");
                        }
                    }
                }
            }

            return false;
         }

        private readonly static Queue<ZPackage> packages = new Queue<ZPackage>();

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPrefix]
        private static bool ReceiveCompressedPackages(ref ZPackage __result, ref ZSteamSocket __instance, ref HSteamNetConnection ___m_con, ref int ___m_totalRecv, ref bool ___m_gotData) {
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

            ZNetPeer peer = BN_Utils.GetPeer(__instance);
            if (!CompressionStatus.GetReceiveCompressionStarted(peer)) {
#if DEBUG
                BN_Logger.LogInfo($"Compressed Receive: Receiving uncompressed message from {BN_Utils.GetPeerName(peer)}");
#endif
                return true;
            }
#if DEBUG
            BN_Logger.LogInfo($"Compressed Receive: Receiving compressed message from {BN_Utils.GetPeerName(peer)}");
#endif

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
                steamNetworkingMessage_t.m_pfnRelease = array[0];
                steamNetworkingMessage_t.Release();

                byte[] uncompressedPackages;
                try {
                    uncompressedPackages = LZ4Pickler.Unpickle(compressedPackages);
                } catch {
                    BN_Logger.LogInfo($"Compressed Receive ({BN_Utils.GetPeerName(peer)}): Couldn't decompress message; assuming uncompressed");

                    ZPackage zpackage = new ZPackage(compressedPackages);
                    ___m_totalRecv += zpackage.Size();
                    ___m_gotData = true;
                    __result = zpackage;
                    return false;
                }

                if (BetterNetworking.configLogMessages.Value >= BN_Logger.Options_Logger_LogLevel.info) {
                    if (uncompressedPackages.Length > 256) { // small messages don't compress well but they also don't matter
                        float compressedSizePercentage = ((float)steamNetworkingMessage_t.m_cbSize / (float)uncompressedPackages.Length) * 100;
                        BN_Logger.LogInfo($"Compressed Receive ({BN_Utils.GetPeerName(peer)}): {uncompressedPackages.Length} B compressed to {compressedSizePercentage.ToString("0")}%");
                    }
                }

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


        private static class CompressionStatus {

            public const int COMPRESSION_VERSION_UNKNOWN = 0;

            public static PeerCompressionStatus ourStatus = new PeerCompressionStatus() { version = COMPRESSION_VERSION, compressionEnabled = BetterNetworking.configCompressionEnabled.Value == Options_NetworkCompression.@true };
            private readonly static Dictionary<ZNetPeer, PeerCompressionStatus> peerStatuses = new Dictionary<ZNetPeer, PeerCompressionStatus>();
            public class PeerCompressionStatus {
                public int version = COMPRESSION_VERSION_UNKNOWN;
                public bool compressionEnabled = false;
                public bool receivingCompressed = false;
                public bool sendingCompressed = false;
            }
            public static bool AddPeer(ZNetPeer peer) {
                if (peer == null) {
                    BN_Logger.LogError("Compression: Tried to add null peer");
                    return false;
                }

                peerStatuses.Add(peer, new PeerCompressionStatus());
                return true;
            }
            public static void RemovePeer(ZNetPeer peer) {
                if (!IsPeerExist(peer)) {
                    BN_Logger.LogError($"Compression: Tried to remove non-existent peer: {BN_Utils.GetPeerName(peer)}");
                    return;
                }

                peerStatuses.Remove(peer);
            }
            public static bool IsPeerExist(ZNetPeer peer) {
                if (peer != null && peerStatuses.ContainsKey(peer)) { return true; }
                return false;
            }

            public static int GetVersion(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return 0; }
                return peerStatuses[peer].version;
            }
            public static void SetVersion(ZNetPeer peer, int theirVersion) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].version = theirVersion;
            }
            public static bool GetIsCompatibleWith(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return (ourStatus.version == GetVersion(peer));
            }

            public static bool GetCompressionEnabled(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return peerStatuses[peer].compressionEnabled;
            }
            public static void SetCompressionEnabled(ZNetPeer peer, bool enabled) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].compressionEnabled = enabled;
            }

            public static bool GetSendCompressionStarted(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return peerStatuses[peer].sendingCompressed;
            }
            public static void SetSendCompressionStarted(ZNetPeer peer, bool started) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].sendingCompressed = started;
            }

            public static bool GetReceiveCompressionStarted(ZNetPeer peer) {
                if (!IsPeerExist(peer)) { return false; }
                return peerStatuses[peer].receivingCompressed;
            }
            public static void SetReceiveCompressionStarted(ZNetPeer peer, bool started) {
                if (!IsPeerExist(peer)) { return; }
                peerStatuses[peer].receivingCompressed = started;
            }
        }
    }
}