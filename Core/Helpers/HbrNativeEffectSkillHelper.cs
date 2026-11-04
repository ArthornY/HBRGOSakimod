using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Native values remain native units: six is one 100% skill coefficient. This
// registry declares effect sources, independently of their native attack flags.
internal static class HbrNativeEffectSkillHelper
{
    private static readonly AccessTools.FieldRef<DynamicVar, AbstractModel> VariableOwner =
        AccessTools.FieldRefAccess<DynamicVar, AbstractModel>("_owner");
    private static readonly HashSet<Type> Models = BuildRegistry();
    private static readonly ConditionalWeakTable<LocString, Display> Displays = new();
    [ThreadStatic] private static MerchantDisplay _merchantDisplay;

    private sealed class Display
    {
        internal readonly Dictionary<string, SkillValue> Variables = new(StringComparer.Ordinal);
    }

    private sealed record SkillValue(decimal RawUnits, bool IsBlock);

    private sealed class MerchantDisplay : IDisposable
    {
        internal RelicModel Relic;
        internal Player Player;
        internal MerchantDisplay Previous;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _merchantDisplay = Previous;
        }
    }

    private static HashSet<Type> BuildRegistry()
    {
        HashSet<Type> result = new();
        foreach (string name in new[] { "Cards.Burn", "Cards.Decay", "Cards.Infection", "Cards.Toxic",
                     "Cards.Wither", "Events.AbyssalBaths", "Events.RoomFullOfCheese",
                     "Events.RoundTeaParty", "Events.TheLegendsWereTrue", "Relics.Kusarigama",
                     "Relics.MiniatureCannon", "Relics.Anchor" })
            result.Add(typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models." + name, true));
        return result;
    }

    internal static bool IsSkillModel(AbstractModel model) => model != null &&
        (Models.Contains(model.GetType()) || model is PotionModel &&
            model.GetType().Assembly == typeof(PotionModel).Assembly);

    internal static bool IsSkillVariable(DynamicVar variable) => variable is DamageVar &&
        IsSkillModel(VariableOwner(variable));

    private static bool IsAnchorBlock(DynamicVar variable) => variable is BlockVar &&
        VariableOwner(variable) is Anchor && variable.Name == "Block";

    internal static bool TryResolveAnchorBlock(BlockVar variable, Creature target, out decimal amount)
    {
        amount = 0m;
        if (variable == null || variable.Props != ValueProp.Unpowered ||
            VariableOwner(variable) is not Anchor { IsMutable: true } anchor ||
            !ReferenceEquals(anchor.Owner?.Creature, target) ||
            !ReferenceEquals(anchor.DynamicVars.Block, variable) ||
            !HbrBattleStatsHelper.TryGetStats(target, out HbrBattleStats stats)) return false;
        amount = HbrSkillPower.EvaluateBlockFromUnits(variable.BaseValue, stats.Intelligence);
        return true;
    }

    internal static Creature GetModeOwner(DynamicVar variable) =>
        GetModeOwner(variable == null ? null : VariableOwner(variable));

    internal static Creature GetModeOwner(AbstractModel model)
    {
        // Canonical templates are not allowed to read model Owner properties.
        if (model?.IsMutable != true) return null;
        return model switch
        {
            CardModel card => card.Owner?.Creature,
            RelicModel relic => relic.Owner?.Creature,
            PotionModel potion => potion.Owner?.Creature,
            EventModel eventModel => eventModel.Owner?.Creature,
            _ => null
        };
    }

    internal static bool IsEnabled(AbstractModel model) =>
        HbrBattleStatsHelper.IsEnabled(GetModeOwner(model)) ||
        model is Kusarigama or MiniatureCannon or Anchor && ReferenceEquals(_merchantDisplay?.Relic, model) &&
        HbrBattleStatsHelper.IsEnabled(_merchantDisplay.Player);

    internal static IDisposable BeginMerchantDisplay(RelicModel relic, Player player)
    {
        MerchantDisplay scope = new() { Relic = relic, Player = player, Previous = _merchantDisplay };
        _merchantDisplay = scope;
        return scope;
    }

    internal static void TagDescription(AbstractModel model, LocString text)
    {
        if (text == null || !IsSkillModel(model)) return;
        Displays.Remove(text);
        if (!IsEnabled(model)) return;
        DynamicVarSet vars = model switch
        {
            CardModel card => card.DynamicVars,
            RelicModel relic => relic.DynamicVars,
            PotionModel potion => potion.DynamicVars,
            EventModel eventModel => eventModel.DynamicVars,
            _ => null
        };
        if (vars == null) return;
        foreach (DynamicVar variable in vars.Values)
            if (IsAnchorBlock(variable))
                TagSkillDescription(text, variable.Name, variable.BaseValue, isBlock: true);
            else if (IsSkillVariable(variable) || model is MiniatureCannon && variable.Name == "ExtraDamage")
                TagDescription(text, variable.Name, variable.BaseValue);
    }

    internal static void TagDescription(LocString text, string variable, decimal rawUnits)
        => TagSkillDescription(text, variable, rawUnits, isBlock: false);

    private static void TagSkillDescription(LocString text, string variable, decimal rawUnits, bool isBlock)
    {
        if (text == null) return;
        Displays.GetValue(text, _ => new Display()).Variables[variable] = new SkillValue(rawUnits, isBlock);
    }

    internal static string FormatPercent(decimal rawUnits) =>
        HbrSkillPower.DamagePercent(rawUnits).ToString("0.##", CultureInfo.InvariantCulture);

    internal static string AdaptText(LocString text, string raw)
    {
        if (string.IsNullOrEmpty(raw) || !Displays.TryGetValue(text, out Display display)) return raw;
        // Replace the complete formatter, including diff/plural nested braces.
        // Feeding a percentage string into a native numeric formatter is invalid.
        foreach ((string variable, SkillValue value) in display.Variables)
        {
            string parameter = "HbrEffectDamageSkill" + variable;
            LocString skill = new("static_hover_tips", "HBR_NATIVE_EFFECT_SKILL_DAMAGE.description");
            skill.Add("SkillPower", value.IsBlock
                ? HbrSkillPower.BlockPercent(value.RawUnits).ToString("0.##", CultureInfo.InvariantCulture)
                : FormatPercent(value.RawUnits));
            text.Add(parameter, skill.GetFormattedText());
            raw = ReplaceVariable(raw, variable, parameter, value.IsBlock);
        }
        return raw;
    }

    private static string ReplaceVariable(string raw, string variable, string parameter, bool isBlock)
    {
        for (int cursor = 0; cursor < raw.Length; cursor++)
        {
            if (raw[cursor] != '{') continue;
            int nameStart = cursor + 1;
            while (nameStart < raw.Length && char.IsWhiteSpace(raw[nameStart])) nameStart++;
            if (nameStart + variable.Length >= raw.Length ||
                string.CompareOrdinal(raw, nameStart, variable, 0, variable.Length) != 0) continue;
            int nameEnd = nameStart + variable.Length;
            if (raw[nameEnd] is not ('}' or ':' or '.' or '[') && !char.IsWhiteSpace(raw[nameEnd])) continue;
            int end = cursor + 1;
            int depth = 1;
            while (end < raw.Length && depth > 0)
            {
                if (raw[end] == '{') depth++;
                else if (raw[end] == '}') depth--;
                end++;
            }
            if (depth != 0) return raw;
            // Chinese native descriptions put a point unit after the formatter;
            // the replacement is a coefficient rather than a count of points.
            if (end < raw.Length && raw[end] == '点') end++;
            else if (isBlock)
            {
                // Anchor colors the value, placing its point unit after the
                // closing color tag. Preserve the tags while removing that unit.
                int unit = end;
                while (unit + 1 < raw.Length && raw[unit] == '[' && raw[unit + 1] == '/')
                {
                    int close = raw.IndexOf(']', unit + 2);
                    if (close < 0) break;
                    unit = close + 1;
                }
                if (unit < raw.Length && raw[unit] == '点') raw = raw.Remove(unit, 1);
            }
            string replacement = "{" + parameter + "}";
            raw = raw[..cursor] + replacement + raw[end..];
            cursor += replacement.Length - 1;
        }
        return raw;
    }
}
