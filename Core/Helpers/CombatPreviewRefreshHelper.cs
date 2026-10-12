using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatPreviewRefreshHelper
{
    private static readonly Action<CombatStateTracker, string> NotifyCombatStateChanged =
        PrivateAccess.MethodDelegate<CombatStateTracker, Action<CombatStateTracker, string>>(
            "NotifyCombatStateChanged",
            typeof(string));

    public static void RefreshCombatState(Creature creature, string caller)
    {
        if (creature?.CombatState == null)
        {
            return;
        }

        // Use the game's coalesced notification to refresh cards and intents together.
        NotifyCombatStateChanged(CombatManager.Instance.StateTracker, caller);
    }

    public static Task RefreshCreatureIntent(Creature creature)
    {
        if (creature?.CombatState == null)
        {
            return Task.CompletedTask;
        }

        NCreature creatureNode = NCombatRoom.Instance?
            .GetCreatureNode(creature);
        if (creatureNode == null ||
            !GodotObject.IsInstanceValid(creatureNode) ||
            !creatureNode.IsInsideTree())
        {
            return Task.CompletedTask;
        }

        return creatureNode.UpdateIntent(
            creature.CombatState.GetOpponentsOf(creature));
    }

    public static async Task RefreshOpponentIntents(Creature protectedCreature)
    {
        NCombatRoom room = NCombatRoom.Instance;
        var combatState = protectedCreature?.CombatState;
        if (combatState == null ||
            room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree())
        {
            return;
        }

        HashSet<Creature> opponents = combatState
            .GetOpponentsOf(protectedCreature)
            .Where(creature => creature.Monster != null)
            .ToHashSet();
        if (opponents.Count == 0)
        {
            return;
        }

        // The room already indexes combatants. Walking the entire scene also
        // visits every card, effect and UI child whenever a reduction changes.
        // Snapshot before awaiting: an intent animation can remove a creature.
        NCreature[] targets = room.CreatureNodes
            .Where(node => GodotObject.IsInstanceValid(node) &&
                node.Entity != null && opponents.Contains(node.Entity))
            .ToArray();
        foreach (NCreature creatureNode in targets)
        {
            if (protectedCreature.CombatState != combatState) break;
            if (!GodotObject.IsInstanceValid(creatureNode) ||
                !creatureNode.IsInsideTree() ||
                creatureNode.Entity?.CombatState != combatState) continue;
            await creatureNode.RefreshIntents();
        }
    }
}
