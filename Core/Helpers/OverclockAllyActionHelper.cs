using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

internal static class OverclockAllyActionHelper
{
    private sealed class EntryScope
    {
        internal bool IsRunning;
    }

    private static readonly ConditionalWeakTable<ICombatState, EntryScope> Entries = new();

    // Called once by the OD coordinator on the combat thread. This is an extra
    // action, not a turn: leave turn counters, revival and turn-end hooks alone.
    internal static async Task PerformEntryActions(IReadOnlyList<Player> participants)
    {
        CombatManager manager = CombatManager.Instance;
        ICombatState combat = manager.DebugOnlyGetState();
        if (participants == null || !IsCurrentPlayerCombat(combat, manager))
        {
            return;
        }

        var owners = new HashSet<Player>(
            participants.Where(player =>
                player?.Creature is { IsAlive: true } creature &&
                creature.Side == CombatSide.Player &&
                ReferenceEquals(creature.CombatState, combat) &&
                combat.ContainsCreature(creature)),
            ReferenceEqualityComparer.Instance);
        if (owners.Count == 0)
        {
            return;
        }

        EntryScope scope = Entries.GetValue(combat, static _ => new EntryScope());
        if (scope.IsRunning)
        {
            // Never recursively execute another entry batch from an action hook.
            return;
        }

        scope.IsRunning = true;
        try
        {
            var seen = new HashSet<Creature>(ReferenceEqualityComparer.Instance);
            var actions = combat.Allies
                .Where(creature =>
                    creature.IsAlive && creature.Side == CombatSide.Player &&
                    creature.Monster != null &&
                    creature.PetOwner != null && owners.Contains(creature.PetOwner) &&
                    creature.HasPower<PhantomPower>() && seen.Add(creature))
                .OrderBy(creature => PhantomHelper.IsPhantomMinion(creature) ? 1 : 0)
                .Select(creature => (Creature: creature, Owner: creature.PetOwner))
                .ToArray();

            // Snapshot both leaders and minions before the first await. Summoning
            // during an entry action cannot expand this batch or recurse forever.
            foreach (var action in actions)
            {
                if (!IsCurrentPlayerCombat(combat, manager))
                {
                    break;
                }

                Creature creature = action.Creature;
                if (!creature.IsAlive || creature.Side != CombatSide.Player ||
                    !ReferenceEquals(creature.CombatState, combat) ||
                    !combat.ContainsCreature(creature) ||
                    !ReferenceEquals(creature.PetOwner, action.Owner) ||
                    action.Owner.Creature is not { IsAlive: true } ownerCreature ||
                    !ReferenceEquals(ownerCreature.CombatState, combat) ||
                    !combat.ContainsCreature(ownerCreature) ||
                    !creature.HasPower<PhantomPower>())
                {
                    continue;
                }

                // The normal explicit-action path owns intent/action accounting,
                // copied intents, fusion effects and preparing the next intent.
                // Its attack counters advance only for actual attack intents.
                await PhantomHelper.PerformCurrentIntent(creature);
            }
        }
        finally
        {
            scope.IsRunning = false;
        }
    }

    private static bool IsCurrentPlayerCombat(ICombatState combat, CombatManager manager)
    {
        return combat != null && manager.IsInProgress && !manager.IsOverOrEnding &&
            combat.CurrentSide == CombatSide.Player &&
            ReferenceEquals(manager.DebugOnlyGetState(), combat);
    }
}
