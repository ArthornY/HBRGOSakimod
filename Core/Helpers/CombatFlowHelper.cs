using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatFlowHelper
{
    public static async Task FinishAutoPlayedCard(CardModel card)
    {
        await RemoveFromPlayPileIfStillInCombat(card);
    }

    private static async Task RemoveFromPlayPileIfStillInCombat(CardModel card)
    {
        if (card.HasBeenRemovedFromState || card.Pile?.Type != PileType.Play)
        {
            return;
        }

        await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
    }
}
