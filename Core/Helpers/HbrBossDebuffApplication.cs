using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// These boss commands authored their argument as a vanilla duration, not an
// HBR strength modifier. Enemy casters use the original Weak/Vulnerable strength;
// phantom replay retains 15%. The native first enemy-end skip stays in the profile.
internal static class HbrBossDebuffApplication
{
    internal static HbrEffectProfile Profile(Creature target, decimal turns) => turns >= 99m
        ? new(HbrEffectLimit.Default, HbrEffectExit.Eternal, 1)
        : new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd,
            (int)Math.Clamp(decimal.Truncate(turns), 1m, int.MaxValue - 1m) +
            (EnemyStatScalingHelper.IsEnemy(target) ? 0 : 1));

    internal static Task ApplyAttackDown(PlayerChoiceContext context, Creature target,
        decimal turns, Creature applier, CardModel source = null) =>
        HbrBattleStatsHelper.IsEnabled(target)
            ? HbrPercentDebuffApplication.ApplyBasis<HbrAttackDownPower>(context, target,
                EnemyStatScalingHelper.IsEnemy(applier) ? 25m : 15m, applier, source, Profile(target, turns))
            : HbrPercentDebuffApplication.ApplyAttackDown(context, target, turns, applier, source);

    internal static Task ApplyDefenseDown(PlayerChoiceContext context, Creature target,
        decimal turns, Creature applier, CardModel source = null) =>
        HbrBattleStatsHelper.IsEnabled(target)
            ? HbrPercentDebuffApplication.ApplyBasis<HbrDefenseDownPower>(context, target,
                EnemyStatScalingHelper.IsEnemy(applier) ? 50m : 15m, applier, source, Profile(target, turns))
            : HbrPercentDebuffApplication.ApplyDefenseDown(context, target, turns, applier, source);

    internal static async Task ApplyAttackDown(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal turns, Creature applier, CardModel source = null)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets) await ApplyAttackDown(context, target, turns, applier, source);
    }

    internal static async Task ApplyDefenseDown(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal turns, Creature applier, CardModel source = null)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets) await ApplyDefenseDown(context, target, turns, applier, source);
    }
}
