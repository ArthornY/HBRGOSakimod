using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class ThirtyOneCDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
    }
    private static void Postfix(RelicModel __instance, LocString __result)
    {
        if (__instance is ThirtyOneCCollection collection) collection.AddDescriptionVariables(__result);
        if (__instance is ThirtyOneDCollection scouting) scouting.AddDescriptionVariables(__result);
    }
}
