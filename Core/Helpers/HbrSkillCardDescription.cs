using System;
using System.Globalization;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Card faces show numeric effects. Only the inspect renderer adds coefficient
// suffixes. Presentation never writes resolved values back into native variables.
internal static class HbrSkillCardDescription
{
    internal static bool IsEnabled(CardModel card) => card != null &&
        card.GetType().Assembly == typeof(HbrSkillCardDescription).Assembly &&
        (!card.IsMutable || card.Owner == null || HbrBattleStatsHelper.IsEnabled(card.Owner));

    internal static string FormatPercent(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static void AddParameters(CardModel card, LocString description)
    {
        if (description == null) return;
        HbrFixedResourceDescriptionHelper.AddParameters(card, description);
        HbrFragileCardDescription.AddParameters(card, description);
        HbrPercentDebuffCardDescription.AddParameters(card, description);
        foreach (DynamicVar variable in card.DynamicVars.Values)
        {
            if (variable is not IHbrSkillPowerVar skill) continue;
            decimal units = variable is CalculatedVar calculated
                ? calculated.Calculate(HbrCardDescriptionContext.GetTarget(card)) : variable.BaseValue;
            bool fragileDamage = variable is DamageVar && variable.Name == "Damage" &&
                !HbrCardDescriptionContext.IsUnpowered(card) &&
                card is PiercingShotHbrgoSaki or StrongShotHbrgoSaki;
            if (fragileDamage)
                units = card is PiercingShotHbrgoSaki piercing
                    ? piercing.GetDamageUnits(HbrCardDescriptionContext.GetTarget(card))
                    : ((StrongShotHbrgoSaki)card).GetDamageUnits();
            decimal percent = fragileDamage ? HbrSkillPower.DamagePercent(units) : skill.SkillPowerPercent;
            description.Add(variable.Name + "Skill", FormatPercent(percent));
            description.Add(variable.Name + "SkillDetail", Detail(card, percent));
            bool block = skill.SkillPowerKind == HbrSkillPowerKind.Block;
            decimal basisUnits = variable is CalculatedVar ? units : variable.EnchantedValue;
            decimal basis = (IsEffectVar(variable) || IsDeferredBlock(variable)) &&
                !HbrCardDescriptionContext.IsUnpowered(card)
                ? BaseAmount(card, basisUnits, block) : UnitBaseAmount(units, block);
            decimal value = fragileDamage && CanCalculate(card)
                ? CalculateLive(card, units, ((DamageVar)variable).Props, false)
                : DisplayAmount(card, variable, units, block);
            description.Add(variable.Name + "Display", Number(value, basis, variable.WasJustUpgraded));
            if (skill.SkillPowerKind == HbrSkillPowerKind.DamageAndBlock)
            {
                decimal blockPercent = HbrSkillPower.BlockPercent(variable.BaseValue);
                description.Add(variable.Name + "BlockSkill",
                    FormatPercent(blockPercent));
                description.Add(variable.Name + "BlockSkillDetail", Detail(card, blockPercent));
                description.Add(variable.Name + "BlockDisplay", Number(
                    HbrSkillPower.BlockBaseFromUnits(units), HbrSkillPower.BlockBaseFromUnits(units),
                    variable.WasJustUpgraded));
            }
            // Retain the old parameter for nested third-party templates, while
            // authored HBR descriptions use the direct numeric display above.
            description.Add(variable.Name + "SkillPreview", string.Empty);
        }
        AddFixed(description, card, "FixedDamageOne", 1m, false);
        AddFixed(description, card, "FixedDamageTwo", 2m, false);
        AddFixed(description, card, "FixedBlockOne", 1m, true);
    }

    internal static void AddDamageParameters(CardModel card, LocString text, string name, decimal nativeUnits)
    {
        if (text == null || !IsEnabled(card)) return;
        decimal amount = BaseAmount(card, nativeUnits, false);
        if (CanCalculate(card))
            amount = CalculateLive(card, nativeUnits, ValueProp.Move, false);
        text.Add(name + "Display", Number(amount, BaseAmount(card, nativeUnits, false), false));
        text.Add(name + "SkillDetail", Detail(card, HbrSkillPower.DamagePercent(nativeUnits)));
    }

    private static decimal DisplayAmount(CardModel card, DynamicVar variable, decimal units, bool block)
    {
        // These cards store a future block grant as a raw ability counter.
        // Native preview intentionally leaves that counter unconverted.
        if (IsDeferredBlock(variable))
            return HbrCardDescriptionContext.IsUnpowered(card)
                ? UnitBaseAmount(units, true) : BaseAmount(card, units, true);
        if (!IsEffectVar(variable) || HbrCardDescriptionContext.IsUnpowered(card))
            return UnitBaseAmount(units, block);
        if (HbrCardDescriptionContext.UsesNativePreview(card)) return variable.PreviewValue;
        ValueProp props = variable switch
        {
            DamageVar damage => damage.Props,
            BlockVar gain => gain.Props,
            CalculatedDamageVar damage => damage.Props,
            CalculatedBlockVar gain => gain.Props,
            _ => ValueProp.Move
        };
        if (!props.IsPoweredAttack()) return units;
        if (CanCalculate(card))
            return CalculateLive(card, units, props, block);
        // There is no target outside combat. Show the single-coefficient base
        // instead of inventing an enemy or displaying the compatibility raw 6.
        return BaseAmount(card, variable is CalculatedVar ? units : variable.EnchantedValue, block);
    }

    private static bool IsEffectVar(DynamicVar variable) =>
        variable is DamageVar or BlockVar or CalculatedDamageVar or CalculatedBlockVar;

    private static bool IsDeferredBlock(DynamicVar variable) =>
        variable is BlockVar block && !block.Props.IsPoweredAttack() ||
        variable is PowerVar<RefuseToLeavePower>;

    private static bool CanCalculate(CardModel card) =>
        !HbrCardDescriptionContext.IsUnpowered(card) &&
        card.IsMutable && card.Owner?.RunState != null && card.CombatState != null;

    private static decimal CalculateLive(CardModel card, decimal units, ValueProp props, bool block)
    {
        if (block)
            return Hook.ModifyBlock(card.CombatState, card.Owner.Creature, units, props, card, null, out _);
        // A visual refresh may run inside a real hit. Previewing must not roll
        // shields/criticals or capture this value as that hit's damage.
        StrongestShieldDamagePatch.BeginIntentPreview();
        try
        {
            using var mode = ModeTransitionHelper.BeginDamagePreview(card, true);
            return DamageLimitHelper.CapSingleHit(HookCompatibility.ModifyDamage(
                card.Owner.RunState, card.CombatState, HbrCardDescriptionContext.GetTarget(card),
                card.Owner.Creature, units, props, card, ModifyDamageHookType.All,
                CardPreviewMode.Normal, out _));
        }
        finally { StrongestShieldDamagePatch.EndIntentPreview(); }
    }

    private static decimal UnitBaseAmount(decimal units, bool block) => block
        ? HbrSkillPower.BlockBaseFromUnits(units) : HbrSkillPower.DamageBaseFromUnits(units);

    private static decimal BaseAmount(CardModel card, decimal units, bool block)
    {
        HbrBattleStats stats = default;
        bool owned = card.IsMutable && HbrBattleStatsHelper.TryGetStats(card.Owner?.Creature, out stats);
        if (!block)
        {
            if (!owned || card.CombatState == null) return HbrSkillPower.DamageBaseFromUnits(units);
            var target = HbrCardDescriptionContext.GetTarget(card);
            stats = HbrStatPowerHelper.GetOutgoingAttack(target, card.Owner.Creature, stats, card, ValueProp.Move);
            var weights = target?.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m
                ? HbrDamageSkillAdapter.DpWeights : HbrDamageSkillAdapter.HpWeights;
            return HbrSkillPower.EvaluateDamageFromUnits(units,
                HbrOriginalNumericFormula.EvaluateWeightedStat(stats, weights),
                HbrEnemyProfileHelper.GetEnemyParameter(target));
        }
        decimal intelligence = owned ? stats.Intelligence : HbrBattleStatsHelper.InitialStat;
        return HbrSkillPower.EvaluateBlockFromUnits(units, intelligence);
    }

    private static void AddFixed(LocString text, CardModel card, string name, decimal units, bool block)
    {
        decimal value = block ? HbrSkillPower.BlockBaseFromUnits(units) : HbrSkillPower.DamageBaseFromUnits(units);
        text.Add(name + "Display", Number(value, value, false));
        text.Add(name + "SkillDetail", Detail(card, block
            ? HbrSkillPower.BlockPercent(units) : HbrSkillPower.DamagePercent(units)));
    }

    private static string Detail(CardModel card, decimal percent) =>
        HbrCardDescriptionContext.IsDetail(card) ? $"（{FormatPercent(percent)}%）" : string.Empty;

    private static string Number(decimal value, decimal basis, bool upgraded)
    {
        value = decimal.Floor(Math.Max(0m, value));
        basis = decimal.Floor(Math.Max(0m, basis));
        string number = value.ToString("0", CultureInfo.InvariantCulture);
        return upgraded || value > basis ? $"[green]{number}[/green]" :
            value < basis ? $"[red]{number}[/red]" : number;
    }
}
