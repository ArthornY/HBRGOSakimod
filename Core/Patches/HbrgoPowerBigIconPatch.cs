using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ResolvedBigIconPath), MethodType.Getter)]
internal static class HbrgoPowerBigIconPatch
{
    private static void Postfix(PowerModel __instance, ref string __result)
    {
        // The vanilla missing-power texture says "NOPE". Powers that share an
        // atlas icon can use it for applied, removed and triggered effects too.
        if (__instance.GetType().Assembly != typeof(HbrgoPowerBigIconPatch).Assembly ||
            !string.Equals(__result, "res://images/powers/missing_power.png", StringComparison.Ordinal))
        {
            return;
        }

        string iconPath = __instance.IconPath;
        if (!string.IsNullOrEmpty(iconPath) && ResourceLoader.Exists(iconPath))
        {
            __result = iconPath;
        }
    }
}
