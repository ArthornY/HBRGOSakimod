using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class KanzakiShowtimeCombatHelper
{
    private sealed class State
    {
        internal int Retained;
        internal bool RoarUsed;
        internal readonly HashSet<(int OwnTurn, object ExtraTurn)> RetentionTurns = new();
    }
    private sealed class Battle { internal readonly ConditionalWeakTable<CardModel, State> Cards = new(); }
    internal sealed class Receipt : IDisposable
    {
        internal CardModel Card;
        internal int Retained;
        internal Receipt Previous;
        private bool _closed;
        internal bool Active => !_closed;
        public void Dispose() { if (_closed) return; _closed = true; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<Receipt> Current = new();
    private static State Get(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return null;
        CardModel physical = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        return Battles.GetOrCreateValue(combat).Cards.GetOrCreateValue(physical);
    }
    internal static decimal GetStoredAmplification(ItsNinjaShowtimeHbrgoSaki card) =>
        card.HasUpgradeEffect(ItsNinjaShowtimeHbrgoSaki.FaintPresence) ? (Get(card)?.Retained ?? 0) * 25m : 0m;
    internal static void RecordRetained(ItsNinjaShowtimeHbrgoSaki card, object token)
    {
        if (!card.HasUpgradeEffect(ItsNinjaShowtimeHbrgoSaki.FaintPresence) || card.Pile?.Type != PileType.Hand ||
            card.Owner?.Creature.IsAlive != true || Get(card) is not { } state) return;
        if (state.RetentionTurns.Add((card.Owner.PlayerCombatState?.TurnNumber ?? card.Owner.Creature.CombatState.RoundNumber, token)))
            state.Retained = Math.Min(4, state.Retained + 1);
    }
    internal static void OnTurnEnd(Player owner, object token)
    {
        if (owner?.PlayerCombatState?.Hand is not { } hand) return;
        foreach (CardModel card in hand.Cards)
            if (card is ItsNinjaShowtimeHbrgoSaki showtime) RecordRetained(showtime, token);
    }
    internal static Receipt BeginPlay(ItsNinjaShowtimeHbrgoSaki card)
    {
        State state = Get(card);
        Receipt receipt = new() { Card = card, Retained = (int)(GetStoredAmplification(card) / 25m), Previous = Current.Value };
        if (state != null) state.Retained = 0;
        Current.Value = receipt; return receipt;
    }
    internal static bool TryUseRoar(ItsNinjaShowtimeHbrgoSaki card)
    {
        State state = Get(card);
        if (state == null || state.RoarUsed) return false;
        state.RoarUsed = true; return true;
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is ItsNinjaShowtimeHbrgoSaki && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 0m;
        for (Receipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && ReferenceEquals(receipt.Card, source)) return receipt.Retained * 25m;
        return GetStoredAmplification((ItsNinjaShowtimeHbrgoSaki)source);
    }
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target, Creature dealer, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 1m;
        ItsNinjaShowtimeHbrgoSaki card = (ItsNinjaShowtimeHbrgoSaki)source;
        decimal factor = card.DestructionCoefficient / HbrDestructionGainHelper.DefaultSkillCoefficient;
        return card.HasUpgradeEffect(ItsNinjaShowtimeHbrgoSaki.BeastFangCrush) && OverclockImmediateTurnCoordinator.IsActiveFor(card.Owner)
            ? factor * 1.2m : factor;
    }
    internal static decimal GetCriticalDamageBonusPercent(Creature target, Creature dealer, CardModel source, ValueProp props) =>
        Applies(source, dealer, target, props) && source is ItsNinjaShowtimeHbrgoSaki card &&
        card.HasUpgradeEffect(ItsNinjaShowtimeHbrgoSaki.BeastFangCrush) && OverclockImmediateTurnCoordinator.IsActiveFor(card.Owner) ? 20m : 0m;
}
