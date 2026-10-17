using System;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Replay counts are generated before CardPlay exists. Preserve the wrapper's
// automatic/manual flag through awaits and nested plays for those early hooks.
internal static class CardPlayContextHelper
{
    internal sealed class DamageBudget
    {
        internal decimal IssuedAmount;
    }

    internal sealed class PlayScope
    {
        internal CardModel Card;
        internal bool IsAutoPlay;
        internal PlayScope Previous;
        internal bool Active = true;
        internal DamageBudget Damage;
        internal CardDamageHistoryHelper.PlayScope History;
        internal HighHitDamageVisualHelper.CardBatchScope Visuals;
        internal HotaruFirstManualCardHelper.PlayScope FirstManual;
    }

    private static readonly AsyncLocal<PlayScope> Current = new();

    internal static PlayScope Begin(CardModel card, bool isAutoPlay)
    {
        PlayScope scope = new()
        {
            Card = card,
            IsAutoPlay = isAutoPlay,
            Previous = Current.Value
        };
        PlayScope parent = Current.Value;
        scope.Damage = parent?.Active == true &&
            (ReferenceEquals(parent.Card, card) || (isAutoPlay && card?.IsDupe == true))
                ? parent.Damage
                : new DamageBudget();
        Current.Value = scope;
        scope.History = CardDamageHistoryHelper.Begin(card);
        scope.Visuals = HighHitDamageVisualHelper.BeginCardBatch(card);
        scope.FirstManual = HotaruFirstManualCardHelper.Begin(card, isAutoPlay);
        HotaruUncommonCardHelper.CapturePlayStart(card, isAutoPlay, scope);
        HotaruRareRollHelper.CapturePlayStart(card, isAutoPlay, scope);
        return scope;
    }

    internal static decimal LimitIssuedDamage(CardModel card, decimal amount)
    {
        if (card == null || amount <= 0m) return amount;
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
        {
            if (!scope.Active || !ReferenceEquals(scope.Card, card)) continue;
            decimal allowed = System.Math.Min(amount, System.Math.Max(0m,
                BigCombatNumbers.MaximumCardDamage - scope.Damage.IssuedAmount));
            scope.Damage.IssuedAmount += allowed;
            return allowed;
        }
        return amount;
    }

    internal static bool IsAutoPlay(CardModel card)
    {
        if (card == null) return false;
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
        {
            if (ReferenceEquals(scope.Card, card))
                return scope.Active && scope.IsAutoPlay;
        }
        return false;
    }

    // Identity remains stable across awaits and a replay series, but nested
    // wrappers (including the same CardModel) each have a separate identity.
    internal static object GetCurrentPlayToken(CardModel card)
    {
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, card)) return scope;
        return null;
    }

    // The original async wrapper captures this scope; its caller must not keep
    // it while the returned task is pending, especially in multiplayer combat.
    internal static void RestoreCaller(PlayScope scope)
    {
        CardDamageHistoryHelper.RestoreCaller(scope?.History);
        HighHitDamageVisualHelper.RestoreCardCaller(scope?.Visuals);
        HotaruFirstManualCardHelper.RestoreCaller(scope?.FirstManual);
        if (scope != null && ReferenceEquals(Current.Value, scope))
            Current.Value = scope.Previous;
    }

    internal static async Task Finish(Task original, PlayScope scope)
    {
        try { await original; }
        finally { Close(scope); }
    }

    internal static void Close(PlayScope scope)
    {
        if (scope != null) scope.Active = false;
        RestoreCaller(scope);
        try { HighHitDamageVisualHelper.CloseCard(scope?.Visuals); }
        finally
        {
            try { CardDamageHistoryHelper.Close(scope?.History); }
            finally { HotaruFirstManualCardHelper.Close(scope?.FirstManual); }
        }
    }
}
