using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class EnemyHealthScalingHelper
{
    private sealed class AppliedState { }

    private static readonly ConditionalWeakTable<Creature, AppliedState> Applied = new();

    internal static async Task AfterAddedToRoom(Task originalTask, Creature creature)
    {
        // Monsters may normalize their HP or enter an already-hatched phase in this callback.
        // Apply once after those operations, before combat-start and summon DP hooks.
        await originalTask;
        ApplyInitialHealth(creature);
    }

    internal static void ApplyInitialHealth(Creature creature)
    {
        if (!EnemyStatScalingHelper.IsEnemy(creature) || Applied.TryGetValue(creature, out _))
            return;

        decimal originalMaxHp = LargeCreatureHealthHelper.GetMaxHp(creature);
        decimal originalCurrentHp = LargeCreatureHealthHelper.GetCurrentHp(creature);
        decimal multiplier = EnemyStatScalingHelper.GetMaxHpMultiplier(creature);
        decimal maximum = BigCombatNumbers.ScaleHealth(originalMaxHp, multiplier);
        decimal current = originalCurrentHp <= 0 ? 0 : Math.Min(maximum,
            BigCombatNumbers.ScaleHealth(originalCurrentHp, multiplier));

        // Mark before notifying health observers so reentrant callbacks cannot multiply twice.
        Applied.Add(creature, new AppliedState());
        if (maximum != originalMaxHp) creature.SetMaxHpInternal(maximum);
        if (current != LargeCreatureHealthHelper.GetCurrentHp(creature)) creature.SetCurrentHpInternal(current);
    }

    internal static decimal ScaleReplacementHealth(decimal amount, Creature creature)
    {
        // The original source below supplies a fresh, unscaled HP value. An already-hatched
        // egg can execute the same source during its first entry; the outer entry hook owns
        // scaling in that case. Re-adds and later phases retain their one-time entry marker.
        return EnemyStatScalingHelper.IsEnemy(creature) && Applied.TryGetValue(creature, out _)
            ? BigCombatNumbers.ScaleHealth(amount,
                EnemyStatScalingHelper.GetMaxHpMultiplier(creature))
            : amount;
    }
}
