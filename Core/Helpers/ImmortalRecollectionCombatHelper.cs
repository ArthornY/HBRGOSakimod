using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ImmortalRecollectionCombatHelper
{
    private sealed class CardState { internal bool Played; internal int Growth; }
    private sealed class Battle { internal readonly ConditionalWeakTable<CardModel, CardState> Cards = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static CardState State(CardModel card)
    {
        if (card?.IsMutable != true || card.Owner?.Creature?.CombatState is not { } combat) return null;
        CardModel physical = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        return Battles.GetOrCreateValue(combat).Cards.GetOrCreateValue(physical);
    }
    internal static void InitializeCombat(CardModel card) => _ = State(card);
    internal static bool MarkPlayed(CardModel card)
    {
        CardState state = State(card);
        bool first = state?.Played != true;
        if (state != null) state.Played = true;
        return first;
    }
    internal static int Growth(CardModel card) => State(card)?.Growth ?? 0;
    internal static decimal DefenseBasis(ImmortalRecollectionHbrgoSaki card) =>
        (card.HasUpgradeEffect(ImmortalRecollectionHbrgoSaki.DestructionPlus) ? 40m : 30m) +
        (card.HasUpgradeEffect(ImmortalRecollectionHbrgoSaki.NecroDance) ? Growth(card) : 0);
    internal static void Advance(CardModel card)
    { if (State(card) is { } state) state.Growth = Math.Min(40, state.Growth + 10); }
    internal static async Task GrantPotion(Player player)
    {
        // Test space before the factory: a full bar must not consume even the
        // potion selection/rarity RNG and the first-play receipt stays spent.
        if (player?.HasOpenPotionSlots != true) return;
        PotionModel potion = PotionFactory.CreateRandomPotionInCombat(player, player.RunState.Rng.CombatPotionGeneration).ToMutable();
        await PotionCmd.TryToProcure(potion, player);
    }
}
