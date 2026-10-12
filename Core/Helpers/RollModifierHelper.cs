using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Helpers;

internal static class RollModifierHelper
{
    internal static SoulRollBreakdown Apply(
        SoulRollBreakdown roll,
        IReadOnlyList<SoulRollBonusDetail> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
            return roll;

        List<SoulRollBonusDetail> details = new(roll.Bonuses);
        decimal basis = roll.FinalRoll;
        foreach (SoulRollBonusDetail modifier in modifiers.Where(value => !value.IsPercentage))
        {
            basis += modifier.Amount;
            details.Add(modifier);
        }

        decimal percent = 0m;
        int result = ToRoll(basis);
        foreach (SoulRollBonusDetail modifier in modifiers.Where(value => value.IsPercentage))
        {
            percent += modifier.Amount;
            // Every percentage uses the same basis. Cumulative rounding only
            // allocates whole-point history entries; it never compounds bonuses.
            int adjusted = ToRoll(basis * (1m + percent / 100m));
            details.Add(new SoulRollBonusDetail(
                (int)Math.Clamp((long)adjusted - result, int.MinValue, int.MaxValue),
                $"{modifier.Source}（{modifier.Amount:+#;-#;0}%）"));
            result = adjusted;
        }

        return new SoulRollBreakdown(roll.BaseRoll, result, roll.DiceSource, details)
        {
            IsForcedCritical = roll.IsForcedCritical
        };
    }

    private static int ToRoll(decimal value) =>
        (int)Math.Clamp(decimal.Floor(value), int.MinValue, int.MaxValue);
}
