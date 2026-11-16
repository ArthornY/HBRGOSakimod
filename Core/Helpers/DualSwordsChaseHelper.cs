using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// A chase belongs to one physical manual play, including all its native
// replays. Nested automatic cards get their own masking scope and never
// borrow the enclosing card's chase or advance the next-card state.
internal static class DualSwordsChaseHelper
{
    private sealed class PaymentSnapshot
    {
        internal bool IsChase;
    }

    internal sealed class PlayScope : IDisposable
    {
        internal readonly PlayScope Previous;
        internal readonly CardModel Card;
        internal readonly bool IsAutoPlay;
        internal readonly bool IsChase;
        internal bool Committed;
        private bool _disposed;
        internal bool Active => !_disposed;

        internal PlayScope(CardModel card, bool isAutoPlay)
        {
            Previous = Current.Value;
            Card = card;
            IsAutoPlay = isAutoPlay;
            IsChase = !isAutoPlay && (Payments.TryGetValue(card, out PaymentSnapshot paid)
                ? paid.IsChase : IsPendingChase(card));
            Payments.Remove(card);
            Current.Value = this;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Payments.Remove(Card);
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }

    private static readonly AsyncLocal<PlayScope> Current = new();
    private static readonly ConditionalWeakTable<CardModel, PaymentSnapshot> Payments = new();

    internal static bool NeedsPlayScope(CardModel card) => Current.Value != null ||
        card != null && Payments.TryGetValue(card, out _) ||
        card?.Owner?.Creature?.HasPower<ReptureDualSwordsPower>() == true ||
        ModeTransitionHelper.IsDualSwordsRestrictedCard(card);

    internal static PlayScope BeginPlay(CardModel card, bool isAutoPlay) => new(card, isAutoPlay);

    internal static bool IsResolvingPlay(CardModel card) => Current.Value?.Active == true &&
        ReferenceEquals(Current.Value.Card, card);

    internal static void RecordPayment(CardModel card)
    {
        if (card == null) return;
        Payments.Remove(card);
        if (card.Owner?.Creature?.HasPower<ReptureDualSwordsPower>() == true)
            Payments.Add(card, new PaymentSnapshot { IsChase = IsPendingChase(card) });
    }

    internal static async Task<(int, int)> KeepPaymentUnlessFailed(CardModel card, Task<(int, int)> payment)
    {
        try { return await payment; }
        catch { Payments.Remove(card); throw; }
    }

    private static bool IsPendingChase(CardModel card) => card?.IsMutable == true &&
        card.Type == CardType.Attack && card.Owner?.Creature?.GetPower<ReptureDualSwordsPower>()
            ?.NextAttackHasChase == true;

    internal static bool IsChaseAttack(Creature attacker, CardModel card)
    {
        if (attacker == null || card?.Owner?.Creature != attacker || card.Type != CardType.Attack) return false;
        PlayScope scope = Current.Value;
        if (scope != null && ReferenceEquals(scope.Card, card))
            return scope.Active && !scope.IsAutoPlay && scope.IsChase;
        // Only a live hand card is a prospective manual play. Permanently
        // stored templates and triggered damage cannot gain a preview chase.
        return IsPendingChase(card) && card.Pile?.Type == PileType.Hand;
    }

    internal static bool IsChaseCost(CardModel card)
    {
        if (card?.EnergyCost.CostsX != false || card.Type != CardType.Attack) return false;
        PlayScope scope = Current.Value;
        if (scope != null && ReferenceEquals(scope.Card, card)) return scope.Active && !scope.IsAutoPlay && scope.IsChase;
        if (Payments.TryGetValue(card, out PaymentSnapshot payment)) return payment.IsChase;
        return IsPendingChase(card);
    }

    internal static decimal GetDamageMultiplier(Creature attacker, CardModel card) =>
        IsChaseAttack(attacker, card) && !attacker.HasPower<DualSwordsEnhancementPower>() ? .75m : 1m;

    internal static void Commit(CardPlay play, ReptureDualSwordsPower power)
    {
        if (play.IsAutoPlay || !play.IsFirstInSeries || play.Card?.Owner?.Creature != power.Owner) return;
        PlayScope scope = Current.Value;
        if (scope != null && ReferenceEquals(scope.Card, play.Card))
        {
            if (!scope.Active || scope.IsAutoPlay || scope.Committed) return;
            scope.Committed = true;
            power.SetNextAttackHasChase(play.Card.Type == CardType.Attack && !scope.IsChase);
        }
        else
        {
            power.SetNextAttackHasChase(play.Card.Type == CardType.Attack && !power.NextAttackHasChase);
        }
    }

    internal static void Refresh(Creature owner)
    {
        if (owner?.Player?.PlayerCombatState is not { } combat) return;
        // Native card events refresh cost labels without polling or changing
        // authored costs. The existing coalesced state signal updates previews.
        foreach (CardModel card in combat.Hand.Cards) card.InvokeEnergyCostChanged();
        CombatPreviewRefreshHelper.RefreshCombatState(owner, nameof(DualSwordsChaseHelper));
    }
}
