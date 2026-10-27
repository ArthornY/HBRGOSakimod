using HBRGOSakiMod.Core.Helpers;

internal static class SkillPowerChecks
{
    internal static void Run(Action<string, decimal, decimal, decimal> check)
    {
        void Equal(string name, decimal actual, decimal expected) =>
            check($"skill coefficient {name}", actual, expected, 0m);
        void InvalidDivisor(string name, Action action)
        {
            bool rejected = false;
            try { action(); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Equal(name, rejected ? 1m : 0m, 1m);
        }

        Equal("damage 100%", HbrSkillPower.DamageUnits(100m), 6m);
        Equal("damage 150% upgrade", HbrSkillPower.DamageUnits(150m), 9m);
        Equal("damage half", HbrSkillPower.DamageUnits(50m), 3m);
        Equal("damage exact sixth", HbrSkillPower.DamageUnits(50m, 3m), 1m);
        Equal("damage exact third", HbrSkillPower.DamageUnits(100m, 3m), 2m);
        Equal("damage exact seventh", HbrSkillPower.DamageUnits(350m, 7m), 3m);
        Equal("damage signed upgrade delta", HbrSkillPower.DamageUnits(-50m), -3m);
        Equal("damage zero", HbrSkillPower.DamageUnits(0m), 0m);
        Equal("block 100%", HbrSkillPower.BlockUnits(100m), 5m);
        Equal("block 160% upgrade", HbrSkillPower.BlockUnits(160m), 8m);
        Equal("block exact third", HbrSkillPower.BlockUnits(60m, 3m), 1m);
        Equal("block signed upgrade delta", HbrSkillPower.BlockUnits(-40m), -2m);
        Equal("damage display native six", HbrSkillPower.DamagePercent(6m), 100m);
        Equal("damage display upgrade nine", HbrSkillPower.DamagePercent(9m), 150m);
        Equal("block display native five", HbrSkillPower.BlockPercent(5m), 100m);
        Equal("block display upgrade eight", HbrSkillPower.BlockPercent(8m), 160m);
        Equal("signed display", HbrSkillPower.DamagePercent(-3m), -50m);
        Equal("native one display sixth", HbrSkillPower.DamagePercent(1m), 100m / 6m);

        HbrSkillPowerCurve standard = HbrSkillPower.DamageCurveFromUnits(HbrSkillPower.DamageUnits(100m));
        Equal("damage curve minimum", standard.MinimumPower, 6000m);
        Equal("damage curve maximum", standard.MaximumPower, 12000m);
        Equal("damage curve threshold", standard.ParameterLimit, 140m);
        Equal("block 100% base", HbrSkillPower.BlockBaseFromUnits(HbrSkillPower.BlockUnits(100m)), 500m);
        Equal("block 100% empty intelligence", HbrSkillPower.EvaluateBlockFromUnits(5m, 0m), 400m);
        Equal("block 100% initial intelligence", HbrSkillPower.EvaluateBlockFromUnits(5m, 200m), 533m);
        Equal("block 100% curve limit", HbrSkillPower.EvaluateBlockFromUnits(5m, 300m), 600m);
        Equal("block 160% initial intelligence", HbrSkillPower.EvaluateBlockFromUnits(8m, 200m), 853m);

        // Three distinct hit coefficients remain three damage events. This
        // checks power allocation only, and deliberately does not merge hits.
        decimal perHit = HbrSkillPower.DamageUnits(100m, 3m);
        Equal("three-hit native coefficient total", perHit + perHit + perHit, 6m);
        Equal("three-hit curve total", HbrSkillPower.DamageCurveFromUnits(perHit).MinimumPower * 3m, 6000m);

        // Equivalence against the existing native-unit damage/block boundary,
        // including dynamic fractional values and small cannon base values.
        decimal[] units = [0.25m, 0.5m, 1m, 2m, 3m, 4m, 5m, 6m, 8m, 9m, 14m, 77m];
        decimal[] stats = [0m, 170m, 200m, 300m, 500m];
        foreach (decimal native in units)
        {
            HbrSkillPowerCurve actual = HbrSkillPower.DamageCurveFromUnits(native);
            HbrSkillPowerCurve legacy = new(native * 1000m, native * 2000m, 140m);
            Equal($"native {native} exact curve minimum", actual.MinimumPower, legacy.MinimumPower);
            Equal($"native {native} exact curve maximum", actual.MaximumPower, legacy.MaximumPower);
            foreach (decimal stat in stats)
            {
                Equal($"native {native} attack at {stat}",
                    HbrOriginalNumericFormula.EvaluateAttackCurve(stat, 200m, actual),
                    HbrOriginalNumericFormula.EvaluateAttackCurve(stat, 200m, legacy));
                Equal($"native {native} block at {stat}",
                    HbrSkillPower.EvaluateBlockFromUnits(native, stat),
                    decimal.Floor(native * 100m * HbrOriginalNumericFormula.EvaluateBuffPotency(
                        stat, new HbrSkillPowerCurve(0.8m, 1.2m, 300m))));
            }
        }

        Equal("incoming native-unit scale", HbrSkillPower.DamageCurveFromUnits(6m, 100m).MinimumPower, 600m);
        Equal("enemy block native-unit scale", HbrSkillPower.EvaluateBlockFromUnits(5m, 300m, 1000m), 6000m);
        Equal("negative skill curve clamped", HbrSkillPower.DamageCurveFromUnits(-6m).MinimumPower, 0m);
        Equal("negative skill block clamped", HbrSkillPower.EvaluateBlockFromUnits(-5m, 300m), 0m);
        Equal("negative intelligence clamped", HbrSkillPower.EvaluateBlockFromUnits(5m, -1m), 400m);
        Equal("negative damage scale clamped", HbrSkillPower.DamageCurveFromUnits(6m, -100m).MaximumPower, 0m);
        Equal("negative block scale clamped", HbrSkillPower.BlockBaseFromUnits(5m, -100m), 0m);
        Equal("display damage saturation", HbrSkillPower.DamagePercent(decimal.MaxValue), decimal.MaxValue);
        Equal("display block saturation", HbrSkillPower.BlockPercent(decimal.MinValue), decimal.MinValue);
        Equal("coefficient damage saturation", HbrSkillPower.DamageUnits(decimal.MaxValue, 0.001m), decimal.MaxValue);
        Equal("coefficient block saturation", HbrSkillPower.BlockUnits(decimal.MinValue, 0.001m), decimal.MinValue);
        Equal("tiny decimal damage restored", HbrSkillPower.DamageUnits(0.0000000000000000000000000001m,
            0.0000000000000000000000000001m), 0.06m);
        Equal("tiny decimal block restored", HbrSkillPower.BlockUnits(0.0000000000000000000000000001m,
            0.0000000000000000000000000001m), 0.05m);
        Equal("coefficient large denominator", HbrSkillPower.DamageUnits(decimal.MaxValue, decimal.MaxValue), 0.06m);
        Equal("curve minimum saturation", HbrSkillPower.DamageCurveFromUnits(decimal.MaxValue).MinimumPower, decimal.MaxValue);
        Equal("curve maximum saturation", HbrSkillPower.DamageCurveFromUnits(decimal.MaxValue).MaximumPower, decimal.MaxValue);
        Equal("block saturation", HbrSkillPower.EvaluateBlockFromUnits(decimal.MaxValue, decimal.MaxValue), decimal.MaxValue);
        InvalidDivisor("zero damage divisor rejected", () => HbrSkillPower.DamageUnits(100m, 0m));
        InvalidDivisor("negative damage divisor rejected", () => HbrSkillPower.DamageUnits(100m, -1m));
        InvalidDivisor("zero block divisor rejected", () => HbrSkillPower.BlockUnits(100m, 0m));
        InvalidDivisor("negative block divisor rejected", () => HbrSkillPower.BlockUnits(100m, -1m));
    }
}
