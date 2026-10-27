using System;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class EnemyStatScalingHelper
{
    internal const int MaximumStat = 999999999;

    internal static bool IsEnemy(Creature creature) =>
        creature?.Monster != null &&
        creature.Side == CombatSide.Enemy &&
        creature.PetOwner == null &&
        !creature.HasPower<PhantomPower>();

    internal static decimal GetMaxHpMultiplier(Creature creature) =>
        IsEnemy(creature)
            ? BigCombatNumbers.CalculateHealthMultiplier(
                HBRGOSakiConfig.EnemyMaxHpPercent,
                HBRGOSakiConfig.EnemyMaxHpGrowthPerActPercent,
                GetActIndex(creature)) *
                HbrEnemyResourceAdapter.GetMaxHpScale(creature)
            : 1m;

    internal static decimal GetDamageMultiplier(Creature creature) =>
        IsEnemy(creature)
            ? CalculateMultiplier(
                HBRGOSakiConfig.EnemyDamagePercent,
                HBRGOSakiConfig.EnemyDamageGrowthPerActPercent,
                GetActIndex(creature))
            : 1m;

    internal static decimal GetDpGrowthMultiplier(Creature creature) =>
        IsEnemy(creature)
            ? BigCombatNumbers.CalculateHealthMultiplier(
                100,
                HBRGOSakiConfig.EnemyDpGrowthPerActPercent,
                GetActIndex(creature))
            : 1m;

    // Act one has index zero. Exponentiation by squaring also handles custom
    // endless acts without a long loop or overflowing decimal arithmetic.
    internal static decimal CalculateMultiplier(
        int basePercent,
        int growthPercent,
        int actIndex)
    {
        decimal result = Math.Clamp(basePercent, 0, 1000) / 100m;
        if (result == 0m) return 0m;

        decimal growth = 1m + Math.Clamp(growthPercent, 0, 1000) / 100m;
        decimal growthLimit = MaximumStat / Math.Min(1m, result);
        int exponent = Math.Max(0, actIndex);
        while (exponent > 0)
        {
            if ((exponent & 1) != 0)
                result = Math.Min(MaximumStat, result * growth);
            exponent >>= 1;
            if (exponent > 0)
                growth = Math.Min(growthLimit, growth * growth);
        }

        return result;
    }

    internal static int ScaleHealth(decimal value, decimal multiplier) =>
        (int)Math.Clamp(decimal.Floor(value * multiplier), 1m, MaximumStat);

    private static int GetActIndex(Creature creature) =>
        creature.CombatState?.RunState.CurrentActIndex ?? 0;
}
