using System.Globalization;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Descriptions resolve the same authored strength and caster formula used by
// application. They never write resolved percentages into card variables.
internal static class HbrEffectCardDescription
{
    internal static void AddParameters(CardModel card, LocString text)
    {
        Add<HbrAttackUpPower>(card, text, "AttackUpPercent");
        Add<HbrCriticalRateUpPower>(card, text, "CriticalRate");
        Add<HbrCriticalDamageUpPower>(card, text, "CriticalDamage");
    }

    private static void Add<T>(CardModel card, LocString text, string variable) where T : HbrEffectPower
    {
        if (!card.DynamicVars.TryGetValue(variable, out var value)) return;
        HbrEffectPower power = ModelDb.Power<T>();
        Creature source = card.IsMutable ? card.Owner?.Creature : null;
        decimal amount = value.BaseValue;
        if (!HbrCardDescriptionContext.IsUnpowered(card) && source != null)
        {
            if (card.CombatState != null)
            {
                amount = Hook.ModifyPowerAmountGiven(card.CombatState, power, source,
                    amount, source, card, out _);
                amount = Hook.ModifyPowerAmountReceived(card.CombatState, power, source,
                    amount, source, out _);
            }
            amount = power.ResolvePotency(amount, source, source);
        }
        string number = amount.ToString("0.#", CultureInfo.InvariantCulture);
        text.Add(variable + "Display", value.WasJustUpgraded || amount > value.BaseValue
            ? "[green]" + number + "[/green]" : amount < value.BaseValue
                ? "[red]" + number + "[/red]" : number);
    }
}
