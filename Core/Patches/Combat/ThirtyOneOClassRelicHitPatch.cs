using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class ThirtyOneOSakuraSwordTasselHitPatch
{
    // All native power additions/doublings and the existing final postfix run
    // first. The relic contributes one final hit only to the actual finisher.
    [HarmonyPriority(Priority.Last - 100)]
    private static void Postfix(AttackCommand __1, ref decimal __result)
    {
        if (ThirtyOneOClassRelicHelper.HasLuckySevenBonus(__1))
            __result = Math.Min(int.MaxValue, Math.Max(0m, __result) + 1m);
    }
}
