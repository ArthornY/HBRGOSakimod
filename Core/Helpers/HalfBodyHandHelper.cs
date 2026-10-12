using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HalfBodyHandHelper
{
    internal static int CountInHand(Player player) =>
        player?.PlayerCombatState?.Hand.Cards.Count(card =>
            card is HalfBodyHbrgoSaki && !card.HasBeenRemovedFromState &&
            ReferenceEquals(card.Owner, player)) ?? 0;

    internal static int GetEffectiveMaxCardsInHand(Player player) =>
        (int)Math.Clamp((long)CardPile.MaxCardsInHand - CountInHand(player) +
            (player?.Creature?.GetPowerAmount<PhantomHandCapacityPower>() ?? 0),
            0L, int.MaxValue);

    internal static int GetRollPenalty(Player player) => -10 * CountInHand(player);

    internal static int GetEffectiveMaxCardsInHandForCard(CardModel card)
    {
        int incomingPenalty = card is HalfBodyHbrgoSaki && card.Pile?.Type != PileType.Hand ? 1 : 0;
        return Math.Max(0, GetEffectiveMaxCardsInHand(card?.Owner) - incomingPenalty);
    }
}
