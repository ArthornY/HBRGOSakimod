using System;
using HBRGOSakiMod.Core.Models.Monsters;
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
    internal static decimal GetEnemyParameter(Creature creature)
    {
        // Authored opening benchmark, independent of map growth and HP rolls.
        // A summoned allied copy must continue to use its owner's stats.
        if (creature?.Monster is SmallHopper && EnemyStatScalingHelper.IsEnemy(creature) &&
            HbrBattleStatsHelper.IsEnabled(creature)) return 150m;
        if (creature?.CombatState?.RunState is not { } run) return 170m;
        int act = Math.Max(0, run.CurrentActIndex);
        int localFloor = run.CurrentMapCoord is { } coord ? Math.Max(1, coord.row + 1) :
            Math.Max(1, run.TotalFloor - act * 17);
        RoomType room = creature.CombatState.Encounter?.RoomType ??
            run.CurrentRoom?.RoomType ?? RoomType.Monster;
        return 170m + 60m * act + 3m * localFloor +
            (room == RoomType.Boss ? 25m : room == RoomType.Elite ? 15m : 0m);
    }
}
