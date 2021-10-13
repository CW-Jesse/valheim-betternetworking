using BepInEx;
using HarmonyLib;
using UnityEngine;

using K4os.Compression.LZ4;
using System.Reflection;

namespace Mod_ImprovedNetworking {

    [BepInPlugin("Valheim.CW_Jesse.ImprovedNetworking", "Improved Networking", "0.0.1")]
    [BepInProcess("valheim.exe")]
    public class ImprovedNetworking : BaseUnityPlugin {
        private readonly Harmony harmony = new Harmony("Valheim.CW_Jesse.ImprovedNetworking");

        void Awake() {
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ZDOMan), "SendZDOToPeers")]
        class NetworkUpdateFrequency_Patch {

            static void Prefix(ref float dt) {
                //Debug.Log($"Time since last update: {dt}");
            }
        }
    }
}
