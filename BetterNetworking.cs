using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

using K4os.Compression.LZ4;
using System.Reflection;
using System.ComponentModel;

namespace Mod_ImprovedNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "0.0.2")]
    [BepInProcess("valheim.exe")]
    public class ImprovedNetworking : BaseUnityPlugin {

        static private ConfigEntry<Options_NetworkUpdateRates> configNetworkUpdateRate;
        private enum Options_NetworkUpdateRates {
            [Description("100% (20 updates/second)")]
            _100,
            [Description("80% (16 updates/second)")]
            _80,
            [Description("60% (12 updates/second)")]
            _60,
            [Description("40% (8 updates/second)")]
            _40,
            [Description("20% (4 updates/second)")]
            _20
        }

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        void Awake() {
            harmony.PatchAll();

            configNetworkUpdateRate = Config.Bind(
                "Networking",
                "NetworkUpdateRate",
                Options_NetworkUpdateRates._100,
                new ConfigDescription(
                    "You can reduce network strain by reducing the number of updates your computer sends out. Values are correct as of patch 0.203.11."
                )
            );
        }

        [HarmonyPatch(typeof(ZDOMan), "SendZDOToPeers")]
        class NetworkUpdateFrequency_Patch {

            static void Prefix(ref float dt) {
                float networkUpdateRate = 1.0f;
                switch (configNetworkUpdateRate.Value) {
                    case Options_NetworkUpdateRates._80:
                        networkUpdateRate = 0.8f;
                        break;
                    case Options_NetworkUpdateRates._60:
                        networkUpdateRate = 0.6f;
                        break;
                    case Options_NetworkUpdateRates._40:
                        networkUpdateRate = 0.4f;
                        break;
                    case Options_NetworkUpdateRates._20:
                        networkUpdateRate = 0.2f;
                        break;
                }
                dt = dt * networkUpdateRate;
            }
        }
    }
}
