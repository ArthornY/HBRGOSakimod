using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage))]
internal static class AttackIntentSingleHitLimitPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref int __result) =>
        __result = Math.Min(__result, DamageLimitHelper.MaximumSingleHitDamage);
}
