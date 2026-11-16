using MegaCrit.Sts2.Core.Entities.Creatures;
using HBRGOSakiMod.Core.Models.Powers;
using System;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrEffectFormula
{
    internal static readonly HbrStatWeights Intelligence = new(0, 0, 1, 0, 0, 0);
    internal static readonly HbrStatWeights IntelligenceLuck = new(0, 0, 2, 0, 0, 1);
    internal static readonly HbrStatWeights Fragile = new(0, 0, 1, 0, 0, 2);

    internal static decimal Weighted(HbrBattleStats stats, HbrStatWeights weights) =>
        HbrOriginalNumericFormula.EvaluateWeightedStat(stats, weights);

    internal static bool TryParameter(Creature source, HbrStatWeights weights, out decimal parameter)
    {
        if (HbrBattleStatsHelper.TryGetStats(source, out HbrBattleStats stats))
        { parameter = Weighted(stats, weights); return true; }
        // Enemy rows have one status parameter rather than six player stats.
        // Use that scalar directly for effects with a referenced attribute.
        if (EnemyStatScalingHelper.IsEnemy(source) && HbrBattleStatsHelper.IsEnabled(source))
        {
            decimal total = weights.Strength + weights.Dexterity + weights.Intelligence +
                weights.Vitality + weights.Spirit + weights.Luck;
            if (total <= 0m) { parameter = 0m; return false; }
            decimal intelligence = HbrWisdomDownPower.GetWisdomFactor(source);
            // An enemy still owns one E. Only the INT share of this effect's
            // referenced view changes; physical E and non-INT shares do not.
            decimal referencedWeight = total - weights.Intelligence + weights.Intelligence * intelligence;
            parameter = DamageAmplificationLimits.MultiplySaturated(
                HbrEnemyProfileHelper.GetEnemyParameter(source), referencedWeight) / total;
            return true;
        }
        parameter = 0m; return false;
    }

    internal static decimal Buff(decimal basis, Creature source, HbrStatWeights weights) =>
        TryParameter(source, weights, out decimal parameter)
            ? HbrOriginalNumericFormula.EvaluateLinearBuffPotency(basis, parameter) : basis;

    internal static decimal Debuff(decimal basis, Creature source, Creature target, HbrStatWeights weights) =>
        TryParameter(source, weights, out decimal parameter)
            ? HbrOriginalNumericFormula.EvaluateLinearDebuffPotency(basis, parameter,
                HbrEnemyProfileHelper.GetEnemyParameter(target)) : basis;
}
