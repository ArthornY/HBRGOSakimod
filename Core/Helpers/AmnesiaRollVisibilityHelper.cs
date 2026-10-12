using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

internal static class AmnesiaRollVisibilityHelper
{
    public static bool IsRollHidden(Player player)
    {
        if (player == null)
        {
            return false;
        }

        if (player.Deck.Cards.Any(
                static card => card is AmnesiaHbrgoSaki))
        {
            return true;
        }

        return player.PlayerCombatState?.AllCards.Any(
            static card =>
                card is AmnesiaHbrgoSaki &&
                !card.HasBeenRemovedFromState) == true;
    }
}
