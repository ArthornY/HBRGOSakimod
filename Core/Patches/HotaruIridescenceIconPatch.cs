using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.UpdateTexture))]
internal static class HotaruIridescenceIconPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(RelicModel __instance, TextureRect __0)
    {
        if (__instance is HotaruIridescence)
            HotaruIridescenceVisuals.Apply(__0);
        else
            HotaruIridescenceVisuals.Restore(__0);
    }
}
