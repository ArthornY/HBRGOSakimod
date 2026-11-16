using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native targeting, retargeting, hit hooks and result types stay intact. Only
// the exact species command created by this card receives the intersection.
[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class HisameCloseSlashTargetPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result) =>
        __result = HisameCloseSlashCombatHelper.FilterTargets(__instance, __result);
}
