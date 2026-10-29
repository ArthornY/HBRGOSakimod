using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrStatPowerDescriptionPatch
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
        // Canonical powers have no owner or run context. Leave their native
        // descriptions intact instead of making HBR text leak into legacy runs.
        if (power is not (StrengthPower or DexterityPower) || !power.IsMutable ||
            !HbrBattleStatsHelper.IsEnabled(power.Owner)) return original;
        bool enemy = EnemyStatScalingHelper.IsEnemy(power.Owner);
        string stat = power is StrengthPower ? "STRENGTH" : "DEXTERITY";
        LocString text = new("powers", $"HBR_STAT_{stat}_{(enemy ? "ENEMY" : "ALLY")}.description");
        if (original != null) text.AddVariablesFrom(original);
        if (enemy) text.Add("IgnorePerLayer", HbrStatPowerHelper.GetEnemyIgnorePerLayer(power.Owner));
        return text;
    }
}

[HarmonyPatch(typeof(DexterityPower), "get_ExtraHoverTips")]
internal static class HbrDexterityExtraHoverTipsPatch
{
    private static void Postfix(DexterityPower __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance.IsMutable && HbrBattleStatsHelper.IsEnabled(__instance.Owner))
            __result = Array.Empty<IHoverTip>();
    }
}
