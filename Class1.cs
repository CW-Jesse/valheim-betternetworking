using BepInEx;
using HarmonyLib;
using UnityEngine;


namespace Mod_ImprovedNetworking
{
    [BepInPlugin("Valheim.CW_Jesse.ImprovedNetworking", "Improved Networking", "0.0.1")]
    [BepInProcess("valheim.exe")]
    public class ImprovedNetworking : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("Valheim.CW_Jesse.ImprovedNetworking");

        void Awake() {
            harmony.PatchAll();
        }

        [HarmonyPatch(ZDOMan, ZDOMan.)]
    }
}
