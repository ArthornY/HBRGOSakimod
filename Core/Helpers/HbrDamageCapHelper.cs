using System;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Native stacks are percentages in HBR mode. Keep them raw in saves/clones;
// actual HP lost this turn remains in combat units. Fusion caps already carry
// their own health-relative meaning and must not be reinterpreted as stacks.
internal static class HbrDamageCapHelper
{
    internal static bool TryGetMaximum(PowerModel power, out decimal maximum)
    {
        maximum = 0m;
        if (power is not (HardToKillPower or HardenedShellPower) || !power.IsMutable)
            return false;
        if (PhantomHealthDamageCapHelper.TryGetHealthBasedMaximum(power, out maximum))
            return maximum > BigCombatNumbers.NativeStatCap;
        if (!HbrBattleStatsHelper.IsEnabled(power.Owner)) return false;
        maximum = GetPercentMaximum(power.Owner, power.Amount);
        return true;
    }

    internal static bool TryGetNativePercent(PowerModel power, out int percent)
    {
        percent = 0;
        if (power is not (HardToKillPower or HardenedShellPower) || !power.IsMutable ||
            !HbrBattleStatsHelper.IsEnabled(power.Owner) ||
            PhantomHealthDamageCapHelper.TryGetHealthBasedMaximum(power, out _)) return false;
        percent = power.Amount;
        return true;
    }

    internal static decimal GetPercentMaximum(Creature owner, int percent)
    {
        if (percent <= 0) return 0m;
        // Clamp HP before multiplying. The resource limit times Int32.MaxValue
        // fits decimal, while an unbounded saved value need not. Clamp only the
        // final percentage result, preserving ratios above 100% correctly.
        decimal maximumHp = BigCombatNumbers.ClampStat(LargeCreatureHealthHelper.GetMaxHp(owner));
        return Math.Max(1m, BigCombatNumbers.ClampStat(maximumHp * percent / 100m));
    }
}
