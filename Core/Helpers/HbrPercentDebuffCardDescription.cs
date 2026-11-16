using System;
using System.Globalization;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Presentation reads raw application counters, never a live power's duration.
// The same variables serve authored HBR templates and native card descriptions.
internal static class HbrPercentDebuffCardDescription
{
    internal static bool IsEnabled(CardModel card) => card != null && card.GetType().Assembly == typeof(HbrPercentDebuffCardDescription).Assembly &&
        (HbrSkillCardDescription.IsEnabled(card) ||
            card.IsMutable && HbrBattleStatsHelper.IsEnabled(card.Owner));

    internal static void AddParameters(CardModel card, LocString text)
    {
        if (text == null || !IsEnabled(card)) return;
        foreach (DynamicVar variable in card.DynamicVars.Values)
        {
            PowerModel power = GetPower(variable);
            if (power == null) continue;
            text.Add(variable.Name + "Percent", Format(card, power,
                variable.BaseValue, variable.WasJustUpgraded));
            text.Add(variable.Name + "Duration", HbrCardFaceDescription.Duration(
                HbrEffectProfileResolver.Resolve(power, card)));
        }
        text.Add("AttackDownDuration", HbrCardFaceDescription.Duration(
            HbrEffectProfileResolver.ForType(typeof(HbrAttackDownPower), card)));
        text.Add("DefenseDownDuration", HbrCardFaceDescription.Duration(
            HbrEffectProfileResolver.ForType(typeof(HbrDefenseDownPower), card)));
        HbrPercentDebuffDescriptionHelper.Track(text, card.DynamicVars.Values);
    }

    internal static PowerModel GetPower(DynamicVar variable) => variable switch
    {
        PowerVar<WeakPower> => ModelDb.Power<HbrAttackDownPower>(),
        PowerVar<VulnerablePower> => ModelDb.Power<HbrDefenseDownPower>(),
        PowerVar<HbrAttackDownPower> => ModelDb.Power<HbrAttackDownPower>(),
        PowerVar<HbrDefenseDownPower> => ModelDb.Power<HbrDefenseDownPower>(),
        PowerVar<HbrDpDefenseDownPower> => ModelDb.Power<HbrDpDefenseDownPower>(),
        _ => null
    };

    internal static string Format(CardModel card, PowerModel power, decimal modifiers, bool upgraded = false)
    {
        if (power is WeakPower) power = ModelDb.Power<HbrAttackDownPower>();
        else if (power is VulnerablePower) power = ModelDb.Power<HbrDefenseDownPower>();
        decimal basis = HbrPercentDebuffHelper.BasePercent(modifiers);
        decimal value = basis;
        Creature applier = card.IsMutable ? card.Owner?.Creature : null;
        Creature target = HbrCardDescriptionContext.GetTarget(card);
        if (card is LastStandHbrgoSaki || card.TargetType == TargetType.Self) target = applier;
        if (!HbrCardDescriptionContext.IsUnpowered(card) && target != null && applier != null)
        {
            decimal applied = card is MimicIceFlowerThousandArrowsHbrgoSaki
                ? ModeTransitionHelper.GetAdjustedEffectAmount(card, modifiers) : modifiers;
            value = HbrPercentDebuffHelper.ResolvePercent(power, applied, applier, target);
            if (decimal.Truncate(applied) > 0m)
                value = HotaruUnfadingScarsPower.EnhanceEffect(target, value);
        }
        string number = value.ToString("0.#", CultureInfo.InvariantCulture);
        return upgraded || value > basis ? $"[green]{number}[/green]" :
            value < basis ? $"[red]{number}[/red]" : number;
    }
}
