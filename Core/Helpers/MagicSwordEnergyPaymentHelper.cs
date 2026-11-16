using System;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal readonly record struct SwordheartPaymentQuote(
    int EnergyDeficit,
    int EnergyToSpend,
    int StarsToSpend,
    decimal SwordheartToSpend,
    bool HasEnoughSwordheart,
    bool HasEnoughStars)
{
    internal bool CanAfford => HasEnoughSwordheart && HasEnoughStars;
}

internal static class MagicSwordEnergyPaymentHelper
{
    private static readonly Dictionary<CardModel, int> PendingSwordheartCosts = new();

    public static bool CanPayWithSwordheart(CardModel card, out int deficit, out int energyToSpend, out int starsToSpend)
    {
        deficit = 0;
        energyToSpend = 0;
        starsToSpend = 0;

        if (!TryGetPaymentQuote(card, out SwordheartPaymentQuote quote)) return false;
        deficit = quote.EnergyDeficit;
        energyToSpend = quote.EnergyToSpend;
        starsToSpend = quote.StarsToSpend;
        return quote.CanAfford;
    }

    // Pure quotation shared by rendering, resource validation and payment.
    // An unaffordable Swordheart quote is still useful to render its red cost.
    internal static bool TryGetPaymentQuote(CardModel card, out SwordheartPaymentQuote quote)
    {
        quote = default;
        if (card?.IsMutable != true ||
            card.Owner?.Character is not HbrgoSaki31O ||
            card.EnergyCost.CostsX ||
            card.Owner.PlayerCombatState == null ||
            card.CombatState == null)
        {
            return false;
        }

        bool isInMagicSwordMode = card.Owner.Creature.HasPower<MagicSwordModePower>();
        if (!isInMagicSwordMode &&
            !card.Owner.Creature.HasPower<MagicSwordUniversalityPower>())
        {
            return false;
        }

        int energyRequired = card.EnergyCost.GetAmountToSpend();
        int currentEnergy = Math.Max(0, card.Owner.PlayerCombatState.Energy);
        if (energyRequired <= currentEnergy) return false;
        int energyToSpend = Math.Min(energyRequired, currentEnergy);
        int deficit = energyRequired - energyToSpend;
        int starsToSpend = Math.Max(0, card.GetStarCostWithModifiers());
        decimal swordheartCost = SwordheartClarityPower.GetSwordheartConsumptionAmount(card.Owner.Creature, deficit);
        int availableSwordheart = Math.Max(0, card.Owner.Creature.GetPowerAmount<SwordheartPower>());
        quote = new SwordheartPaymentQuote(deficit, energyToSpend, starsToSpend, swordheartCost,
            availableSwordheart >= swordheartCost, card.Owner.PlayerCombatState.Stars >= starsToSpend);
        return true;
    }

    public static bool CanPlayUsingSwordheart(CardModel card)
    {
        if (!CanPayWithSwordheart(card, out _, out _, out _))
        {
            return false;
        }

        if (card.CanPlay(out UnplayableReason reason, out _))
        {
            return true;
        }

        return (reason & ~UnplayableReason.EnergyCostTooHigh) == UnplayableReason.None;
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
