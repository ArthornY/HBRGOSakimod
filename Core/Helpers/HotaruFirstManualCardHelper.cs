using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HotaruFirstManualCardHelper
{
    internal sealed class TurnState
    {
        internal bool HasManual;
    }

    private sealed class PlayerState
    {
        internal ICombatState Combat;
        internal int OrdinaryTurn = int.MinValue;
        internal TurnState Ordinary;
        internal readonly Dictionary<object, TurnState> Overclock = new(ReferenceEqualityComparer.Instance);
    }

    internal sealed class PlayScope
    {
        internal CardModel Card;
        internal TurnState Turn;
        internal bool IsFirstManual;
        internal bool IsAutoPlay;
        internal bool Active = true;
        internal PlayScope Previous;
    }

    private static readonly ConditionalWeakTable<Player, PlayerState> Players = new();
    private static readonly AsyncLocal<PlayScope> Current = new();

    // Called for every wrapper, even before the player owns the power. Do not
    // inspect CurrentPlayIndex here: it can still describe the previous series.
    internal static PlayScope Begin(CardModel card, bool isAutoPlay)
    {
        Player owner = card?.IsMutable == true ? card.Owner : null;
        TurnState turn = GetTurn(owner);
        PlayScope scope = new()
        {
            Card = card,
            Turn = turn,
            IsAutoPlay = isAutoPlay,
            IsFirstManual = !isAutoPlay && turn != null && !turn.HasManual,
            Previous = Current.Value
        };
        if (!isAutoPlay && turn != null) turn.HasManual = true;
        Current.Value = scope;
        return scope;
    }

    internal static void RestoreCaller(PlayScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    internal static void Close(PlayScope scope)
    {
        if (scope != null) scope.Active = false;
        RestoreCaller(scope);
    }

    // Initialization is idempotent for the same native turn or OD session;
    // returning from OD never clears the suspended ordinary turn's record.
    internal static void ResetTurn(Player player, object turnToken = null) => GetTurn(player, turnToken);

    internal static bool HasManualPlayThisTurn(Player player) => GetTurn(player)?.HasManual == true;

    internal static bool IsFirstManualPlay(CardModel card, CardPlay play) =>
        play != null && !play.IsAutoPlay && play.IsFirstInSeries &&
        ReferenceEquals(play.Card, card) && IsEligible(card, play);

    internal static bool IsEligible(CardModel card, CardPlay play = null)
    {
        if (card?.IsMutable != true || card.Owner is not { } player ||
            player.Creature?.CombatState == null || CardPlayContextHelper.IsAutoPlay(card)) return false;
        if (play != null && (play.IsAutoPlay || !play.IsFirstInSeries ||
            !ReferenceEquals(play.Card, card))) return false;
        TurnState turn = GetTurn(player);
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, card))
                return !scope.IsAutoPlay && scope.IsFirstManual && ReferenceEquals(scope.Turn, turn);
        return turn != null && !turn.HasManual;
    }

    private static TurnState GetTurn(Player player, object explicitToken = null)
    {
        ICombatState combat = player?.Creature?.CombatState;
        if (combat == null || player.PlayerCombatState == null) return null;
        PlayerState state = Players.GetValue(player, static _ => new());
        if (!ReferenceEquals(state.Combat, combat))
        {
            state.Combat = combat;
            state.Ordinary = null;
            state.OrdinaryTurn = int.MinValue;
            state.Overclock.Clear();
        }
        int turnNumber = player.PlayerCombatState.TurnNumber;
        if (state.Ordinary == null || state.OrdinaryTurn != turnNumber)
        {
            state.OrdinaryTurn = turnNumber;
            // Native history is also useful when lazy initialization follows
            // a restored combat phase whose start receipt has already run.
            state.Ordinary = new TurnState
            {
                HasManual = CombatManager.Instance.History.CardPlaysStarted.Any(entry =>
                    entry.HappenedThisTurn(combat) && entry.CardPlay.IsFirstInSeries &&
                    !entry.CardPlay.IsAutoPlay && ReferenceEquals(entry.Actor, player.Creature))
            };
        }
        // Snapshot the suspended ordinary turn before any OD manual play can
        // enter native history, even when OD is our first access this combat.
        object token = explicitToken ?? OverclockImmediateTurnCoordinator.GetTurnToken(player);
        if (token != null)
        {
            if (!state.Overclock.TryGetValue(token, out TurnState overclock))
                state.Overclock[token] = overclock = new();
            return overclock;
        }
        return state.Ordinary;
    }
}
