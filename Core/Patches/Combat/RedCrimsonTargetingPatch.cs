using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class RedCrimsonAttackTargetPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        AttackCommand __instance,
        ref IReadOnlyList<Creature> __result)
    {
        Creature attacker = __instance.Attacker;
        ICombatState combatState = attacker?.CombatState;
        if (attacker == null ||
            combatState == null ||
            attacker.Side != CombatSide.Enemy ||
            __instance.IsSingleTargeted)
        {
            return;
        }

        IReadOnlyList<Creature> redCrimsons = combatState.Allies
            .Where(
                creature =>
                    creature.IsHittable &&
                    creature.IsAlive &&
                    creature.Monster is RedCrimson)
            .ToList();
        if (redCrimsons.Count == 0)
        {
            return;
        }

        // Player creature collections can be local-player-first on each peer.
        // Normalize the order before multi-hit attacks walk the target list.
        __result = __result
            .Concat(redCrimsons)
            .Distinct()
            .OrderBy(creature => creature.Monster is RedCrimson ? 1 : 0)
            .ThenBy(GetOwnerSlotIndex)
            .ToList();
    }

    private static int GetOwnerSlotIndex(Creature creature)
    {
        if (creature.Player != null)
        {
            return creature.Player.RunState.GetPlayerSlotIndex(creature.Player);
        }

        if (creature.PetOwner != null)
        {
            return creature.PetOwner.RunState.GetPlayerSlotIndex(creature.PetOwner);
        }

        return int.MaxValue;
    }
}
