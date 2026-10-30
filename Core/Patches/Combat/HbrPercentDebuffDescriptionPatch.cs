using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrPercentDebuffPowerDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Description));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.SmartDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.RemoteDescription));
    }

    private static void Postfix(PowerModel __instance, ref LocString __result) =>
        __result = ForPower(__instance, __result);

    internal static LocString ForPower(PowerModel power, LocString original)
    {
        if (power.GetType().Assembly != typeof(HbrPercentDebuffPower).Assembly || !power.IsMutable ||
            !HbrBattleStatsHelper.IsEnabled(power.Owner)) return original;
        HbrPercentDebuffDescriptionHelper.Track(original, Array.Empty<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar>());
        return original;
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class HbrPercentDebuffRawDescriptionPatch
{
    private static void Postfix(LocString __instance, ref string __result) =>
        __result = HbrPercentDebuffDescriptionHelper.Rewrite(__instance, __result);
}

[HarmonyPatch]
internal static class HbrPercentDebuffReferenceHoverTipsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.HoverTips));
        yield return AccessTools.PropertyGetter(typeof(PotionModel), nameof(PotionModel.HoverTips));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.HoverTips));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.HoverTipsExcludingRelic));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.HoverTips));
    }

    private static void Postfix(AbstractModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__result != null && HbrPercentDebuffDescriptionHelper.IsEnabled(__instance))
            __result = HbrPercentDebuffDescriptionHelper.RewriteHoverTips(__instance, __result);
    }
}
