using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Keywords;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed record HbrManualGlossaryEntry(
    string Id, string Title, string Description, string Category, string IconPath,
    Rect2? IconRegion = null);

/// <summary>
/// Reads the localized reference catalog without evaluating owner-dependent power descriptions.
/// Rebuild on opening the manual so locale changes and newly registered content remain visible.
/// </summary>
internal static class HbrManualGlossaryCatalog
{
    private const string TitleSuffix = ".title";
    private const string SixSensePrefix = "HBRGO_SIX_SENSE";
    private static readonly CompareInfo TitleComparison = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;
    private const CompareOptions TextComparisonOptions = CompareOptions.IgnoreCase |
        CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;
    private static readonly Regex Markup = new(@"\[[^\]\r\n]*\]", RegexOptions.Compiled);
    private static readonly IComparer<HbrManualGlossaryEntry> EntryOrder =
        Comparer<HbrManualGlossaryEntry>.Create((left, right) =>
        {
            int comparison = TitleComparison.Compare(left.Title, right.Title, TextComparisonOptions);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.Id, right.Id);
        });

    // Kept registered only so older saves and combat states load; nothing gives
    // them any more, so the manual leaves them out. The six-sense card effects and
    // resistances were removed on 2026-11-09, together with the cards built on
    // resistances. Narara's new opposed-roll nullification is active and is
    // deliberately not part of this retired set.
    internal static readonly IReadOnlySet<Type> RetiredPowerTypes = new HashSet<Type>
    {
        typeof(SixSenseResistancePower), typeof(SixSenseCombatPower), typeof(SixSenseLeadershipPower),
        typeof(SixSenseGentlenessPower), typeof(SixSenseMentalStrengthPower), typeof(GodspeedPower),
        typeof(HastePower), typeof(BreakInsightPower),
        typeof(StrengthenPower), typeof(NextRollBonusPower), typeof(ExtremeDodgeRollPenaltyPower),
        typeof(CrushYouLikeABugReplayPower), typeof(DualSwordsCounterPower), typeof(BowCounterPower),
        typeof(QuickSwitchPower)
    };

    internal static IReadOnlyList<HbrManualGlossaryEntry> Build(bool hbrNumericsEnabled = false)
    {
        Dictionary<string, HbrManualGlossaryEntry> entries = new(StringComparer.Ordinal);
        LocTable powers = LocManager.Instance.GetTable("powers");
        foreach (Type type in HbrManualGlossaryDiscovery.ReferencedPowerTypes.Where(type => !RetiredPowerTypes.Contains(type)))
        {
            PowerModel power = ModelDb.DebugPower(type);
            AddEntry(entries, powers, "powers", power.Id.Entry,
                type.Assembly == typeof(HbrManualGlossaryCatalog).Assembly ? "HBR 能力" : "通用能力",
                GetPowerIconPath(power), powers.HasEntry(power.Id.Entry + ".description")
                    ? string.Empty : GetInheritedPowerDescription(powers, type),
                hbrNumericsEnabled: hbrNumericsEnabled);
        }

        HashSet<string> commonKeywords = Enum.GetValues<CardKeyword>()
            .Select(keyword => StringHelper.Slugify(keyword.ToString())).ToHashSet(StringComparer.Ordinal);
        LocTable keywords = LocManager.Instance.GetTable("card_keywords");
        foreach (string prefix in GetPrefixes(keywords).Where(prefix =>
                     prefix.StartsWith("HBRGO_", StringComparison.Ordinal) || commonKeywords.Contains(prefix)))
            AddEntry(entries, keywords, "card_keywords", prefix, commonKeywords.Contains(prefix) ? "通用词条" : "HBR 词条",
                hbrNumericsEnabled: hbrNumericsEnabled);

        // A keyword can use its own localization tables instead of card_keywords.
        // Registered definitions also supply the actual icon when one was provided.
        foreach (ModKeywordDefinition definition in ModKeywordRegistry.GetDefinitionsSnapshot())
        {
            if (!string.Equals(definition.ModId, "HBRGOSakiMod", StringComparison.OrdinalIgnoreCase)) continue;
            LocTable titleTable = LocManager.Instance.GetTable(definition.TitleTable);
            LocTable descriptionTable = LocManager.Instance.GetTable(definition.DescriptionTable);
            if (!titleTable.HasEntry(definition.TitleKey)) continue;

            string prefix = definition.TitleKey.EndsWith(TitleSuffix, StringComparison.Ordinal)
                ? definition.TitleKey[..^TitleSuffix.Length] : definition.TitleKey;
            string id = definition.TitleTable + ":" + prefix;
            string title = PlainText(titleTable.GetRawText(definition.TitleKey));
            if (string.IsNullOrWhiteSpace(title)) continue;
            string raw = descriptionTable.HasEntry(definition.DescriptionKey)
                ? descriptionTable.GetRawText(definition.DescriptionKey) : string.Empty;
            // Preserve the richer per-card-type/character variants gathered from card_keywords.
            if (entries.TryGetValue(id, out HbrManualGlossaryEntry existing))
                entries[id] = existing with { IconPath = definition.IconPath ?? existing.IconPath };
            else
                entries[id] = CreateEntry(id, title, raw, "HBR 词条", definition.IconPath ?? string.Empty);
        }

        // These mechanics are authored as hand-made hover tips/relic rules rather than powers.
        LocTable relics = LocManager.Instance.GetTable("relics");
        foreach (RelicModel relic in new RelicModel[]
                 { ModelDb.Relic<Overclock>(), ModelDb.Relic<DeflectorProtection>(), ModelDb.Relic<MomentOfReign>(),
                     ModelDb.Relic<Millsage>(), ModelDb.Relic<HotaruIridescence>() })
            AddEntry(entries, relics, "relics", relic.Id.Entry, "HBR 机制", relic.PackedIconPath);
        string manualIcon = ModelDb.Relic<HotaruElectronicSoldierManual>().PackedIconPath;
        foreach (string prefix in new[]
                 { "HOTARU_MANUAL_DP_RULES", "HOTARU_MANUAL_SIX_SENSE_RULES", "HOTARU_MANUAL_GARDEN_RULES",
                     "HOTARU_MANUAL_CRITICAL_RULES", "HOTARU_MANUAL_ENDGAME_RULES" })
            AddEntry(entries, relics, "relics", prefix, "小萤手册", manualIcon,
                hbrNumericsEnabled: hbrNumericsEnabled);
        AddEntry(entries, LocManager.Instance.GetTable("static_hover_tips"), "static_hover_tips", "BLOCK", "通用词条");
        return Order(entries.Values);
    }

    internal static IReadOnlyList<HbrManualGlossaryEntry> Search(
        IEnumerable<HbrManualGlossaryEntry> entries, string query)
    {
        ArgumentNullException.ThrowIfNull(entries);
        string[] terms = (query ?? string.Empty).Split((char[])null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Order(entries.Where(entry => terms.All(term =>
            Contains(entry.Title, term) || Contains(entry.Id, term) ||
            Contains(entry.Category, term) || Contains(PlainText(entry.Description), term))));
    }

    private static IEnumerable<string> GetPrefixes(LocTable table) =>
        // Keys alone omits fallback translations. This public API merges local and fallback keys.
        table.GetLocStringsWithPrefix(string.Empty)
            .Select(text => text.LocEntryKey)
            .Where(key => key.EndsWith(TitleSuffix, StringComparison.Ordinal))
            .Select(key => key[..^TitleSuffix.Length])
            .Distinct(StringComparer.Ordinal);

    private static void AddEntry(Dictionary<string, HbrManualGlossaryEntry> entries,
        LocTable table, string tableName, string prefix, string category, string iconPath = "",
        string descriptionFallback = "", bool hbrNumericsEnabled = false)
    {
        if (!table.HasEntry(prefix + TitleSuffix)) return;
        string title = PlainText(table.GetRawText(prefix + TitleSuffix));
        if (string.IsNullOrWhiteSpace(title)) return;
        bool sixSense = tableName == "card_keywords" &&
            (prefix == SixSensePrefix || prefix.StartsWith(SixSensePrefix + ".", StringComparison.Ordinal));
        string descriptionKey = sixSense && table.HasEntry(prefix + ".menuDescription")
            ? prefix + ".menuDescription" : prefix + ".description";
        if (tableName == "powers" && hbrNumericsEnabled && table.HasEntry(prefix + ".hbrReferenceDescription"))
            descriptionKey = prefix + ".hbrReferenceDescription";
        else if (tableName == "powers" && table.HasEntry(prefix + ".referenceDescription"))
            descriptionKey = prefix + ".referenceDescription";
        else if (hbrNumericsEnabled && sixSense && table.HasEntry(prefix + ".hbrMenuDescription"))
            descriptionKey = prefix + ".hbrMenuDescription";
        else if (hbrNumericsEnabled && table.HasEntry(prefix + ".hbrDescription"))
            descriptionKey = prefix + ".hbrDescription";
        string raw = table.HasEntry(descriptionKey) ? table.GetRawText(descriptionKey) : descriptionFallback;
        if (tableName == "card_keywords" && !sixSense)
        {
            foreach ((string suffix, string heading) in DescriptionVariants)
                if (table.HasEntry(prefix + suffix))
                    raw += $"\n\n[b]{heading}[/b]\n" + table.GetRawText(prefix + suffix);
        }
        // Some power templates read one live instance; the manual writes the rule instead.
        var notes = new List<string>();
        raw = HbrManualGlossaryStaticText.Rewrite(tableName, prefix, raw, hbrNumericsEnabled, notes);
        string id = tableName + ":" + prefix;
        Rect2? region = null;
        if (sixSense && prefix.StartsWith(SixSensePrefix + ".", StringComparison.Ordinal) &&
            Enum.TryParse(prefix[(SixSensePrefix.Length + 1)..], out SixSenseAttribute attribute) &&
            Enum.IsDefined(attribute))
        {
            iconPath = SixSenseBadgeHelper.AtlasPath;
            region = SixSenseBadgeHelper.AtlasRegion(attribute);
            if (hbrNumericsEnabled)
                raw = $"每级使战斗属性{HbrBattleStatsHelper.GetBattleStatTitle(attribute)} +{HbrBattleStatsHelper.SenseStatGainPerLevel}，按已确认有效等级结算。\n" + raw;
        }
        entries[id] = CreateEntry(id, title, raw, sixSense ? "六感" : category, iconPath, region, notes);
    }

    private static readonly (string suffix, string heading)[] DescriptionVariants =
    {
        (".attackSkillDescription", "攻击牌与技能牌"),
        (".powerDescription", "能力牌"),
        (".sakiDescription", "祥子规则"),
        (".sakiAttackSkillDescription", "祥子：攻击牌与技能牌"),
        (".sakiPowerDescription", "祥子：能力牌"),
        (".hotaruDescription", "小萤规则")
    };

    internal static string GetPowerIconPath(PowerModel power)
    {
        // These path getters resolve the registered icon; no tooltip, DynamicVars or Owner is read.
        try { return power?.IconPath ?? string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
        catch (NullReferenceException) { return string.Empty; }
    }

    private static string GetInheritedPowerDescription(LocTable table, Type powerType)
    {
        // Some temporary-stat powers only override their title; their rule belongs to the base power.
        // Read that base localization directly instead of invoking any virtual Description getter.
        for (Type type = powerType.BaseType; type != null && type != typeof(PowerModel) &&
             typeof(PowerModel).IsAssignableFrom(type); type = type.BaseType)
        {
            string key = ModelDb.GetId(type).Entry + ".description";
            if (table.HasEntry(key)) return table.GetRawText(key);
        }
        return string.Empty;
    }

    private static HbrManualGlossaryEntry CreateEntry(
        string id, string title, string raw, string category, string iconPath, Rect2? region = null,
        IEnumerable<string> notes = null) =>
        new(id, title, HbrManualGlossaryPresentation.FormatEntry(raw, id, notes), category, iconPath, region);

    private static IReadOnlyList<HbrManualGlossaryEntry> Order(IEnumerable<HbrManualGlossaryEntry> entries) =>
        Array.AsReadOnly(entries.OrderBy(entry => entry, EntryOrder).ToArray());

    private static bool Contains(string text, string term) =>
        TitleComparison.IndexOf(text ?? string.Empty, term, TextComparisonOptions) >= 0;

    private static string PlainText(string text) =>
        Markup.Replace(text ?? string.Empty, string.Empty).Trim();
}
