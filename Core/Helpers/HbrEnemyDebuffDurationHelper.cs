using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrEnemyDebuffDurationHelper
{
    // Enemy move-table skills use the original part's upper power value.
    // Player-owned phantoms and explicit card/boss bases do not use this path.
    internal static decimal? BasisForTableMonster(PowerModel power, Creature applier)
    {
        if (!EnemyStatScalingHelper.IsEnemy(applier)) return null;
        decimal basis = (applier.Monster, power) switch
        {
            (DiamondEyeball or CorollaKnife or ArachneLine, HbrDefenseDownPower) => 30m,
            (SlimySerpent, HbrAttackDownPower) => 30m,
            (BarrelWalker, HbrDefenseDownPower) => 25m,
            (ShrimpCrab, HbrAttackDownPower or HbrDefenseDownPower) => 20m,
            _ => 0m
        };
        return basis > 0m ? basis * AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies, 125, 100) / 100m : null;
    }

    internal static HbrEffectProfile? ForTableMonster(PowerModel power, Creature applier)
    {
        if (power is not (HbrAttackDownPower or HbrDefenseDownPower) || !EnemyStatScalingHelper.IsEnemy(applier)) return null;
        int count = applier.Monster switch
        {
            BarrelWalker or ShrimpCrab => 4,
            DiamondEyeball or CorollaKnife or ArachneLine or SlimySerpent => 2,
            _ => 0
        };
        return count > 0 ? new(HbrEffectLimit.None, HbrEffectExit.EnemyTurnEnd, count) : null;
    }
}
