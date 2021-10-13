using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

using K4os.Compression.LZ4;
using System.Reflection;

namespace Mod_ImprovedNetworking {

    [BepInPlugin("CW_Jesse.BetterNetworking", "Better Networking", "0.0.2")]
    [BepInProcess("valheim.exe")]
    public class ImprovedNetworking : BaseUnityPlugin {

        static private ConfigEntry<float> configNetworkUpdateRate;

        private readonly Harmony harmony = new Harmony("CW_Jesse.BetterNetworking");

        void Awake() {
            harmony.PatchAll();

            configNetworkUpdateRate = Config.Bind("Networking",
                                                  "NetworkUpdateRateMultiplier",
                                                  1.0f,
                                                  "(Default: 1.0) Allows you to increase or decrease how often your Valheim checks to see if it can update peers. As of Patch 0.203.11, 1.0 is 20 times/second. 0.5 is 10 times/second.");
        }

        [HarmonyPatch(typeof(ZDOMan), "SendZDOToPeers")]
        class NetworkUpdateFrequency_Patch {

            static void Prefix(ref float dt) {
                dt = dt * configNetworkUpdateRate.Value;
            }
        }
    }
}
