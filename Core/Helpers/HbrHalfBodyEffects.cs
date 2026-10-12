using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrHalfBodyEffects
{
    private sealed class Consumption { public bool Used; }
    private static readonly ConditionalWeakTable<CardModel, Consumption> Consumed = new();
    private static readonly ConditionalWeakTable<CombatRoom, HashSet<CardModel>> Rewarded = new();

    internal static int GetRemovedCardCount(Player player)
    {
        if (player?.RunState == null) return 0;
        // Native run history is saved across rooms/acts and already distinguishes
        // permanent removal from combat exhaustion and other players' removals.
        long count = 0;
        foreach (var act in player.RunState.MapPointHistory)
        foreach (var floor in act)
            count += floor.PlayerStats.FirstOrDefault(entry => entry.PlayerId == player.NetId)
                ?.CardsRemoved.Count ?? 0;
        return (int)Math.Min(count, int.MaxValue);
    }

    private static CardModel GetOrigin(HalfBodyHbrgoSaki card) =>
        card.Pile?.Type == PileType.Deck ? card : card.DeckVersion ?? card;

    internal static bool CanPreventDeath(HalfBodyHbrgoSaki card, Creature creature)
    {
        if (card == null || !card.IsMutable || card.HasBeenRemovedFromState ||
            !card.HasUpgradeEffect(2) || creature != card.Owner?.Creature || creature.MaxHp <= 0)
            return false;
        CardModel origin = GetOrigin(card);
        if (Consumed.GetOrCreateValue(origin).Used || origin.HasBeenRemovedFromState) return false;
        if (card.DeckVersion != null && card.DeckVersion.Pile?.Type != PileType.Deck) return false;
        return card.Pile?.Type == PileType.Deck ||
            (card.Pile?.IsCombatPile == true && card.Owner.PlayerCombatState != null);
    }

    internal static async Task PreventDeath(HalfBodyHbrgoSaki card, Creature creature)
    {
        if (!CanPreventDeath(card, creature)) return;
        Player player = card.Owner;
        CardModel origin = GetOrigin(card);
        // Reserve the actual permanent card before any HP/removal callback can
        // enter death prevention again through a different combat copy.
        Consumed.GetOrCreateValue(origin).Used = true;
        CardModel[] combatCopies = player.PlayerCombatState?.AllCards
            .Where(copy => !copy.HasBeenRemovedFromState && copy.Pile?.IsCombatPile == true &&
                (ReferenceEquals(copy, card) || ReferenceEquals(copy.DeckVersion, origin)))
            .ToArray() ?? Array.Empty<CardModel>();

        // Setting HP, rather than healing, guarantees the agreed one-HP rescue
        // even when ordinary healing is modified. This uses native HP hooks.
        await CreatureCmd.SetCurrentHp(creature, 1m);
        player.GetRelic<DeflectorProtection>()?.RestoreDpAfterHalfBodyRevival();
        if (origin.Pile?.Type == PileType.Deck && origin.Owner == player)
        {
            // Eternal prevents voluntary selection; this explicit self-removal
            // still uses the native command, history and removal hooks.
            await CardPileCmd.RemoveFromDeck(origin, showPreview: false);
        }
        foreach (CardModel copy in combatCopies)
        {
            if (!copy.HasBeenRemovedFromState && copy.Pile?.IsCombatPile == true)
                await CardPileCmd.RemoveFromCombat(copy);
        }
    }

    internal static Task GrantRemovalReward(HalfBodyHbrgoSaki card, CombatRoom room)
    {
        if (room?.CombatState == null || card == null || !card.IsMutable ||
            card.HasBeenRemovedFromState || !card.HasUpgradeEffect(1) ||
            card.Owner?.Creature.IsAlive != true || !room.CombatState.Players.Contains(card.Owner))
            return Task.CompletedTask;
        CardModel origin = GetOrigin(card);
        if (origin.HasBeenRemovedFromState || Consumed.GetOrCreateValue(origin).Used ||
            (card.DeckVersion != null && origin.Pile?.Type != PileType.Deck))
            return Task.CompletedTask;
        // The run hook visits the deck and then combat cards. A deck card and
        // its combat copy grant one reward together, including on repeated hooks.
        if (Rewarded.GetOrCreateValue(room).Add(origin))
            room.AddExtraReward(card.Owner, new CardRemovalReward(card.Owner));
        return Task.CompletedTask;
    }
}
