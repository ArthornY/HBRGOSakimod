using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneFBondHelper
{
    private sealed class Battle
    {
        internal ICombatState Combat;
        internal bool OpeningPrepared;
        internal readonly HashSet<(int, object)> Draws = new();
    }
    private static readonly ConditionalWeakTable<Player, Battle> Battles = new();
    internal sealed class DrawScope : IDisposable
    {
        internal Player Player;
        internal ICombatState Combat;
        internal (int, object) Turn;
        internal bool Boundary, Active = true;
        internal DrawScope Previous;
        public void Dispose() { Active = false; RestoreCaller(this); }
    }
    private static readonly AsyncLocal<DrawScope> CurrentDraw = new();
    private static bool Active(Player player) => player?.Creature is { IsAlive: true, CombatState: not null } &&
        HbrBattleStatsHelper.IsEnabled(player) && player.GetRelic<ThirtyOneFCollection>() is { IsMelted: false };
    internal static bool HasBulwark(Player player) => Active(player) && ThirtyOneFKeywordRules.GetPermanentDistinctCount(player) >= 6;
    internal static bool HasSharedCosts(Player player) => Active(player) && ThirtyOneFKeywordRules.GetPermanentDistinctCount(player) >= 4;
    internal static void ResetBattle(Player player)
    { if (player != null) Battles.Remove(player); ThirtyOneFDefenseCombatHelper.ResetBattle(player); }
    private static Battle State(Player player)
    {
        Battle battle = Battles.GetOrCreateValue(player);
        if (!ReferenceEquals(battle.Combat, player.Creature.CombatState))
        { battle.Combat = player.Creature.CombatState; battle.OpeningPrepared = false; battle.Draws.Clear(); }
        return battle;
    }
    private static (int, object) Turn(Player player) => (player.PlayerCombatState?.TurnNumber ?? player.Creature.CombatState.RoundNumber,
        OverclockImmediateTurnCoordinator.GetTurnToken(player));
    internal static void PrepareOpeningDraw(Player player)
    {
        if (!Active(player) || player.PlayerCombatState?.TurnNumber != 1 ||
            OverclockImmediateTurnCoordinator.GetTurnToken(player) != null) return;
        Battle battle = State(player);
        if (battle.OpeningPrepared) return;
        battle.OpeningPrepared = true;
        if (ThirtyOneFKeywordRules.GetPermanentDistinctCount(player) < 2) return;
        CardPile draw = player.PlayerCombatState.DrawPile;
        CardModel[] members = draw.Cards.Where(card => card.Owner == player && !card.HasBeenRemovedFromState &&
            ThirtyOneFKeywordRules.IsThirtyOneFCard(card)).ToArray();
        // Synchronized combat RNG only. Reversing the shuffled order by moving
        // to top is still uniform and leaves nonmember relative order intact.
        for (int index = members.Length - 1; index > 0; index--)
        {
            int selected = player.RunState.Rng.CombatCardSelection.NextInt(index + 1);
            (members[index], members[selected]) = (members[selected], members[index]);
        }
        foreach (CardModel card in members) draw.MoveToTopInternal(card);
        if (members.Length > 0) player.GetRelic<ThirtyOneFCollection>().Flash();
    }
    internal static DrawScope BeginTurnDraw(Player player)
    {
        if (!HasSharedCosts(player)) return null;
        var turn = Turn(player);
        DrawScope previous = CurrentDraw.Value;
        bool boundary = true;
        for (DrawScope scope = previous; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Player, player) && scope.Turn.Equals(turn)) { boundary = false; break; }
        DrawScope value = new() { Player = player, Combat = player.Creature.CombatState, Turn = turn,
            Boundary = boundary, Previous = previous };
        CurrentDraw.Value = value; return value;
    }
    internal static void RestoreCaller(DrawScope scope)
    { if (scope != null && ReferenceEquals(CurrentDraw.Value, scope)) CurrentDraw.Value = scope.Previous; }
    internal static void CompleteTurnDraw(DrawScope scope)
    {
        if (scope?.Active != true || !scope.Boundary || !ReferenceEquals(scope.Player.Creature.CombatState, scope.Combat)) return;
        AfterTurnDraw(scope.Player, scope.Turn);
    }
    internal static void AfterTurnDraw(Player player)
    { if (Active(player)) AfterTurnDraw(player, Turn(player)); }
    private static bool Eligible(CardModel card, Player player)
    {
        if (card?.IsMutable != true || card.Owner != player || card.Pile != player.PlayerCombatState.Hand ||
            card.HasBeenRemovedFromState || !ThirtyOneFKeywordRules.IsThirtyOneFCard(card) || card.EnergyCost.CostsX ||
            card.EnergyCost.Canonical < 0) return false;
        bool playable = card.CanPlay(out UnplayableReason reason, out _);
        return playable || reason != UnplayableReason.None &&
            (reason & ~(UnplayableReason.EnergyCostTooHigh | UnplayableReason.StarCostTooHigh)) == UnplayableReason.None;
    }
    private static void AfterTurnDraw(Player player, (int, object) turn)
    {
        if (!HasSharedCosts(player) || !State(player).Draws.Add(turn)) return;
        var cards = player.PlayerCombatState.Hand.Cards.Where(card => Eligible(card, player))
            .Select(card => (Card: card, Cost: card.EnergyCost.GetWithModifiers(CostModifiers.All))).ToArray();
        if (cards.Length < 2) return;
        int minimum = cards.Min(item => item.Cost);
        List<(CardModel Card, int Local)> changes = new();
        foreach (var item in cards)
            if (item.Cost > minimum && TryCalibrateLocalCost(item.Card, minimum, out int local)) changes.Add((item.Card, local));
        foreach (var item in changes)
            if (item.Card.Owner == player && item.Card.Pile == player.PlayerCombatState.Hand && !item.Card.HasBeenRemovedFromState)
            { item.Card.EnergyCost.SetThisTurn(item.Local, reduceOnly: true); item.Card.InvokeEnergyCostChanged(); }
        if (changes.Count > 0) player.GetRelic<ThirtyOneFCollection>().Flash();
    }
    // Native cost is Local -> Global -> truncate -> floor at zero. Resolve a
    // local preimage once here, without modifying a cost during estimation.
    // Known native/mod cost hooks are monotonic add/subtract, halves and zero.
    // An unrepresentable third-party floor/nonmonotonic rule is left unchanged.
    internal static bool TryCalibrateLocalCost(CardModel card, int target, out int local)
    {
        local = 0;
        int probes = 0;
        bool Evaluate(int value, out int result)
        {
            result = 0;
            if (++probes > 64) return false;
            decimal amount = Hook.ModifyEnergyCostInCombat(card.CombatState, card, value);
            if (amount < int.MinValue || amount > int.MaxValue) return false;
            result = Math.Max(0, (int)amount); return true;
        }
        int upper = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
        if (!Evaluate(upper, out int upperValue) || upperValue < target) return false;
        if (upperValue == target) { local = upper; return true; }
        int candidate = Math.Min(upper, target);
        int low = 0, high = upper;
        bool hasLow = false;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (!Evaluate(candidate, out int actual)) return false;
            if (actual <= target) { low = candidate; hasLow = true; }
            else high = candidate;
            if (actual == target)
            {
                if (candidate == upper || !Evaluate(candidate + 1, out int next)) { local = candidate; return true; }
                if (next > target) { local = candidate; return true; }
                break;
            }
            int nextCandidate = (int)Math.Clamp((long)candidate + target - actual, int.MinValue, upper);
            if (nextCandidate == candidate) break;
            candidate = nextCandidate;
        }
        if (!hasLow)
        {
            long step = Math.Max(1L, (long)upperValue - target);
            for (int attempt = 0; attempt < 32 && !hasLow; attempt++)
            {
                int probe = (int)Math.Max(int.MinValue, (long)high - step);
                if (!Evaluate(probe, out int value)) return false;
                if (value <= target) { low = probe; hasLow = true; }
                else { high = probe; if (probe == int.MinValue) return false; step = Math.Min((long)int.MaxValue + 1L, step * 2L); }
            }
        }
        if (!hasLow) return false;
        while ((long)high - low > 1L)
        {
            int middle = (int)(low + ((long)high - low) / 2L);
            if (!Evaluate(middle, out int value)) return false;
            if (value <= target) low = middle; else high = middle;
        }
        if (!Evaluate(low, out int resolved) || resolved != target) return false;
        local = low; return true;
    }
}
