using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.TickDownDuration))]
internal static class HotaruUnfadingScarsDurationPatch
{
    private static bool Prefix(PowerModel power, ref Task __result)
    {
        if (!HotaruUnfadingScarsPower.PreventsNaturalDecay(power)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(WeakPower), nameof(WeakPower.ModifyDamageMultiplicative))]
internal static class HotaruUnfadingScarsWeakPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(WeakPower __instance, Creature dealer, ValueProp props, ref decimal __result)
    {
        if (!HbrBattleStatsHelper.IsEnabled(__instance.Owner) && dealer == __instance.Owner && props.IsPoweredAttack() && __result < 1m)
            __result = Math.Max(0m, 1m - HotaruUnfadingScarsPower.EnhanceEffect(
                __instance.Owner, 1m - __result));
    }
}

[HarmonyPatch(typeof(VulnerablePower), nameof(VulnerablePower.ModifyDamageMultiplicative))]
internal static class HotaruUnfadingScarsVulnerablePatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(VulnerablePower __instance, Creature target, ValueProp props, ref decimal __result)
    {
        if (!HbrBattleStatsHelper.IsEnabled(__instance.Owner) && target == __instance.Owner && props.IsPoweredAttack() && __result > 1m)
            __result = DamageAmplificationLimits.AddSaturated(1m,
                HotaruUnfadingScarsPower.EnhanceEffect(__instance.Owner, __result - 1m));
    }
}
