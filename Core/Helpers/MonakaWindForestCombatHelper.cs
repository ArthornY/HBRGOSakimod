using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MonakaWindForestCombatHelper
{
    internal sealed class Payment
    {
        internal WindForestFireMountainHbrgoSaki Card;
        internal BigInteger Faith;
        internal bool PaidFaith, Claimed;
    }
    private sealed class Payments { internal readonly List<Payment> Values = new(); }
    internal sealed class Receipt : IDisposable
    {
        internal WindForestFireMountainHbrgoSaki Card;
        internal Receipt Previous;
        internal BigInteger Faith;
        internal bool Selfless, Wholehearted, Active = true;
        internal AttackCommand Command;
        internal readonly HashSet<Creature> HitTargets = new();
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private sealed class TurnReceipts { internal readonly HashSet<(int, object)> Ends = new(); }
    private sealed class Battle { internal readonly ConditionalWeakTable<CardModel, TurnReceipts> Cards = new(); }
    private static readonly ConditionalWeakTable<CardModel, Payments> Pending = new();
    private static readonly ConditionalWeakTable<object, Battle> Battles = new();
    private static readonly AsyncLocal<Receipt> Current = new();
    private static bool Valid(WindForestFireMountainHbrgoSaki card) => card?.IsMutable == true &&
        card.Owner?.Creature is { IsAlive: true, CombatState: not null } && HbrBattleStatsHelper.IsEnabled(card.Owner) &&
        !ThirtyGProposalState.IsDisabled(card);
    private static Payment PendingPayment(CardModel card)
    {
        if (card == null || !Pending.TryGetValue(card, out Payments payments)) return null;
        for (int index = payments.Values.Count - 1; index >= 0; index--)
            if (!payments.Values[index].Claimed) return payments.Values[index];
        return null;
    }
    internal static bool IsFaithFree(CardModel source) => source is WindForestFireMountainHbrgoSaki card && Valid(card) &&
        (PendingPayment(card)?.PaidFaith == true || card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.Wholehearted) && HbrFaithResource.Get(card.Owner.Creature) >= 5);
    internal static Payment BeginPayment(CardModel source)
    {
        if (source is not WindForestFireMountainHbrgoSaki card || !Valid(card)) return null;
        Payment payment = new() { Card = card, Faith = HbrFaithResource.Get(card.Owner.Creature) };
        Pending.GetOrCreateValue(card).Values.Add(payment);
        if (card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.Wholehearted) && payment.Faith >= 5)
            payment.PaidFaith = HbrFaithResource.TrySpend(card.Owner.Creature, 5);
        return payment;
    }
    internal static void CancelPayment(Payment payment)
    {
        if (payment == null || payment.Claimed) return;
        payment.Claimed = true;
        if (Pending.TryGetValue(payment.Card, out var pending)) pending.Values.Remove(payment);
        if (payment.PaidFaith) HbrFaithResource.Gain(payment.Card.Owner.Creature, 5);
    }
    internal static async Task<(int, int)> FinishPayment(Task<(int, int)> original, Payment payment)
    {
        try { return await original; }
        catch { CancelPayment(payment); throw; }
    }
    internal static Receipt BeginPlay(WindForestFireMountainHbrgoSaki card, CardPlay play)
    {
        Payment payment = !play.IsAutoPlay && play.PlayIndex == 0 ? PendingPayment(card) : null;
        BigInteger faith = payment?.Faith ?? HbrFaithResource.Get(card.Owner.Creature);
        if (payment != null)
        {
            payment.Claimed = true;
            Pending.GetOrCreateValue(card).Values.Remove(payment);
        }
        else if (card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.Wholehearted) && faith >= 5)
            HbrFaithResource.TrySpend(card.Owner.Creature, 5);
        Receipt receipt = new()
        {
            Card = card, Previous = Current.Value, Faith = faith,
            Wholehearted = card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.Wholehearted) && faith >= 5,
            Selfless = card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.SelflessDream) && faith >= 10
        };
        Current.Value = receipt; return receipt;
    }
    private static Receipt Find(CardModel card)
    {
        for (Receipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && ReferenceEquals(receipt.Card, card)) return receipt;
        return null;
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is WindForestFireMountainHbrgoSaki card && Valid(card) && ReferenceEquals(card.Owner.Creature, dealer) &&
        (target == null || target.Side != dealer.Side) && props.IsPoweredAttack() && !HotaruCannonStatsHelper.IsCannonAttackSource(dealer, source);
    internal static decimal GetConditionalDamageMultiplier(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 1m;
        var card = (WindForestFireMountainHbrgoSaki)source;
        Receipt receipt = Find(card);
        if (receipt?.Command != null && !HighHitDamageVisualHelper.IsDirectAttackDamage(receipt.Command)) return 1m;
        BigInteger faith = receipt?.Faith ?? PendingPayment(card)?.Faith ?? HbrFaithResource.Get(dealer);
        decimal multiplier = 1m + (decimal)BigInteger.Min(10, BigInteger.Max(0, faith)) * .16m;
        if (card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.SelflessDream) && faith >= 10) multiplier *= 2m;
        if (card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.Wholehearted) && faith >= 5 &&
            target != null && (target.GetPower<HBRGOSakiMod.Core.Models.Powers.HighDimensionalShellPower>()?.CurrentDp ?? 0m) <= 0m) multiplier *= 1.5m;
        return multiplier;
    }
    internal static bool IsOwnAttack(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && Find(source)?.Command is { } command && HighHitDamageVisualHelper.IsDirectAttackDamage(command);
    internal static void RecordDamage(CardModel source, DamageResult result)
    {
        if (Find(source) is { } receipt && result?.Receiver is { } target &&
            target.Side != receipt.Card.Owner.Creature.Side && CombatAttackHitHelper.IsConnected(result)) receipt.HitTargets.Add(target);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 1m;
        if (Find(source)?.Command is { } command && !HighHitDamageVisualHelper.IsDirectAttackDamage(command)) return 1m;
        return 1.6m / HbrDestructionGainHelper.DefaultSkillCoefficient;
    }
    internal static void EndInHand(WindForestFireMountainHbrgoSaki card)
    {
        if (!Valid(card) || !card.HasUpgradeEffect(WindForestFireMountainHbrgoSaki.GentleForest) ||
            card.Owner.PlayerCombatState?.Hand.Cards.Contains(card) != true) return;
        CardModel physical = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        var key = (card.Owner.PlayerCombatState.TurnNumber, OverclockImmediateTurnCoordinator.GetTurnToken(card.Owner));
        if (Battles.GetOrCreateValue(card.CombatState).Cards.GetOrCreateValue(physical).Ends.Add(key)) HbrFaithResource.Gain(card.Owner.Creature, 1);
    }
}
