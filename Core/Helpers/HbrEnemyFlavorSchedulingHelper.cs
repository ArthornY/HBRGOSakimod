using System.Linq;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed record HbrEnemyFlavorChoice(string MoveId, bool PauseTable = false);

internal static class HbrEnemyFlavorSchedulingHelper
{
    internal static void RefreshOwner(Creature owner)
    {
        if (owner?.Monster is not HbrgoMoveTableMonster table || !table.HasFlavorMechanics ||
            owner.IsAlive != true || owner.CombatState == null) return;
        table.RefreshFlavorIntent();
        CombatPreviewRefreshHelper.RefreshCombatState(owner, nameof(HbrEnemyFlavorSchedulingHelper));
    }
    internal static void OnOverclockLevelChanged(ICombatState combat)
    {
        if (combat == null || CombatManager.Instance.IsEnding || !CombatManager.Instance.IsInProgress) return;
        foreach (Creature enemy in combat.Creatures.Where(creature => creature.Monster is PillarGuardian &&
            EnemyStatScalingHelper.IsEnemy(creature) && HbrBattleStatsHelper.IsEnabled(creature) && creature.IsAlive))
            RefreshOwner(enemy);
    }
}
