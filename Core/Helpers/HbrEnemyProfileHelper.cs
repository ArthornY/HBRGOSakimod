using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

// Enemy ability E is independent of the six player stats and resource pools.
// The original calculator stores enemy_stat, max_hp and max_dp separately:
// hbr-tool/damage-calculator ead9f319, src/data/enemyList.js.
// Until encounters have individually verified source rows, these are explicit
// STS map-calibration values, not original HBR enemy data.
internal static class HbrEnemyProfileHelper
{
    internal const decimal InitialEnemyParameter = 150m;
    internal const decimal GainPerAct = 60m;
    internal const decimal GainPerFloor = 6m;
    internal const decimal EliteBonus = 30m;
    internal const decimal BossBonus = 50m;

    internal static decimal GetEnemyParameter(Creature creature)
    {
        decimal baseline = GetBaseEnemyParameter(creature);
        if (!EnemyStatScalingHelper.IsEnemy(creature) || !HbrBattleStatsHelper.IsEnabled(creature))
            return baseline;
        return Math.Max(0m, baseline - HbrCalamityPower.GetTotalReduction(creature) -
            FearOfDevilPower.GetTotalReduction(creature));
    }

    // Progression remains independent of temporary debuffs and resource pools.
    internal static decimal GetBaseEnemyParameter(Creature creature)
    {
        // Every enemy, including Hopper, shares map growth. The first normal
        // room starts at the authored opening benchmark, without a floor bonus.
        if (creature?.CombatState?.RunState is not { } run) return InitialEnemyParameter;
        int act = Math.Max(0, run.CurrentActIndex);
        // Native map entry defines ActFloor as row + 1. Its saved/current
        // value is the fallback; act lengths vary, especially in multiplayer.
        int localFloor = run.CurrentMapCoord is { } coord ? Math.Max(1, coord.row + 1) :
            Math.Max(1, run.ActFloor);
        // Completed acts retain their floor growth. Native history is saved per
        // act and survives the coordinate/ActFloor reset on entering a new act.
        // Each act's first floor has no floor bonus; current-act history must
        // not be added again on top of the live coordinate/fallback above.
        decimal previousFloorGrowth = 0m;
        var history = run.MapPointHistory;
        for (int index = 0; index < Math.Min(act, history?.Count ?? 0); index++)
            previousFloorGrowth += Math.Max(0, (history[index]?.Count ?? 0) - 1);
        RoomType room = creature.CombatState.Encounter?.RoomType ??
            run.CurrentRoom?.RoomType ?? RoomType.Monster;
        return InitialEnemyParameter + GainPerAct * act +
            GainPerFloor * (previousFloorGrowth + localFloor - 1) +
            (room == RoomType.Boss ? BossBonus : room == RoomType.Elite ? EliteBonus : 0m);
    }
}
