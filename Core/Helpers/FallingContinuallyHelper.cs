using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class FallingContinuallyHelper
{
    // Use block's base units, then the same INT/LUCK curve as other debuffs.
    // Native power commands still own application modifiers and stacking.
    internal static decimal ResolveAmount(decimal nativeUnits, Creature applier, Creature target) =>
        HbrBattleStatsHelper.TryGetStats(applier, out HbrBattleStats stats)
            ? HbrPercentDebuffHelper.EvaluateBasePotency(
                HbrSkillPower.BlockBaseFromUnits(nativeUnits), stats.Intelligence,
                stats.Luck, HbrEnemyProfileHelper.GetEnemyParameter(target))
            : nativeUnits;
}
