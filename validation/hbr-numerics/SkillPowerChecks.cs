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

        Equal("damage 100% base", HbrSkillPower.DamageBaseFromUnits(HbrSkillPower.DamageUnits(100m)), 6000m);
        Equal("block 100% base", HbrSkillPower.BlockBaseFromUnits(HbrSkillPower.BlockUnits(100m)), 500m);
        Equal("block 100% empty intelligence", HbrSkillPower.EvaluateBlockFromUnits(5m, 0m), 0m);
        Equal("block 100% initial intelligence", HbrSkillPower.EvaluateBlockFromUnits(5m, 100m), 500m);
        Equal("block 100% intelligence doubled", HbrSkillPower.EvaluateBlockFromUnits(5m, 200m), 1000m);
        Equal("block 160% initial intelligence", HbrSkillPower.EvaluateBlockFromUnits(8m, 100m), 800m);

        // Three distinct hit coefficients remain three damage events. This
        // checks power allocation only, and deliberately does not merge hits.
        decimal perHit = HbrSkillPower.DamageUnits(100m, 3m);
        Equal("three-hit native coefficient total", perHit + perHit + perHit, 6m);
        Equal("three-hit base total", HbrSkillPower.DamageBaseFromUnits(perHit) * 3m, 6000m);

        Equal("alternate damage base unit scale", HbrSkillPower.DamageBaseFromUnits(6m, 100m), 600m);
        Equal("enemy block native-unit scale", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, 300m), 6000m);
        Equal("negative skill base clamped", HbrSkillPower.DamageBaseFromUnits(-6m), 0m);
        Equal("negative skill block clamped", HbrSkillPower.EvaluateBlockFromUnits(-5m, 300m), 0m);
        Equal("negative intelligence clamped", HbrSkillPower.EvaluateBlockFromUnits(5m, -1m), 0m);
        Equal("negative damage scale clamped", HbrSkillPower.DamageBaseFromUnits(6m, -100m), 0m);
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
        Equal("damage base saturation", HbrSkillPower.DamageBaseFromUnits(decimal.MaxValue), decimal.MaxValue);
        Equal("block saturation", HbrSkillPower.EvaluateBlockFromUnits(decimal.MaxValue, decimal.MaxValue), decimal.MaxValue);
        InvalidDivisor("zero damage divisor rejected", () => HbrSkillPower.DamageUnits(100m, 0m));
        InvalidDivisor("negative damage divisor rejected", () => HbrSkillPower.DamageUnits(100m, -1m));
        InvalidDivisor("zero block divisor rejected", () => HbrSkillPower.BlockUnits(100m, 0m));
        InvalidDivisor("negative block divisor rejected", () => HbrSkillPower.BlockUnits(100m, -1m));
        SingleMultiplierChecks.Run(check);
    }
}
