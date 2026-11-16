using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class WorldConquestCombatHelper
{
    private sealed class CardState { internal bool Played, EnergyGranted; }
    private sealed class Battle { internal readonly ConditionalWeakTable<CardModel, CardState> Cards = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly AsyncLocal<PlayScope> Current = new();
    internal sealed class PlayScope : IDisposable
    {
        internal WorldConquestHbrgoSaki Card;
        internal PlayScope Previous;
        internal bool FirstPlay;
        internal int Roll;
        internal bool Active = true;
        public void Dispose()
        { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static CardModel Physical(CardModel card) => !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card)
        ? card.DeckVersion ?? card : card;
    private static CardState State(CardModel card)
    {
        ICombatState combat = card?.IsMutable == true ? card.Owner?.Creature?.CombatState : null;
        return combat == null ? null : Battles.GetOrCreateValue(combat).Cards.GetOrCreateValue(Physical(card));
    }
    // A new combat already owns a fresh table. Repeated initialization of an
    // OD mirror must not reset its permanent physical card's receipts.
    internal static void InitializeCombat(CardModel card) => _ = State(card);
    internal static PlayScope Begin(WorldConquestHbrgoSaki card, int roll)
    {
        CardState state = State(card);
        PlayScope scope = new() { Card = card, Previous = Current.Value, FirstPlay = state?.Played != true, Roll = roll };
        if (state != null) state.Played = true;
        Current.Value = scope;
        return scope;
    }
    private static PlayScope Find(CardModel card)
    {
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, card)) return scope;
        return null;
    }
    private static bool Applies(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        source is WorldConquestHbrgoSaki && source.IsMutable && dealer != null && source.Owner?.Creature == dealer &&
        props.IsPoweredAttack() && target != null && target.Side != dealer.Side &&
        !HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out _);
    internal static decimal GetDamageAmplificationPercent(CardModel source, Creature dealer, Creature target, ValueProp props) =>
        Applies(source, dealer, target, props) && ((WorldConquestHbrgoSaki)source).HasUpgradeEffect(WorldConquestHbrgoSaki.SuperDestroyer)
            ? Math.Clamp(ThirtyOneCKeywordRules.GetPermanentDistinctCount(source.Owner), 0, 6) * 10m : 0m;
    internal static decimal GetConditionalDamageMultiplier(CardModel source, Creature dealer, Creature target, ValueProp props)
    {
        if (!Applies(source, dealer, target, props)) return 1m;
        var card = (WorldConquestHbrgoSaki)source;
        PlayScope scope = Find(card);
        decimal factor = RanaByakkoCombatHelper.HasDp(target)
            ? card.HasUpgradeEffect(WorldConquestHbrgoSaki.SuperDestroyer) ? 2m : 1.3m : 1m;
        if (scope != null && card.HasUpgradeEffect(WorldConquestHbrgoSaki.TrickOrTreat)) factor *= scope.Roll > 30 ? 2m : 0m;
        if (card.HasUpgradeEffect(WorldConquestHbrgoSaki.HolyAnthem) && (scope?.FirstPlay ?? State(card)?.Played != true)) factor *= 2m;
        return factor;
    }
    internal static async Task Recruit(Player player, CardModel source)
    {
        CardModel[] candidates = player.PlayerCombatState.DrawPile.Cards.Where(card =>
            !ReferenceEquals(card, source) && card.Id != source.Id && ThirtyOneCKeywordRules.IsThirtyOneCCard(card)).ToArray();
        if (candidates.Length == 0) return;
        CardModel selected = candidates[player.RunState.Rng.CombatCardSelection.NextInt(0, candidates.Length)];
        await CardPileCmd.Add(selected, PileType.Hand);
    }
    internal static async Task OnEnergyReset(Player player)
    {
        if (player?.Creature is not { IsAlive: true, CombatState: not null } || player.Deck == null || player.PlayerCombatState?.TurnNumber != 1) return;
        foreach (WorldConquestHbrgoSaki card in player.Deck.Cards.OfType<WorldConquestHbrgoSaki>().Where(card =>
            card.Owner == player && !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(WorldConquestHbrgoSaki.HelpDestroyWorld)).ToArray())
        {
            CardState state = State(card);
            if (state == null || state.EnergyGranted) continue;
            state.EnergyGranted = true;
            await PlayerCmd.GainEnergy(1m, player);
        }
    }
    internal static bool NeedsOpeningEnergy(Player player) => player?.Creature?.IsAlive == true &&
        player.PlayerCombatState?.TurnNumber == 1 && player.Deck != null && player.Deck.Cards.OfType<WorldConquestHbrgoSaki>().Any(card =>
            card.Owner == player && !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(WorldConquestHbrgoSaki.HelpDestroyWorld) &&
            State(card)?.EnergyGranted == false);
}
