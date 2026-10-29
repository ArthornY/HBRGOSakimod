using System;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// Only explicitly fixed HP/DP amounts cross this boundary. Percentage effects,
// damage already resolved by the skill pipeline, phase resets, transfers and
// serialized resource ledgers are already in combat units.
internal static class HbrResourceValueHelper
{
    internal const decimal FixedResourceScale = 100m;

    internal static decimal Fixed(Creature source, decimal amount)
    {
        if (!HbrBattleStatsHelper.IsEnabled(source)) return amount;
        decimal bounded = Math.Clamp(amount, -BigCombatNumbers.MaximumCreatureStat,
            BigCombatNumbers.MaximumCreatureStat);
        decimal scaled = BigCombatNumbers.MultiplyPositive(Math.Abs(bounded), FixedResourceScale);
        return bounded < 0m ? -scaled : scaled;
    }

    // DP's native integer ledgers have a smaller limit than exact HP resources.
    internal static int FixedInt(Creature source, decimal amount) =>
        (int)Math.Clamp(Fixed(source, amount), int.MinValue, int.MaxValue);

    internal static decimal ConvertFixedRecovery(Creature source, decimal amount) => Fixed(source, amount);
}
