using System;
using System.Threading.Tasks;
using HarmonyLib;
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
    private static void Postfix(WeakPower __instance, Creature dealer, ValueProp props, ref decimal __result)
    {
        if (dealer == __instance.Owner && props.IsPoweredAttack())
            __result = Math.Max(0m, __result - HotaruUnfadingScarsPower.AdditionalEffect(__instance.Owner));
    }
}

[HarmonyPatch(typeof(VulnerablePower), nameof(VulnerablePower.ModifyDamageMultiplicative))]
internal static class HotaruUnfadingScarsVulnerablePatch
{
    private static void Postfix(VulnerablePower __instance, Creature target, ValueProp props, ref decimal __result)
    {
        if (target == __instance.Owner && props.IsPoweredAttack())
            __result += HotaruUnfadingScarsPower.AdditionalEffect(__instance.Owner);
    }
}
