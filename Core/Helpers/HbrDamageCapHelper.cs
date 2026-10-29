using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Keep native counters in saves and clones. Only fixed HP-unit thresholds are
// adapted at their consumers; actual damage received this turn is never scaled.
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
        maximum = BigCombatNumbers.ClampStat(HbrResourceValueHelper.Fixed(power.Owner, power.Amount));
        return true;
    }
}
