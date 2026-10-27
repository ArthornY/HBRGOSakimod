using System;

namespace HBRGOSakiMod.Core.Helpers;

internal readonly record struct HbrBattleStats(
    decimal Strength, decimal Dexterity, decimal Intelligence,
    decimal Vitality, decimal Spirit, decimal Luck);

internal readonly record struct HbrStatWeights(
    decimal Strength, decimal Dexterity, decimal Intelligence,
    decimal Vitality, decimal Spirit, decimal Luck);

internal readonly record struct HbrSkillPowerCurve(
    decimal MinimumPower, decimal MaximumPower, decimal ParameterLimit);

/// <summary>
/// Numeric rules reconstructed by the current community HBR calculator, not an
/// official publication of the game's exact formulas. No game state or random
/// generator is consulted here. STS card/monster mappings are separate policy.
/// </summary>
/// <remarks>
/// Reference snapshot: hbr-tool/damage-calculator, ead9f319d18fb47e749718adac0d4384b785c0bc.
/// https://github.com/hbr-tool/damage-calculator/blob/ead9f319d18fb47e749718adac0d4384b785c0bc/src/components/dmgCalc/logic.js#L690-L737
/// https://github.com/hbr-tool/damage-calculator/blob/ead9f319d18fb47e749718adac0d4384b785c0bc/src/components/dmgCalc/logic.js#L890-L921
/// https://github.com/hbr-tool/damage-calculator/blob/ead9f319d18fb47e749718adac0d4384b785c0bc/src/components/dmgCalc/logic.js#L1141-L1311
/// https://github.com/hbr-tool/damage-calculator/blob/ead9f319d18fb47e749718adac0d4384b785c0bc/src/components/dmgCalc/logicBuff.js#L76-L132
/// Equipment jewels and named skill exceptions are deliberately not inferred.
/// </remarks>
internal static class HbrOriginalNumericFormula
{
    internal const decimal CriticalParameterReduction = 50m;
    internal const decimal AttackOverflowPerPoint = 0.0025m;
    internal const decimal BuffOverflowPerPoint = 0.0002m;
    internal const decimal DebuffOverflowPerPoint = 0.001m;

    // Normalize before summing so even maximal decimal stats/weights remain safe.
    // The reference uses weight 2 for ref_status_1 and 1 for each other reference.
    internal static decimal EvaluateWeightedStat(HbrBattleStats stats, HbrStatWeights weights)
    {
        ReadOnlySpan<decimal> values = [stats.Strength, stats.Dexterity, stats.Intelligence,
            stats.Vitality, stats.Spirit, stats.Luck];
        ReadOnlySpan<decimal> factors = [weights.Strength, weights.Dexterity, weights.Intelligence,
            weights.Vitality, weights.Spirit, weights.Luck];
        decimal maximumWeight = 0m;
        for (int i = 0; i < factors.Length; i++) maximumWeight = Math.Max(maximumWeight, factors[i]);
        if (maximumWeight <= 0m) return 0m;

        decimal totalWeight = 0m;
        decimal numerator = 0m;
        decimal maximumStat = 0m;
        bool needsScaledValues = false;
        for (int i = 0; i < values.Length; i++)
        {
            decimal weight = Positive(factors[i]) / maximumWeight;
            if (weight == 0m) continue;
            totalWeight += weight;
            decimal value = Positive(values[i]);
            maximumStat = Math.Max(maximumStat, value);
            decimal contribution = value * weight;
            if (contribution > decimal.MaxValue - numerator) needsScaledValues = true;
            else numerator += contribution;
        }
        if (!needsScaledValues) return numerator / totalWeight;

        // The ordinary sum preserves exact common integer/rational boundaries.
        // Only enormous inputs need a second normalization before averaging.
        decimal scaledNumerator = 0m;
        for (int i = 0; i < values.Length; i++)
            scaledNumerator += (Positive(values[i]) / maximumStat) * (Positive(factors[i]) / maximumWeight);
        return maximumStat * Math.Clamp(scaledNumerator / totalWeight, 0m, 1m);
    }

    internal static decimal EvaluateSkillPower(HbrBattleStats stats, HbrStatWeights weights,
        decimal enemyParameter, HbrSkillPowerCurve curve, bool critical = false)
    {
        decimal defense = Positive(enemyParameter);
        if (critical) defense = Math.Max(0m, defense - CriticalParameterReduction);
        return EvaluateAttackCurve(EvaluateWeightedStat(stats, weights), defense, curve);
    }

    internal static decimal EvaluateAttackCurve(decimal parameter, decimal enemyParameter,
        HbrSkillPowerCurve curve)
    {
        decimal minimum = Positive(curve.MinimumPower);
        decimal maximum = Math.Max(minimum, curve.MaximumPower);
        decimal limit = Positive(curve.ParameterLimit);
        if (limit == 0m) return FloorHundredths(Math.Max(1m, maximum));

        decimal difference = Positive(parameter) - Positive(enemyParameter);
        decimal power;
        if (difference < 0m)
        {
            // Below -limit the reference clamps the basic attack power to 1.
            power = difference <= -limit ? 1m : Math.Max(1m,
                Multiply(minimum, 1m + difference / limit));
        }
        else if (difference < limit)
        {
            power = Add(minimum, Multiply(maximum - minimum, difference / limit));
        }
        else
        {
            power = Multiply(maximum, Add(1m,
                Multiply(difference - limit, AttackOverflowPerPoint)));
        }
        return FloorHundredths(Math.Max(1m, power));
    }

