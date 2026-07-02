using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MagicSwordEnergyPaymentHelper
{
    private static readonly Dictionary<CardModel, int> PendingSwordheartCosts = new();

    public static bool CanPayWithSwordheart(CardModel card, out int deficit, out int energyToSpend, out int starsToSpend)
    {
        deficit = 0;
        energyToSpend = 0;
        starsToSpend = 0;

        if (card == null || card.EnergyCost.CostsX || card.Owner?.PlayerCombatState == null || card.CombatState == null)
        {
            return false;
        }

        if (!card.Owner.Creature.HasPower<MagicSwordModePower>())
        {
            return false;
        }

        SwordheartPower swordheart = card.Owner.Creature.GetPower<SwordheartPower>();
        if (swordheart == null || swordheart.Amount <= 0)
        {
            return false;
        }

        energyToSpend = card.EnergyCost.GetAmountToSpend();
        starsToSpend = int.Max(0, card.GetStarCostWithModifiers());

        int currentEnergy = card.Owner.PlayerCombatState.Energy;
        if (energyToSpend <= currentEnergy)
        {
            return false;
        }

        deficit = energyToSpend - currentEnergy;
        decimal swordheartCost = SwordheartClarityPower.GetSwordheartConsumptionAmount(card.Owner.Creature, deficit);
        if (deficit <= 0 || swordheart.Amount < swordheartCost)
        {
            return false;
        }

        return starsToSpend <= card.Owner.PlayerCombatState.Stars;
    }

    public static void RecordPendingSwordheartCost(CardModel card, int amount)
    {
        if (amount <= 0)
        {
            PendingSwordheartCosts.Remove(card);
            return;
        }

        PendingSwordheartCosts[card] = amount;
    }

    public static bool TryTakePendingSwordheartCost(CardModel card, out int amount)
    {
        if (!PendingSwordheartCosts.TryGetValue(card, out amount))
        {
            return false;
        }

        PendingSwordheartCosts.Remove(card);
        return amount > 0;
    }
}
