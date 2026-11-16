using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Plow stores a native remaining-HP threshold, not damage or a skill amount.
// Keep its raw stacks/save data; compare and display it in the enemy HP scale.
internal static class HbrPlowThresholdHelper
{
    internal static bool TryResolve(PowerModel power, decimal raw, out decimal threshold)
    {
        threshold = raw;
        if (power is not PlowPower || !power.IsMutable ||
            !EnemyStatScalingHelper.IsEnemy(power.Owner) ||
            !HbrBattleStatsHelper.IsEnabled(power.Owner)) return false;
        threshold = raw <= 0m ? 0m : HbrEnemyResourceAdapter.ScaleHealth(power.Owner, raw);
        return true;
    }

    internal static bool IsAtOrBelow(Creature target, PowerModel power) =>
        TryResolve(power, power.Amount, out decimal threshold)
            ? LargeCreatureHealthHelper.GetCurrentHp(target) <= threshold
            : target.CurrentHp <= power.Amount;
}

[HarmonyPatch(typeof(LargeCreatureHealthHelper), nameof(LargeCreatureHealthHelper.GetCurrentForPlowComparison))]
internal static class HbrPlowThresholdRuntimePatch
{
    private static bool Prefix(Creature creature, ref int __result)
    {
        PlowPower power = creature?.GetPower<PlowPower>();
        if (!HbrPlowThresholdHelper.TryResolve(power, power?.Amount ?? 0m, out decimal threshold)) return true;
        // The existing single native Plow transpiler already calls this helper.
        // Return the same comparison sentinel, with a real HBR HP threshold.
        __result = power.Amount + decimal.Compare(LargeCreatureHealthHelper.GetCurrentHp(creature), threshold);
        return false;
    }
}

[HarmonyPatch(typeof(PowerModel), "AddDumbVariablesToDescription")]
internal static class HbrPlowThresholdDescriptionPatch
{
    private static void Postfix(PowerModel __instance, LocString __0, int? __1)
    {
        if (__instance is PlowPower &&
            HbrPlowThresholdHelper.TryResolve(__instance, __1 ?? __instance.Amount, out decimal threshold))
            __0.Add("Amount", threshold);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.DisplayAmount), MethodType.Getter)]
internal static class HbrPlowThresholdBadgePatch
{
    private static void Postfix(PowerModel __instance, ref int __result)
    {
        if (__instance is PlowPower &&
            HbrPlowThresholdHelper.TryResolve(__instance, __instance.Amount, out decimal threshold))
            __result = (int)Math.Clamp(threshold, 0m, BigCombatNumbers.NativeStatCap);
    }
}