    // The two curve endpoints have different skill-level growth in the reference.
    internal static HbrSkillPowerCurve ScaleAttackCurveForLevel(HbrSkillPowerCurve curve, int level)
    {
        decimal extraLevels = Math.Max(0m, (decimal)level - 1m);
        return new HbrSkillPowerCurve(
            Multiply(Positive(curve.MinimumPower), Add(1m, extraLevels * 0.05m)),
            Multiply(Positive(curve.MaximumPower), Add(1m, extraLevels * 0.02m)),
            Positive(curve.ParameterLimit));
    }

    internal static decimal EvaluateBuffPotency(decimal parameter, HbrSkillPowerCurve curve,
        int level = 1)
    {
        decimal extraLevels = Math.Max(0m, (decimal)level - 1m);
        decimal minimum = Multiply(Positive(curve.MinimumPower), Add(1m, extraLevels * 0.03m));
        decimal maximum = Math.Max(minimum,
            Multiply(Positive(curve.MaximumPower), Add(1m, extraLevels * 0.02m)));
        decimal limit = Positive(curve.ParameterLimit);
        decimal stat = Positive(parameter);
        if (limit == 0m) return maximum;
        return stat < limit
            ? Add(minimum, Multiply(maximum - minimum, stat / limit))
            : Multiply(maximum, Add(1m, Multiply(stat - limit, BuffOverflowPerPoint)));
    }

    internal static decimal EvaluateDebuffPotency(HbrBattleStats stats, HbrStatWeights weights,
        decimal enemyParameter, HbrSkillPowerCurve curve, int level = 1) =>
        EvaluateDebuffPotency(EvaluateWeightedStat(stats, weights), enemyParameter, curve, level);

    internal static decimal EvaluateDebuffPotency(decimal parameter, decimal enemyParameter,
        HbrSkillPowerCurve curve, int level = 1)
    {
        HbrSkillPowerCurve scaled = ScaleAttackCurveForLevel(curve, level);
        decimal minimum = scaled.MinimumPower;
        decimal maximum = Math.Max(minimum, scaled.MaximumPower);
        decimal limit = scaled.ParameterLimit;
        decimal difference = Positive(parameter) - Positive(enemyParameter);
        if (limit == 0m) return maximum;
        if (difference <= 0m) return minimum;
        return difference < limit
            ? Add(minimum, Multiply(maximum - minimum, difference / limit))
            : Multiply(maximum, Add(1m, Multiply(difference - limit, DebuffOverflowPerPoint)));
    }

    // Values are percentage points. Call separately for each compatible buff kind;
    // two attack-up instances and two elemental-up instances are separate slots.
    internal static decimal SumStrongestTwo(ReadOnlySpan<decimal> values)
    {
        decimal first = 0m;
        decimal second = 0m;
        foreach (decimal value in values)
        {
            decimal positive = Positive(value);
            if (positive >= first) { second = first; first = positive; }
            else second = Math.Max(second, positive);
        }
        return Add(first, second);
    }

    // A turn-duration/exclusive effect competes with, rather than adds to, the
    // two ordinary instances of its kind. The larger total is effective.
    internal static decimal SelectStackedPotency(ReadOnlySpan<decimal> stackable,
        ReadOnlySpan<decimal> exclusive)
    {
        decimal selected = SumStrongestTwo(stackable);
        foreach (decimal value in exclusive) selected = Math.Max(selected, value);
        return selected;
    }

    // Returned probability is 0..1; bonusPercent is percentage points, not a ratio.
    internal static decimal CriticalProbability(decimal luck, decimal enemyParameter,
        decimal bonusPercent = 0m)
    {
        decimal difference = Math.Max(0m, Positive(luck) - Positive(enemyParameter));
        decimal naturalPercent = Math.Min(15m, Add(1.5m, Multiply(difference, 0.04m)));
        return Math.Clamp(Add(naturalPercent, bonusPercent), 0m, 100m) / 100m;
    }

    internal static decimal CriticalMultiplier(decimal bonusPercent = 0m) =>
        Math.Max(0m, Add(1.5m, bonusPercent / 100m));

    // Current HBR pools skill/elemental attack-up, mind's eye, and fields in one
    // additive attack bucket; defense-down variants/exposed share a second bucket.
    // Weakness/element/DP-HP eligibility must be checked before adding a potency.
    internal static decimal AdditivePercentMultiplier(ReadOnlySpan<decimal> bonuses)
    {
        decimal total = 0m;
        foreach (decimal bonus in bonuses) total = Add(total, bonus);
        return Math.Max(0m, Add(1m, total / 100m));
    }

    // Destruction gain is based on the enemy coefficient and skill, not damage
    // dealt. Divide between the original hits; extra-hit weights use their own
    // share. The caller advances the rate after a hit has broken/reached HP.
    internal static decimal DestructionGain(decimal enemyCoefficient, decimal skillCoefficient,
        decimal bonusPercent = 0m, decimal resistancePercent = 0m) =>
        Multiply(Multiply(Positive(enemyCoefficient), Positive(skillCoefficient)),
            Multiply(Math.Max(0m, Add(1m, bonusPercent / 100m)),
                Math.Clamp(1m - resistancePercent / 100m, 0m, 1m)));

    internal static decimal AdvanceDestruction(decimal currentPercent, decimal gain,
        decimal maximumPercent) =>
        Math.Min(Math.Max(100m, maximumPercent), Add(Math.Max(100m, currentPercent), Positive(gain)));

    internal static decimal FloorHundredths(decimal value)
    {
        decimal whole = decimal.Truncate(value);
        return whole + decimal.Truncate((value - whole) * 100m) / 100m;
    }

    private static decimal Positive(decimal value) => Math.Max(0m, value);
    private static decimal Add(decimal left, decimal right) =>
        DamageAmplificationLimits.AddSaturated(left, right);
    private static decimal Multiply(decimal left, decimal right) =>
        DamageAmplificationLimits.MultiplySaturated(left, right);
}
