using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class SwordheartSpendHelper
{
    public static async Task<bool> TrySpend(
        PlayerChoiceContext choiceContext,
        Creature owner,
        decimal amount,
        CardModel cardSource = null)
    {
        SwordheartPower swordheart = owner?.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0 || amount <= 0m)
        {
            return false;
        }

        MagicSwordModePower magicSwordMode = owner.GetPower<MagicSwordModePower>();
        if (magicSwordMode != null)
        {
            await magicSwordMode.SpendSwordheart(choiceContext, amount, cardSource);
            return true;
        }

        decimal amountToConsume = SwordheartClarityPower.GetSwordheartConsumptionAmount(owner, amount);
        decimal consumedAmount = Math.Min(swordheart.Amount, amountToConsume);
        if (consumedAmount <= 0m)
        {
            return false;
        }

        await PowerCmd.ModifyAmount(choiceContext, swordheart, -consumedAmount, owner, cardSource);
        return true;
    }
}
