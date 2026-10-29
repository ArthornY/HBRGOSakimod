using HBRGOSakiMod.Core.Helpers;

// STS adaptation policy. These expected values do not use the original HBR
// two-endpoint curve, which is still tested independently by the saved vectors.
internal static class SingleMultiplierChecks
{
    internal static void Run(Action<string, decimal, decimal, decimal> check)
    {
        void Equal(string name, decimal actual, decimal expected) =>
            check($"single multiplier {name}", actual, expected, 0m);

        // Choose stat differences whose /140 factors are exact halves or
        // integers. This table also crosses the old positive overflow border.
        (decimal parameter, decimal enemy, decimal factor)[] damageFactors =
        [
            (0m, 0m, 1m), (100m, 100m, 1m), (130m, 200m, 0.5m),
            (200m, 200m, 1m), (270m, 200m, 1.5m), (340m, 200m, 2m),
            (480m, 200m, 3m), (900m, 200m, 6m)
        ];
        decimal[] nativeUnits = [0.25m, 0.5m, 1m, 2m, 3m, 4m, 5m, 6m, 8m, 9m, 14m, 77m];
        foreach (decimal units in nativeUnits)
        {
            Equal($"native {units} fixed base", HbrSkillPower.DamageBaseFromUnits(units), units * 1000m);
            foreach (var sample in damageFactors)
                Equal($"native {units} damage A={sample.parameter} E={sample.enemy}",
                    HbrSkillPower.EvaluateDamageFromUnits(units, sample.parameter, sample.enemy),
                    units * 1000m * sample.factor);

            // Intelligence 100 is the base; there is no base 80% support floor,
            // former 300-point endpoint, or extra overflow-growth branch.
            foreach (decimal intelligence in new decimal[] { 0m, 50m, 100m, 200m, 300m, 500m, 900m })
                Equal($"native {units} block INT={intelligence}",
                    HbrSkillPower.EvaluateBlockFromUnits(units, intelligence),
                    decimal.Floor(units * intelligence));
        }

        Equal("starter strike against opening E150", HbrSkillPower.EvaluateDamageFromUnits(6m, 100m, 150m), 3857.14m);
        Equal("upgraded strike against opening E150", HbrSkillPower.EvaluateDamageFromUnits(9m, 100m, 150m), 5785.71m);
        Equal("100% damage exact baseline", HbrSkillPower.EvaluateDamageFromUnits(HbrSkillPower.DamageUnits(100m), 200m, 200m), 6000m);
        Equal("150% damage exact baseline", HbrSkillPower.EvaluateDamageFromUnits(HbrSkillPower.DamageUnits(150m), 200m, 200m), 9000m);
        Equal("100% block exact baseline", HbrSkillPower.EvaluateBlockFromUnits(HbrSkillPower.BlockUnits(100m), 100m), 500m);
        Equal("160% block exact baseline", HbrSkillPower.EvaluateBlockFromUnits(HbrSkillPower.BlockUnits(160m), 100m), 800m);

        Equal("positive one-point difference truncates", HbrSkillPower.EvaluateDamageFromUnits(6m, 201m, 200m), 6042.85m);
        Equal("negative one-point difference truncates", HbrSkillPower.EvaluateDamageFromUnits(6m, 199m, 200m), 5957.14m);
        Equal("high stat remains linear", HbrSkillPower.EvaluateDamageFromUnits(6m, 1200m, 200m), 48857.14m);
        Equal("fractional coefficient remains fractional", HbrSkillPower.EvaluateDamageFromUnits(0.00123456789m, 141m, 140m), 1.24m);
        Equal("fractional block rounds down", HbrSkillPower.EvaluateBlockFromUnits(0.12345m, 123m), 15m);
        Equal("small block has no damage-style minimum", HbrSkillPower.EvaluateBlockFromUnits(0.001m, 99m), 0m);

        Equal("damage difference at negative border", HbrSkillPower.EvaluateDamageFromUnits(6m, 60m, 200m), 1m);
        Equal("damage difference below negative border", HbrSkillPower.EvaluateDamageFromUnits(6m, 0m, 200m), 1m);
        Equal("small positive damage minimum", HbrSkillPower.EvaluateDamageFromUnits(0.0000001m, 200m, 200m), 1m);
        Equal("zero damage stays zero", HbrSkillPower.EvaluateDamageFromUnits(0m, 200m, 200m), 0m);
        Equal("negative damage stays zero", HbrSkillPower.EvaluateDamageFromUnits(-6m, 200m, 200m), 0m);
        Equal("zero damage stays zero while critical", HbrSkillPower.EvaluateDamageFromUnits(0m, 200m, 200m, true), 0m);
        Equal("negative parameter clamps before subtraction", HbrSkillPower.EvaluateDamageFromUnits(6m, -10m, 70m), 3000m);
        Equal("negative enemy clamps before subtraction", HbrSkillPower.EvaluateDamageFromUnits(6m, 70m, -10m), 9000m);
        Equal("both negative parameters clamp", HbrSkillPower.EvaluateDamageFromUnits(6m, decimal.MinValue, decimal.MinValue), 6000m);
        Equal("negative block input clamps", HbrSkillPower.EvaluateBlockFromUnits(-5m, 100m), 0m);
        Equal("negative intelligence clamps", HbrSkillPower.EvaluateBlockFromUnits(5m, decimal.MinValue), 0m);

        // Critical's final 1.5 multiplier belongs to the caller. This helper
        // only lowers the enemy parameter by 50, without crossing below zero.
        Equal("critical E150 becomes E100", HbrSkillPower.EvaluateDamageFromUnits(6m, 100m, 150m, true), 6000m);
        Equal("critical E50 reaches zero", HbrSkillPower.EvaluateDamageFromUnits(6m, 0m, 50m, true), 6000m);
        Equal("critical E25 clamps to zero", HbrSkillPower.EvaluateDamageFromUnits(6m, 20m, 25m, true), 6857.14m);
        Equal("critical negative E clamps", HbrSkillPower.EvaluateDamageFromUnits(6m, 0m, -25m, true), 6000m);
        Equal("critical is not multiplied by 1.5 here", HbrSkillPower.EvaluateDamageFromUnits(6m, 200m, 250m, true), 6000m);
        Equal("critical caller final multiplier", HbrSkillPower.EvaluateDamageFromUnits(6m, 100m, 150m, true) * HbrOriginalNumericFormula.CriticalMultiplier(), 9000m);

        decimal perHitUnits = HbrSkillPower.DamageUnits(100m, 3m);
        Equal("three distinct equal-stat hits conserve base", HbrSkillPower.EvaluateDamageFromUnits(perHitUnits, 200m, 200m) * 3m, 6000m);
        Equal("three distinct fractional hits truncate individually", HbrSkillPower.EvaluateDamageFromUnits(perHitUnits, 100m, 150m) * 3m, 3857.13m);
        Equal("three low-power hits retain each minimum", HbrSkillPower.EvaluateDamageFromUnits(0.0000001m, 0m, 1000m) * 3m, 3m);

        Equal("damage maximum unit base saturates", HbrSkillPower.DamageBaseFromUnits(decimal.MaxValue), decimal.MaxValue);
        Equal("damage maximum unit value equal stats", HbrSkillPower.EvaluateDamageFromUnits(decimal.MaxValue, 200m, 200m), decimal.MaxValue);
        Equal("damage maximum stat factor saturates", HbrSkillPower.EvaluateDamageFromUnits(6m, decimal.MaxValue, 0m), decimal.MaxValue);
        Equal("damage equal maximum stats cancel", HbrSkillPower.EvaluateDamageFromUnits(6m, decimal.MaxValue, decimal.MaxValue), 6000m);
        Equal("damage maximum defense minimum", HbrSkillPower.EvaluateDamageFromUnits(6m, 0m, decimal.MaxValue), 1m);
        Equal("damage maximum units zero coefficient minimum", HbrSkillPower.EvaluateDamageFromUnits(decimal.MaxValue, 0m, decimal.MaxValue), 1m);
        Equal("zero damage huge stats", HbrSkillPower.EvaluateDamageFromUnits(0m, decimal.MaxValue, 0m), 0m);
        Equal("negative damage extreme input", HbrSkillPower.EvaluateDamageFromUnits(decimal.MinValue, decimal.MaxValue, 0m), 0m);
        Equal("tiny base survives scale", HbrSkillPower.DamageBaseFromUnits(0.0000000000000000000000000001m), 0.0000000000000000000000001m);
        Equal("block maximum input saturates", HbrSkillPower.EvaluateBlockFromUnits(decimal.MaxValue, decimal.MaxValue), decimal.MaxValue);
        Equal("zero block huge stats", HbrSkillPower.EvaluateBlockFromUnits(0m, decimal.MaxValue), 0m);

        // Incoming/enemy support remains a separate policy and retains the
        // original 0.8..1.2 curve. A player-side refactor must not rescale it.
        Equal("enemy block zero intelligence", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, 0m), 4000m);
        Equal("enemy block INT200", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, 200m), 5333m);
        Equal("enemy block INT300", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, 300m), 6000m);
        Equal("enemy block old overflow", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, 500m), 6240m);
        Equal("enemy block negative intelligence clamps", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, -1m), 4000m);
        Equal("enemy block custom scale", HbrSkillPower.EvaluateEnemyBlockFromUnits(5m, 300m, 100m), 600m);
        Equal("enemy block negative native units", HbrSkillPower.EvaluateEnemyBlockFromUnits(-5m, 300m), 0m);
    }
}
