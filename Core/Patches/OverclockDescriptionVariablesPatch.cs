using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

// A card's generic relic tooltip also formats the compact description. Supply
// the same variable there, even when no live top-bar relic supplied it first.
[HarmonyPatch]
internal static class OverclockDescriptionVariablesPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(RelicModel __instance, LocString __result)
    {
        if (__instance is not Overclock overclock || __result == null) return;
        int remaining = overclock.IsMutable && overclock.Owner?.RunState != null
            ? overclock.RemainingFloorLevels : Overclock.FloorLevelLimit;
        __result.Add(HbrCompactTextKeys.RemainingLevels, remaining);
    }
}
