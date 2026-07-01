using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatFlowHelper
{
    public static async Task FinishAutoPlayedCard(CardModel card, bool checkWinCondition = true)
    {
        await RemoveFromPlayPileIfStillInCombat(card);
        if (checkWinCondition)
        {
            await CheckWinConditionIfEnding();
        }
    }

    private static async Task RemoveFromPlayPileIfStillInCombat(CardModel card)
    {
        if (card.HasBeenRemovedFromState || card.Pile?.Type != PileType.Play)
        {
            return;
        }

        await CardPileCmd.RemoveFromCombat(card);
    }

    private static async Task CheckWinConditionIfEnding()
    {
        if (!CombatManager.Instance.IsInProgress || !CombatManager.Instance.IsEnding)
        {
            return;
        }

        await CombatManager.Instance.CheckWinCondition();
    }
}
