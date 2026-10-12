using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NararaForcedTurnEndHelper
{
    private sealed class PlayScope
    {
        internal int ActivePlays;
        internal bool Failed;
        internal Creature PendingBoss;
    }

    private static readonly ConditionalWeakTable<ICombatState, PlayScope> Plays = new();

    internal sealed class DamageScope
    {
        internal DamageScope Previous;
        internal ICombatState Combat;
        internal bool Active = true;
    }

    private static readonly AsyncLocal<DamageScope> CurrentDamage = new();

    internal static DamageScope BeginDamage(Creature dealer, CardModel cardSource)
    {
        ICombatState combat = cardSource?.Owner?.Creature.CombatState ?? dealer?.CombatState ??
            CombatManager.Instance.DebugOnlyGetState();
        if (combat == null || !Plays.TryGetValue(combat, out PlayScope play) || play.ActivePlays <= 0)
            return null;
        DamageScope scope = new() { Previous = CurrentDamage.Value, Combat = combat };
        CurrentDamage.Value = scope;
        return scope;
    }

    internal static void RestoreDamageCaller(DamageScope scope)
    {
        if (scope != null && ReferenceEquals(CurrentDamage.Value, scope))
            CurrentDamage.Value = scope.Previous;
    }

    internal static async Task<IEnumerable<DamageResult>> FinishDamage(
        Task<IEnumerable<DamageResult>> original, DamageScope scope)
    {
        try { return await original; }
        finally { CloseDamage(scope); }
    }

    internal static void CloseDamage(DamageScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreDamageCaller(scope);
    }

    internal static bool ShouldStopFollowups(ICombatState combat, out MonsterModel preventer)
    {
        if (!ShouldStopAutomaticPlays(combat, out preventer)) return false;

        // Finish every reaction belonging to the hit which crossed the limit,
        // including its magic-sword HP supplement. Only the next independent
        // hit or automatic card is stopped; never throw out of native cleanup.
        for (DamageScope scope = CurrentDamage.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Combat, combat))
            {
                preventer = null;
                return false;
            }
        return true;
    }

    internal static bool ShouldStopAutomaticPlays(ICombatState combat, out MonsterModel preventer)
    {
        preventer = null;
        if (combat == null || !Plays.TryGetValue(combat, out PlayScope play) ||
            play.ActivePlays <= 0 || play.Failed || play.PendingBoss == null ||
            GetPlayersToEnd(play.PendingBoss).Length == 0) return false;
        // A newly played card begins a new action chain, even when requested
        // by the hit that crossed the limit. Its native rejected-play cleanup
        // remains in charge of disposing generated copies and moving cards.
        preventer = play.PendingBoss.Monster;
        return true;
    }

    internal static bool ShouldContinueCardPlay(int nextIndex, int playCount, CardModel card) =>
        nextIndex < playCount &&
        !ShouldStopAutomaticPlays(card?.Owner?.Creature.CombatState, out _);

    internal static ICombatState BeginCardPlay(CardModel card)
    {
        ICombatState combat = card?.Owner?.Creature.CombatState;
        if (combat == null || !combat.Enemies.Any(creature =>
                creature.IsAlive && creature.Monster is NararaShibaBossMonster)) return null;
        Plays.GetValue(combat, static _ => new PlayScope()).ActivePlays++;
        return combat;
    }

    internal static void EndCardPlay(ICombatState combat, bool successful)
    {
        if (combat == null || !Plays.TryGetValue(combat, out PlayScope scope)) return;
        scope.Failed |= !successful;
        if (--scope.ActivePlays > 0) return;

        Plays.Remove(combat);
        if (!scope.Failed && scope.PendingBoss != null)
            CancelQueuedPlaysAndEnd(GetPlayersToEnd(scope.PendingBoss));
    }

    internal static void Request(Creature boss)
    {
        Player[] players = GetPlayersToEnd(boss);
        if (players.Length == 0) return;

        if (Plays.TryGetValue(boss.CombatState, out PlayScope scope) && scope.ActivePlays > 0)
        {
            scope.PendingBoss = boss;
            return;
        }

        CancelQueuedPlaysAndEnd(players);
    }

    private static Player[] GetPlayersToEnd(Creature boss)
    {
        CombatManager manager = CombatManager.Instance;
        var combat = boss?.CombatState;
        if (boss?.Monster is not NararaShibaBossMonster || !boss.IsAlive ||
            boss.Side != CombatSide.Enemy || combat == null ||
            combat.CurrentSide != CombatSide.Player || !manager.IsInProgress ||
            manager.IsOverOrEnding || manager.EndingPlayerTurnPhaseOne ||
            manager.EndingPlayerTurnPhaseTwo ||
            !ReferenceEquals(manager.DebugOnlyGetState(), combat)) return System.Array.Empty<Player>();

        return combat.Players
            .Where(player => player.Creature.IsAlive && player.PlayerCombatState != null &&
                manager.IsPartOfPlayerTurn(player) && !manager.IsPlayerReadyToEndTurn(player))
            .ToArray();
    }

    private static void CancelQueuedPlaysAndEnd(Player[] players)
    {
        if (players.Length == 0) return;
        // Cancel waiting manual plays/undo before this wrapper returns to the
        // action executor. Failed plays never leave the queues stuck cancelling.
        // The scope also covers standalone automatic cards, which native EndTurn
        // does not wait for. Native EndTurn handles the remaining player-driven
        // action without a cyclic await.
        RunManager.Instance.ActionQueueSet.StartCancellingAllPlayerDrivenCombatActions();
        foreach (Player player in players)
            PlayerCmd.EndTurn(player, canBackOut: false);
    }
}
