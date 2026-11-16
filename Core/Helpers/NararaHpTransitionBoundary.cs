using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// A depleted HP layer closes the current action and current player turn. It
// does not allocate a per-turn damage budget or persist a future turn skip.
internal static class NararaHpTransitionBoundary
{
    internal sealed class ActionScope
    {
        internal ActionScope Previous;
        internal ICombatState Combat;
        internal bool Active = true;
        internal readonly ConcurrentDictionary<Creature, byte> Broken = new();
    }

    private sealed record PlayerTurn(Player Player, int Number, object Token)
    {
        internal bool Matches(ICombatState combat) =>
            Player.Creature.IsAlive && ReferenceEquals(Player.Creature.CombatState, combat) &&
            Player.PlayerCombatState?.TurnNumber == Number &&
            ReferenceEquals(OverclockImmediateTurnCoordinator.GetTurnToken(Player), Token);
    }

    private sealed record TurnBoundary(ICombatState Combat, PlayerTurn[] Players, ActionScope Origin);
    private sealed class TurnState { internal TurnBoundary Boundary; }
    private static readonly ConditionalWeakTable<Creature, TurnState> Turns = new();
    private static readonly AsyncLocal<ActionScope> Current = new();

    private static Player[] ActivePlayers(ICombatState combat, CombatManager manager) =>
        combat.Players.Where(player => player.Creature.IsAlive && player.PlayerCombatState != null &&
            manager.IsPartOfPlayerTurn(player)).ToArray();

    internal static ActionScope Begin(ICombatState combat)
    {
        if (combat == null || !combat.Enemies.Any(NararaLayeredHpHelper.IsEnabled)) return null;
        ActionScope scope = new() { Combat = combat, Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }

    internal static ICombatState CombatFor(Creature dealer, CardModel card) =>
        card?.Owner?.Creature.CombatState ?? dealer?.CombatState ?? CombatManager.Instance.DebugOnlyGetState();

    internal static void RestoreCaller(ActionScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    internal static void Close(ActionScope scope, bool failed = false)
    {
        if (scope == null || !scope.Active) return;
        scope.Active = false;
        if (failed)
        {
            // A failed card wrapper does not flush the old forced-turn helper.
            // Never retain a turn lock after its owning action has failed.
            // A nested wrapper failure also marks the old shared play chain
            // failed, even if an outer card catches that exception and later
            // completes successfully. Clear its ancestor-owned turn boundary
            // too; active action markers still protect the remaining cleanup.
            for (ActionScope affected = scope; affected != null; affected = affected.Previous)
                if (ReferenceEquals(affected.Combat, scope.Combat))
                    foreach (Creature boss in affected.Broken.Keys)
                        if (Turns.TryGetValue(boss, out TurnState state) &&
                            ReferenceEquals(state.Boundary?.Origin, affected)) state.Boundary = null;
        }
        RestoreCaller(scope);
    }

    internal static async Task Finish(Task original, ActionScope scope)
    {
        bool successful = false;
        try { await original; successful = true; }
        finally { Close(scope, !successful); }
    }

    internal static async Task<T> Finish<T>(Task<T> original, ActionScope scope)
    {
        bool successful = false;
        try { T result = await original; successful = true; return result; }
        finally { Close(scope, !successful); }
    }

    internal static bool BlocksHpLoss(Creature boss)
    {
        if (!NararaLayeredHpHelper.IsEnabled(boss)) return false;
        ICombatState combat = boss.CombatState;
        for (ActionScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Combat, combat) && scope.Broken.ContainsKey(boss)) return true;

        if (!Turns.TryGetValue(boss, out TurnState state) || state.Boundary is not { } boundary) return false;
        // Ready votes and EndingPlayerTurnPhase* are intentionally absent:
        // requesting EndTurn changes those before the outgoing action finishes.
        CombatManager manager = CombatManager.Instance;
        if (ReferenceEquals(boundary.Combat, combat) && combat.CurrentSide == CombatSide.Player &&
            ReferenceEquals(manager.DebugOnlyGetState(), combat))
        {
            Player[] players = ActivePlayers(combat, manager);
            // Match the whole currently participating turn. An unchanged peer
            // must not keep an old boundary alive when another player enters
            // a native extra turn or returns from a personal/OD session.
            if (players.Length > 0 && players.All(player => boundary.Players.Any(snapshot =>
                    ReferenceEquals(snapshot.Player, player) && snapshot.Matches(combat)))) return true;
        }
        state.Boundary = null;
        return false;
    }

    internal static void RecordBreak(Creature boss)
    {
        if (!NararaLayeredHpHelper.IsEnabled(boss)) return;
        ICombatState combat = boss.CombatState;
        ActionScope origin = null;
        for (ActionScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Combat, combat))
            {
                scope.Broken.TryAdd(boss, 0);
                origin = scope;
            }

        CombatManager manager = CombatManager.Instance;
        if (combat == null || combat.CurrentSide != CombatSide.Player || !manager.IsInProgress ||
            manager.IsOverOrEnding || manager.EndingPlayerTurnPhaseOne || manager.EndingPlayerTurnPhaseTwo ||
            !ReferenceEquals(manager.DebugOnlyGetState(), combat)) return;
        Player[] active = ActivePlayers(combat, manager);
        // Capture ready peers too: a later ready/undo vote is not a new turn.
        if (!active.Any(player => !manager.IsPlayerReadyToEndTurn(player))) return;
        PlayerTurn[] players = active
            .Select(player => new PlayerTurn(player, player.PlayerCombatState.TurnNumber,
                OverclockImmediateTurnCoordinator.GetTurnToken(player))).ToArray();
        if (players.Length != 0)
            Turns.GetValue(boss, static _ => new TurnState()).Boundary = new TurnBoundary(combat, players, origin);
    }
}
