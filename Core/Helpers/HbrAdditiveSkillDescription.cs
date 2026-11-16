using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// These effects add skill units to a future attack. Keep stacks and native
// variables intact; describe the added coefficient rather than literal HP.
internal static class HbrAdditiveSkillDescription
{
    private sealed record Source(string Table, string Entry, string Variable);
    private sealed record Display(Source Source, LocString Legacy, decimal Fallback,
        bool GenericAtZero, bool Upgraded);
    private static readonly Dictionary<string, Source> Sources = new(StringComparer.Ordinal)
    {
        ["TaintedPower"] = new("powers", "TAINTED_POWER", "Amount"),
        ["AccuracyPower"] = new("powers", "ACCURACY_POWER", "Amount"),
        ["CalcifyPower"] = new("powers", "CALCIFY_POWER", "Amount"),
        ["PhantomBladesPower"] = new("powers", "PHANTOM_BLADES_POWER", "Amount"),
        ["LeadershipPower"] = new("powers", "LEADERSHIP_POWER", "Amount"),
        ["PhantomPollutionPower"] = new("powers", "PHANTOM_POLLUTION_POWER", "Amount"),
        ["VitalSparkPower"] = new("powers", "VITAL_SPARK_POWER", "Amount"),
        ["Accuracy"] = new("cards", "ACCURACY", "AccuracyPower"),
        ["Calcify"] = new("cards", "CALCIFY", "CalcifyPower"),
        ["PhantomBlades"] = new("cards", "PHANTOM_BLADES", "PhantomBladesPower"),
        ["FakeStrikeDummy"] = new("relics", "FAKE_STRIKE_DUMMY", "ExtraDamage"),
        ["MysticLighter"] = new("relics", "MYSTIC_LIGHTER", "Damage"),
        ["Tainted"] = new("afflictions", "TAINTED", "Amount")
    };
    private static readonly ConditionalWeakTable<LocString, Display> Displays = new();
    [ThreadStatic] private static HoverScope _hover;

    private sealed class HoverScope : IDisposable
    {
        internal bool Enabled;
        internal HoverScope Previous;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_hover, this)) _hover = Previous;
        }
    }

    private static bool IsEnabled(AbstractModel model)
    {
        if (model is AfflictionModel affliction)
            return affliction.IsMutable && affliction.HasCard
                ? HbrBattleStatsHelper.IsEnabled(affliction.Card.Owner)
                : _hover?.Enabled == true;
        if (model is CardModel or RelicModel)
            return HbrNativeEffectSkillHelper.IsEnabled(model);
        if (model?.IsMutable == true && HbrNativeEffectSkillHelper.GetModeOwner(model) is { } owner)
            return HbrBattleStatsHelper.IsEnabled(owner);
        return _hover?.Enabled == true;
    }

    internal static IDisposable BeginHover(AbstractModel model)
    {
        // Canonical/legacy nested cards shadow a surrounding HBR hover. A
        // cardless affliction or canonical power instead inherits its parent.
        HoverScope scope = new() { Enabled = IsEnabled(model), Previous = _hover };
        _hover = scope;
        return scope;
    }

    internal static LocString Adapt(AbstractModel model, LocString original, bool extraCardText)
    {
        if (model == null || original == null || Displays.TryGetValue(original, out _) ||
            !Sources.TryGetValue(model.GetType().Name, out Source source) || !IsEnabled(model)) return original;
        // A mod cannot silently opt an unrelated class with the same name in.
        if (model.GetType().Assembly != typeof(CardModel).Assembly &&
            model.GetType() != typeof(HBRGOSakiMod.Core.Models.Powers.PhantomPollutionPower)) return original;
        string suffix = extraCardText ? ".hbrAdditiveExtraCardText" : ".hbrAdditiveDescription";
        if (HbrLocalizedText.Local(source.Table, source.Entry + suffix) == null) return original;
        LocString text = new(source.Table, source.Entry + suffix);
        text.AddVariablesFrom(original);
        decimal fallback = model switch
        {
            PowerModel power when power.IsMutable => power.Amount,
            AfflictionModel affliction when affliction.IsMutable => affliction.Amount,
            _ => 0m
        };
        bool upgraded = false;
        DynamicVar variable = model switch
        {
            CardModel card => card.DynamicVars.TryGetValue(source.Variable, out DynamicVar value) ? value : null,
            RelicModel relic => relic.DynamicVars.TryGetValue(source.Variable, out DynamicVar value) ? value : null,
            _ => null
        };
        if (variable != null)
        {
            fallback = variable.BaseValue;
            upgraded = variable.WasJustUpgraded;
        }
        // Some languages have an HBR template without the legacy mod text.
        // Read that fallback only if the final amount actually needs it.
        Displays.Add(text, new Display(source, original, fallback,
            model is PowerModel && !model.IsMutable, upgraded));
        return text;
    }

    internal static string AdaptText(LocString text, string raw)
    {
        if (text == null || !Displays.TryGetValue(text, out Display display)) return raw;
        decimal amount = display.Fallback;
        bool upgraded = display.Upgraded;
        if (text.Variables.TryGetValue(display.Source.Variable, out object value))
        {
            if (value is DynamicVar variable)
            {
                amount = variable.BaseValue;
                upgraded = variable.WasJustUpgraded;
            }
            else if (value is IConvertible && decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                         NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number)) amount = number;
        }
        // The native dumb tooltip supplies its override after Description is
        // read. Resolve the final Amount here, including canonical power tips.
        if (amount <= 0m)
        {
            if (amount < 0m || !display.GenericAtZero) return display.Legacy.GetRawText();
            return HbrLocalizedText.Local(display.Source.Table,
                display.Source.Entry + ".hbrAdditiveRule") ?? display.Legacy.GetRawText();
        }
        string percent = HbrNativeEffectSkillHelper.FormatPercent(amount);
        text.Add("HbrAdditivePercent", upgraded ? "[green]" + percent + "[/green]" : percent);
        return raw;
    }
}
