using System;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// Limited bridge for explicitly fixed recovery values. These curve endpoints
// are test profile choices, not data for a named skill in the original game.
// Never use for percent HP, phase resets, or recovery derived from actual damage.
internal static class HbrResourceValueHelper
{
    private static readonly HbrSkillPowerCurve FixedRecoveryCurve = new(0.8m, 1.2m, 300m);

    internal static decimal ConvertFixedRecovery(Creature source, decimal amount)
    {
        if (!HbrBattleStatsHelper.TryGetStats(source, out HbrBattleStats stats)) return amount;
        if (amount <= 0m) return 0m;
        decimal potency = HbrOriginalNumericFormula.EvaluateBuffPotency(stats.Intelligence,
            FixedRecoveryCurve);
        decimal multiplier = BigCombatNumbers.MultiplyPositive(HbrBattleStatsHelper.PlayerHealthScale, potency);
        decimal recovery = BigCombatNumbers.MultiplyPositive(amount, multiplier);
        // Remove interpolation residue at exact integral boundaries before the
        // engine commits whole HP/DP. Saturation happens before multiplication.
        return BigCombatNumbers.ClampStat(decimal.Round(recovery, 8, MidpointRounding.ToEven));
    }
}
