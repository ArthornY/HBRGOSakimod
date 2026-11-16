using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class IchikoEldestCombatHelper
{
    static IchikoEldestCombatHelper() => ThirtyGProposalState.Changed += card => Invalidate(card?.Owner);
    private sealed class Battle { internal readonly HashSet<CardModel> Played = new(); }
    private sealed class DeckFlags { internal bool Dirty = true; internal int Cards = -1, Flags; internal Player Owner; }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static readonly ConditionalWeakTable<CardPile, DeckFlags> Decks = new();
    internal static void Invalidate(Player owner)
    { if (owner?.Deck is { } deck && Decks.TryGetValue(deck, out var cache)) cache.Dirty = true; }
    internal static bool HasPermanentEffect(Player player, int effect)
    {
        if (player?.Deck is not { } deck || !HbrBattleStatsHelper.IsEnabled(player)) return false;
        DeckFlags cache = Decks.GetValue(deck, pile =>
        { DeckFlags entry = new(); pile.ContentsChanged += () => entry.Dirty = true; return entry; });
        if (cache.Dirty || cache.Cards != deck.Cards.Count || !ReferenceEquals(cache.Owner, player))
        {
            int flags = 0;
            foreach (var card in deck.Cards.OfType<EldestOshimaIsHereHbrgoSaki>()
                .Where(card => card.Owner == player && !card.HasBeenRemovedFromState && !ThirtyGProposalState.IsDisabled(card)))
                foreach (int upgrade in card.UpgradeEffectOrder.Take(card.CurrentUpgradeLevel)) flags |= 1 << upgrade;
            cache.Flags = flags; cache.Cards = deck.Cards.Count; cache.Owner = player; cache.Dirty = false;
        }
        return (cache.Flags & (1 << effect)) != 0;
    }
    internal static int GetFrugalBaseCost(CardModel card, int canonical) => card?.IsMutable == true &&
        card.Owner != null && !card.EnergyCost.CostsX && canonical > 2 && HasPermanentEffect(card.Owner, EldestOshimaIsHereHbrgoSaki.Frugal)
            ? Math.Max(2, canonical / 2) : canonical;
    internal static decimal GetGoldBonusPercent(Player player) => HasPermanentEffect(player, EldestOshimaIsHereHbrgoSaki.MilitaryPay) ? 15m : 0m;
    internal static decimal GetVitalityBonus(Creature creature) => creature?.Powers.OfType<IchikoVitalityUpPower>()
        .Where(power => power.Amount > 0).Aggregate(0m, (sum, power) => DamageAmplificationLimits.AddSaturated(sum, power.EffectPotency)) ?? 0m;
    internal static bool MarkPlayed(CardModel card)
    {
        if (card.Owner?.Creature?.CombatState is not { } combat) return false;
        CardModel key = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        return Battles.GetOrCreateValue(combat).Played.Add(key);
    }
    internal static async Task CallName(PlayerChoiceContext context, EldestOshimaIsHereHbrgoSaki source)
    {
        var owner = source.Owner;
        CardModel[] cards = owner.PlayerCombatState.DrawPile.Cards.Where(card => card is IThirtyOneEMemberCard).ToArray();
        if (cards.Length == 0) { await CardPileCmd.Draw(context, 1m, owner); return; }
        CardModel selected = (await CardSelectCmd.FromSimpleGrid(context, cards, owner,
            new CardSelectorPrefs(new LocString("cards", source.Id.Entry + ".selectionPrompt"), 1)))
            .FirstOrDefault(card => cards.Contains(card) && card.Owner == owner && card.Pile?.Type == PileType.Draw);
        if (selected != null) await CardPileCmd.Add(selected, PileType.Hand);
    }
}
