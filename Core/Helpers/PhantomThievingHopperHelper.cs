using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomThievingHopperHelper
{
    internal static async Task BeforeAttack(Creature phantom, MoveState move)
    {
        if (phantom?.Monster is not ThievingHopper ||
            move?.Id != "THIEVERY_MOVE" ||
            !phantom.HasPower<PhantomPower>() ||
            phantom.PetOwner is not { } owner ||
            owner.Creature?.IsAlive != true ||
            owner.Creature.Side != phantom.Side ||
            phantom.CombatState == null ||
            !ReferenceEquals(owner.Creature.CombatState, phantom.CombatState) ||
            owner.GetRelic<MomentOfReign>() is not { } relic)
        {
            return;
        }

        CardModel source = SelectSourceCard(owner);
        if (source == null)
        {
            return;
        }

        // Capture the permanent card before any generated-card hooks or later
        // combat changes. SavedProperties may themselves contain mutable arrays.
        SerializableCard permanent = CopySnapshot(source.DeckVersion.ToSerializable());
        relic.QueueThievingHopperCardReward(permanent);
        CardModel copy = null;
        try
        {
            copy = source.CreateClone();
            CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
                copy, PileType.Hand, owner);
            if (result.success)
            {
                return;
            }
        }
        catch
        {
            RemovePendingSnapshot(relic, permanent);
            copy?.RemoveFromState();
            throw;
        }

        RemovePendingSnapshot(relic, permanent);
        copy.RemoveFromState();
    }

    private static void RemovePendingSnapshot(
        MomentOfReign relic, SerializableCard snapshot)
    {
        int index = relic.PendingThievingHopperCardRewards.FindIndex(
            card => ReferenceEquals(card, snapshot));
        if (index >= 0)
        {
            relic.PendingThievingHopperCardRewards.RemoveAt(index);
        }
    }

    internal static CardModel SelectSourceCard(Player owner)
    {
        if (owner?.PlayerCombatState == null || owner.RunState == null)
        {
            return null;
        }

        bool IsOwnedDeckCard(CardModel card) =>
            card.Owner == owner && card.DeckVersion?.Owner == owner;

        CardModel[] candidates = owner.PlayerCombatState.DrawPile.Cards
            .Concat(owner.PlayerCombatState.DiscardPile.Cards)
            .Where(IsOwnedDeckCard)
            .Distinct()
            .ToArray();
        if (candidates.Length == 0)
        {
            candidates = owner.PlayerCombatState.Hand.Cards
                .Where(IsOwnedDeckCard)
                .Distinct()
                .ToArray();
        }

        if (candidates.Length == 0)
        {
            return null;
        }

        // Match ThievingHopper._stealPriorities, including its fallback when
        // none of the rarity/enchantment groups matches the available cards.
        int priority = candidates.Min(GetStealPriority);
        CardModel[] preferred = candidates
            .Where(card => GetStealPriority(card) == priority)
            .ToArray();
        return owner.RunState.Rng.CombatCardGeneration.NextItem(preferred);
    }

    private static int GetStealPriority(CardModel card)
    {
        if (card.Enchantment is not Imbued)
        {
            return card.Rarity switch
            {
                CardRarity.Uncommon => 0,
                CardRarity.Common or CardRarity.Rare or CardRarity.Event => 1,
                CardRarity.Basic or CardRarity.Quest => 2,
                CardRarity.Ancient => 3,
                _ => 4
            };
        }

        return 3;
    }

    internal static List<SerializableCard> CopyPendingRewards(
        IEnumerable<SerializableCard> pending)
    {
        return pending?.Where(card => card?.Id != null)
            .Select(CopySnapshot)
            .ToList() ?? new List<SerializableCard>();
    }

    private static SerializableCard CopySnapshot(SerializableCard source)
    {
        string json = JsonSerializer.Serialize(source, JsonSerializationUtility.Options);
        return JsonSerializer.Deserialize<SerializableCard>(
            json, JsonSerializationUtility.Options);
    }

    internal static void GrantPendingRewards(
        CombatRoom room,
        Player owner,
        List<SerializableCard> pending)
    {
        if (room == null || owner?.RunState == null || pending == null)
        {
            return;
        }

        while (pending.Count > 0)
        {
            SerializableCard snapshot = pending[0];
            if (snapshot?.Id == null)
            {
                pending.RemoveAt(0);
                continue;
            }

            CardModel card = CardModel.FromSerializable(CopySnapshot(snapshot));
            // Register the reward in run scope without adding it to the deck.
            // SpecialCardReward adds it to the deck only when the player claims it;
            // skipping the reward and room serialization use the native paths.
            owner.RunState.AddCard(card, owner);
            room.AddExtraReward(owner, new SpecialCardReward(card, owner));
            pending.RemoveAt(0);
        }
    }
}
