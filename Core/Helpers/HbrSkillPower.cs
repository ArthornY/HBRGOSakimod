using System;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Skill-coefficient policy for the STS adaptation, independent of game state.
/// Damage 100% is the old six-point strike; block 100% is the old five-point
/// defend. Native units are retained at the calculation boundary so a displayed
/// repeating percentage is never rounded and fed back into combat calculations.
/// This is a bridge profile, not a catalog of original HBR skill coefficients.
/// </summary>
internal static class HbrSkillPower
{
    internal const decimal LegacyDamageAt100Percent = 6m;
    internal const decimal LegacyBlockAt100Percent = 5m;
    internal const decimal OutgoingDamageUnit = 1000m;
    internal const decimal PlayerBlockUnit = 100m;
    internal const decimal ParameterLimit = 140m;
    internal static readonly HbrSkillPowerCurve BlockPotencyCurve = new(0.8m, 1.2m, 300m);

    // Keep a repeating percentage as a pair: e.g. 50% / 3 is exactly one native
    // damage unit, whereas a previously rounded 16.666...% cannot round-trip.
    // These are per-hit coefficients; hit count remains the caller's concern.
    internal static decimal DamageUnits(decimal percent, decimal divisor = 1m) =>
        PercentToUnits(percent, divisor, LegacyDamageAt100Percent / 100m);

    internal static decimal BlockUnits(decimal percent, decimal divisor = 1m) =>
        PercentToUnits(percent, divisor, LegacyBlockAt100Percent / 100m);

    // Presentation only. Preserve signed values for upgrade deltas, but never
    // use these decimal percentages to reconstruct a native-unit skill value.
    internal static decimal DamagePercent(decimal nativeUnits) =>
        DivideSaturated(nativeUnits, LegacyDamageAt100Percent / 100m);

    internal static decimal BlockPercent(decimal nativeUnits) =>
        DivideSaturated(nativeUnits, LegacyBlockAt100Percent / 100m);

    internal static HbrSkillPowerCurve DamageCurveFromUnits(decimal nativeUnits,
        decimal unitScale = OutgoingDamageUnit)
    {
        decimal minimum = DamageAmplificationLimits.MultiplySaturated(
            Math.Max(0m, nativeUnits), Math.Max(0m, unitScale));
        return new(minimum,
            DamageAmplificationLimits.MultiplySaturated(minimum, 2m), ParameterLimit);
    }

    internal static decimal BlockBaseFromUnits(decimal nativeUnits,
        decimal unitScale = PlayerBlockUnit) =>
        DamageAmplificationLimits.MultiplySaturated(
            Math.Max(0m, nativeUnits), Math.Max(0m, unitScale));

    internal static decimal EvaluateBlockFromUnits(decimal nativeUnits, decimal intelligence,
        decimal unitScale = PlayerBlockUnit)
    {
        decimal support = HbrOriginalNumericFormula.EvaluateBuffPotency(
            intelligence, BlockPotencyCurve);
        return decimal.Floor(DamageAmplificationLimits.MultiplySaturated(
            BlockBaseFromUnits(nativeUnits, unitScale), support));
    }

    private static decimal PercentToUnits(decimal percent, decimal divisor, decimal unitPerPercent)
    {
        if (divisor <= 0m) throw new ArgumentOutOfRangeException(nameof(divisor),
            "A skill percentage divisor must be positive.");

        // The common route multiplies before division. Both unit-per-percent
        // factors are below one, so this multiplication cannot overflow.
        // Tiny decimal fractions need the reverse order to avoid underflow
        // before division restores their magnitude. Here |percent| <= 1 and
        // divisor >= 1e-28, so the intermediate quotient also fits Decimal.
        if (divisor < 1m && Math.Abs(percent) <= 1m)
            return percent / divisor * unitPerPercent;
        return DivideSaturated(percent * unitPerPercent, divisor);
    }

    private static decimal DivideSaturated(decimal value, decimal divisor)
    {
        // All callers supply a positive divisor. Compare before division so
        // malformed/extreme configuration values cannot overflow Decimal.
        if (divisor < 1m && Math.Abs(value) >= decimal.MaxValue * divisor)
            return value < 0m ? decimal.MinValue : decimal.MaxValue;
        return value / divisor;
    }
}
