using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class YamawakiLaboratoryCombatHelper
{
    private sealed class CardState { internal long LastEpoch = -1; }
    private sealed class PlayerState
    {
        internal long ShuffleEpoch;
        internal readonly ConditionalWeakTable<CardModel, CardState> Cards = new();
    }
    private sealed class Battle { internal readonly ConditionalWeakTable<Player, PlayerState> Players = new(); }
    internal sealed class PlayReceipt : IDisposable
    {
        internal CardModel Card;
        internal bool Bonus;
        internal PlayReceipt Previous;
        private bool _closed;
        internal bool Active => !_closed;
        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<PlayReceipt> Current = new();
    private static PlayerState State(Player player) => player?.Creature?.CombatState is { } combat
        ? Battles.GetOrCreateValue(combat).Players.GetOrCreateValue(player) : null;
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card)
        ? card.DeckVersion ?? card : card;
    internal static void AfterShuffle(Player player)
    {
        if (State(player) is { } state) state.ShuffleEpoch++;
    }
    internal static bool HasFirstPlayBonus(WorldIsMyLaboratoryHbrgoSaki card)
    {
        for (PlayReceipt receipt = Current.Value; receipt != null; receipt = receipt.Previous)
            if (receipt.Active && ReferenceEquals(receipt.Card, card)) return receipt.Bonus;
        PlayerState state = State(card?.Owner);
        return state != null && state.Cards.GetOrCreateValue(Physical(card)).LastEpoch != state.ShuffleEpoch;
    }
    internal static PlayReceipt BeginPlay(WorldIsMyLaboratoryHbrgoSaki card)
    {
        PlayerState state = State(card.Owner);
        bool bonus = state != null && state.Cards.GetOrCreateValue(Physical(card)).LastEpoch != state.ShuffleEpoch;
        if (state != null) state.Cards.GetOrCreateValue(Physical(card)).LastEpoch = state.ShuffleEpoch;
        PlayReceipt receipt = new() { Card = card, Bonus = bonus, Previous = Current.Value };
        Current.Value = receipt;
        return receipt;
    }
    private static bool IsOwnAttack(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source?.Owner?.Creature == dealer && dealer != null && props.IsPoweredAttack() &&
        (target == null || target.Side != dealer.Side) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (source is not WorldIsMyLaboratoryHbrgoSaki card || !IsOwnAttack(source, dealer, target, props)) return 0m;
        decimal percent = card.HasUpgradeEffect(WorldIsMyLaboratoryHbrgoSaki.MadScientist) &&
            dealer.CombatState?.GetOpponentsOf(dealer).Any(enemy => enemy.IsAlive && enemy.GetPower<BreakPower>() != null) == true ? 25m : 0m;
        if (card.HasUpgradeEffect(WorldIsMyLaboratoryHbrgoSaki.DevilReturns) && HasFirstPlayBonus(card)) percent += 50m;
        return percent;
    }
    internal static decimal GetHpDamageMultiplier(CardModel source, Creature target) =>
        source is WorldIsMyLaboratoryHbrgoSaki card && target != null && target.Side != card.Owner?.Creature.Side &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, card.Owner?.Creature, card, ValueProp.Move, out _) &&
        !RanaByakkoCombatHelper.HasDp(target) ? 1m + card.HpBonusPercent / 100m : 1m;
    internal static decimal GetDestructionGainMultiplier(CardModel source, Creature target) =>
        source is WorldIsMyLaboratoryHbrgoSaki card && card.HasUpgradeEffect(WorldIsMyLaboratoryHbrgoSaki.WillScythe) &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, card.Owner?.Creature, card, ValueProp.Move, out _) &&
        target?.GetPower<BreakPower>() != null ? 2m : 1m;
}
