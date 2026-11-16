using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native monster attacks hit every player creature. A move-table monster's
// original all-unit skill also hits the player side's hittable summons, and
// its MinimumDp skill hits the player with the least DP. Every other attack
// keeps the native list; the rest of the native attack (animation, hit
// effects, per-hit retargeting, history and hooks) is unchanged.
[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class HbrAttackTargetingPatch
{
    [HarmonyPostfix]
    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (HbrAttackTargeting.TryGet(__instance, out HbrgoAttackTargets targets))
        {
            __result = HbrAttackTargeting.Resolve(__instance.Attacker, __result, targets);
        }
    }
}

// Confusion (original ConfusionRandom): the holder's first single-target
// attack card each turn picks its target among the enemies and the holder.
// Runs before the mode-transition prefix, which replays the play with the
// target argument it receives.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class HbrConfusionCardTargetPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, ref Creature target)
    {
        target = HbrProvokeTargetingHelper.ResolveTarget(__instance, target);
        if (__instance?.Owner?.Creature?.GetPower<HbrConfusionPower>() is { } confusion)
        {
            target = confusion.ChooseCardTarget(__instance, target);
        }
    }
}
