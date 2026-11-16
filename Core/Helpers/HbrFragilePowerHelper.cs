using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// One skill coefficient, with HBR INT/LUCK weights and positive-difference slope.
internal static class HbrFragilePowerHelper
{
    internal const decimal BasePercentPerLayer = 30m;
    private static readonly HbrStatWeights Weights = new(0, 0, 1, 0, 0, 2);

    internal static decimal BasePercent(decimal modifiers) => ScaleModifierPercent(modifiers, BasePercentPerLayer);

    internal static decimal ScaleModifierPercent(decimal modifiers, decimal initialPercent)
    {
        int count = (int)Math.Min(int.MaxValue, Math.Max(0m, decimal.Truncate(modifiers)));
        if (count == 0) return 0m;
        // Exponentiation by squaring bounds work for even saturated counters.
        decimal factor = 1m, basis = 1.25m;
        for (int exponent = count - 1; exponent > 0; exponent >>= 1)
        {
            if ((exponent & 1) != 0)
                factor = DamageAmplificationLimits.MultiplySaturated(factor, basis);
            if (exponent > 1)
                basis = DamageAmplificationLimits.MultiplySaturated(basis, basis);
        }
        return DamageAmplificationLimits.MultiplySaturated(initialPercent, factor);
    }

    internal static decimal EvaluatePercent(decimal modifiers, decimal intelligence,
        decimal luck, decimal enemyParameter)
    {
        if (modifiers <= 0m) return 0m;
        decimal reference = HbrOriginalNumericFormula.EvaluateWeightedStat(
            new HbrBattleStats(0, 0, intelligence, 0, 0, luck), Weights);
        return HbrOriginalNumericFormula.EvaluateLinearDebuffPotency(BasePercent(modifiers), reference, enemyParameter);
    }

    internal static decimal ResolvePercent(decimal modifiers, Creature applier, Creature target) =>
        HbrEffectFormula.Debuff(BasePercent(modifiers), applier, target, Weights);

    internal static decimal PerLayerPercent(Creature applier, Creature target) =>
        ResolvePercent(1m, applier, target);

    // Existing Fragile can power attacks before Break. Keep exact saved values,
    // and enhance their total once instead of treating remaining turns as stacks.
    internal static decimal GetTotalEffectivePercent(Creature target)
    {
        if (target == null) return 0m;
        decimal result = 0m;
        foreach (var power in target.Powers)
            if (power is FragilePower { Amount: > 0 } fragile)
                result = DamageAmplificationLimits.AddSaturated(result, fragile.HbrEffectPercent);
        return HotaruUnfadingScarsPower.EnhanceEffect(target, result);
    }

    internal static int GetModifierAmount(Creature target)
    {
        if (target == null) return 0;
        if (!HbrBattleStatsHelper.IsEnabled(target)) return target.GetPowerAmount<FragilePower>();
        long result = 0;
        foreach (var power in target.Powers)
            if (power is FragilePower { Amount: > 0 } fragile)
                result = Math.Min(int.MaxValue, result + fragile.ModifierAmount);
        return (int)result;
    }
}
