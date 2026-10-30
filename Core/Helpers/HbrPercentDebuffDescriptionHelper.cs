using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

// Adapt only this mod's descriptions, tagged by the exact HBR-owned instance.
// Native localization tables and DynamicVars stay untouched for legacy runs.
internal static class HbrPercentDebuffDescriptionHelper
{
    private sealed class Display
    {
        internal readonly HashSet<string> Names = new(StringComparer.Ordinal);
        internal string Original;
        internal string Converted;
    }

    private static readonly ConditionalWeakTable<LocString, Display> Displays = new();
    private static readonly Regex Grant = new(
        @"\{(?<name>[A-Za-z0-9_]+)(?::[^{}]*)?\}(?<closing>(?:\[/[A-Za-z]+\])*)层(?<power>\[gold\](?:虚弱|易伤)\[/gold\])",
        RegexOptions.CultureInvariant);

    internal static void Track(LocString text, IEnumerable<DynamicVar> variables)
    {
        if (text == null) return;
        Display display = Displays.GetValue(text, _ => new Display());
        foreach (DynamicVar variable in variables)
        {
            if (HbrPercentDebuffCardDescription.GetPower(variable) == null) continue;
            if (display.Names.Add(variable.Name)) display.Original = null;
        }
    }

    internal static string Rewrite(LocString text, string original)
    {
        if (string.IsNullOrEmpty(original) || !Displays.TryGetValue(text, out Display display)) return original;
        if (string.Equals(original, display.Original, StringComparison.Ordinal)) return display.Converted;
        display.Original = original;
        display.Converted = Grant.Replace(original, match => display.Names.Contains(match.Groups["name"].Value)
            ? "{" + match.Groups["name"].Value + "Percent}" + match.Groups["closing"].Value +
                "%的" + match.Groups["power"].Value + "（2回合）"
            : match.Value);
        // Meteor Shower uses one native variable for both grants.
        display.Converted = display.Converted.Replace(
            "[gold]虚弱[/gold]（2回合）和[gold]易伤[/gold]",
            "[gold]虚弱[/gold]和[gold]易伤[/gold]（各2回合）", StringComparison.Ordinal);
        display.Converted = display.Converted.Replace("虚弱", "攻击力下降", StringComparison.Ordinal)
            .Replace("易伤", "防御力下降", StringComparison.Ordinal);
        return display.Converted;
    }

    internal static bool IsEnabled(AbstractModel model) =>
        model.GetType().Assembly == typeof(HbrPercentDebuffDescriptionHelper).Assembly && model switch
    {
        CardModel card => HbrPercentDebuffCardDescription.IsEnabled(card),
        PotionModel potion => potion.IsMutable && HbrBattleStatsHelper.IsEnabled(potion.Owner),
        RelicModel relic => relic.IsMutable && HbrBattleStatsHelper.IsEnabled(relic.Owner),
        PowerModel power => power.IsMutable && HbrBattleStatsHelper.IsEnabled(power.Owner),
        _ => false
    };

    internal static IEnumerable<IHoverTip> RewriteHoverTips(AbstractModel model, IEnumerable<IHoverTip> tips)
    {
        string weak = ModelDb.Power<WeakPower>().Id.ToString();
        string vulnerable = ModelDb.Power<VulnerablePower>().Id.ToString();
        foreach (IHoverTip item in tips)
        {
            // The holder's own tooltip already includes its exact snapshot
            // and duration. Only references to a canonical concept are replaced.
            if (item is not HoverTip tip || model is PowerModel owned && tip.Id == owned.Id.ToString() ||
                tip.Id != weak && tip.Id != vulnerable)
            {
                yield return item;
                continue;
            }
            PowerModel power = tip.Id == weak ? ModelDb.Power<HbrAttackDownPower>() : ModelDb.Power<HbrDefenseDownPower>();
            LocString text = new("powers", power.Id.Entry + ".hbrReferenceDescription");
            HoverTip replacement = new(power, text.GetFormattedText(), false);
            replacement.SetCanonicalModel(power);
            yield return replacement;
        }
    }
}
