using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using SmartFormat;
using SmartFormat.Core.Extensions;
using SmartFormat.Extensions;

static class OverchargeChecks { public static void Run(string reportPath) => CompactHoverChecks.Run(reportPath); }

static partial class CompactHoverChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const string Owner = "hbr.compact-hover-tips.checks";
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly Assembly Native = typeof(ModelDb).Assembly;
    static readonly List<string> Passed = new();
    static readonly Dictionary<string, object> Lengths = new();
    static readonly Dictionary<string, object> Layouts = new();
    static readonly Regex Placeholder = new(@"\{([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);
    static readonly Regex Markup = new(@"\[/?[a-z_]+(=[^\]]*)?\]", RegexOptions.CultureInvariant);
    static readonly string[] Branches = { HbrCompactTextKeys.Count, HbrCompactTextKeys.Layers, "loss", "resistanceCount" };

    static readonly Dictionary<string, object> Sample = new(StringComparer.Ordinal)
    {
        ["criticalThreshold"] = 96m, ["absoluteThreshold"] = 777m, ["threshold"] = 777m,
        ["resistances"] = "疯狂 3 · 单纯 3", ["conversion"] = 121m, ["IgnorePerLayer"] = 8m,
        ["Duration"] = "1回合", ["EffectPercent"] = "18.8", ["ModifierCount"] = 1m, ["Turns"] = 2m, ["FragilePercent"] = "37.5",
        ["CopyLevel"] = 1m, ["Current"] = 105m, ["Maximum"] = 110m, ["Level"] = 1m, ["Amount"] = 3m,
        ["CannonBonuses"] = "幸福 +3%", ["BaseValueGain"] = 2m, ["AttackBonus"] = 4m, ["SkillBonus"] = 2m,
        ["Layers"] = 2m, ["RollBonus"] = 90m, ["MaxRolls"] = 3m, ["ChainUpgradeDescription"] = "",
        ["AdvantageDescription"] = "", ["SwordheartDescription"] = "", ["RedCrimsonLevel"] = 3m,
        ["EnergyIcons"] = "[img]e[/img][img]e[/img]", ["StartingStats"] = 25m,
        ["energyPrefix"] = "hbrgosaki", ["hbrgoSakiEnergyPrefix"] = "hbrgosaki",
        ["Value"] = 20m, ["RemainingUses"] = 3m, ["DualSwordsCount"] = 2m, ["BowCount"] = 1m,
        ["TemporaryMaxHp"] = 1200m, ["DpToReturn"] = 2400m,
        [HbrCompactTextKeys.GardenPoints] = 2m, [HbrCompactTextKeys.RemainingLevels] = 10m,
        [HbrCompactTextKeys.Layers] = 3m, [HbrCompactTextKeys.Count] = 3m,
        ["Count"] = 4m, ["CutinCount"] = 3m, ["NextTier"] = "6种：6人和Live", ["LivePending"] = "",
        ["Kinds"] = 4m, ["ActiveTiers"] = "护身符、互相守护", ["ActiveText"] = "已生效",
        ["RanaBonus"] = "20", ["ByakkoBonus"] = "10", ["UsedText"] = "本回合未使用", ["AoiChecks"] = 2,
        ["Servants"] = 6, ["MaximumUses"] = 2, ["TotalPercent"] = 600, ["Reduction"] = 60,
        ["state"] = "普通", ["value"] = 136m, ["skill"] = "69.6", ["loss"] = 1m, ["attributes"] = "疯狂",
        ["resistanceCount"] = 1m, ["primary"] = "疯狂", ["statName"] = "力量", ["statBonus"] = 32m, ["bonus"] = 16m,
        ["attribute"] = "疯狂", ["title"] = "大成功", ["text"] = "原始出目96～100。"
    };

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAILED: " + message);
        Passed.Add(message);
    }

    public static void Run(string reportPath)
    {
        string root = FindRoot();
        var zhs = Load(root, "zhs");
        var eng = Load(root, "eng");
        LocalizationChecks(zhs, eng);
        SmartFormatter formatter = CreateFormatter();
        FormattingChecks(zhs, formatter);
        LayoutChecks(zhs, formatter);
        PatchChecks();
        HoverTipCompatibilityChecks();
        var report = new
        {
            candidate = Mod.Location,
            nativeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE") ?? "libs",
            nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            passed = Passed.Count,
            lengths = Lengths,
            layouts = Layouts,
            checks = Passed
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
        Console.WriteLine($"{Passed.Count} checks passed");
    }

    static string FindRoot()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "HBRGOSakiMod.csproj"))) return dir.FullName;
        throw new Exception("Repository root not found");
    }

    static Dictionary<string, Dictionary<string, string>> Load(string root, string language)
    {
        var tables = new Dictionary<string, Dictionary<string, string>>();
        foreach (string table in new[] { "card_keywords", "powers", "relics" })
        {
            string path = Path.Combine(root, "HBRGOSakiMod", "localization", language, table + ".json");
            tables[table] = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                : new Dictionary<string, string>();
        }
        return tables;
    }

    static IEnumerable<string> Variables(string text) =>
        Placeholder.Matches(text).Select(match => match.Groups[1].Value);

    static int PlainLength(string text) => Markup.Replace(text, "").Count(c => !char.IsWhiteSpace(c));

    static string FullText(Dictionary<string, string> table, string key) =>
        table.TryGetValue(key, out string text) ? text : table[HbrCompactTextKeys.ModPrefix + key];

    static void LocalizationChecks(Dictionary<string, Dictionary<string, string>> zhs,
        Dictionary<string, Dictionary<string, string>> eng)
    {
        Check(HbrCompactTextKeys.CompactKey("A.description") == "A.compactDescription" &&
              HbrCompactTextKeys.CompactKey("A.hbrReferenceDescription") == "A.hbrReferenceCompactDescription" &&
              HbrCompactTextKeys.CompactKey("A.sakiAttackSkillDescription") == "A.sakiAttackSkillCompactDescription" &&
              HbrCompactTextKeys.Normalize("HBRGOSAKIMOD-X.description") == "X.description" &&
              HbrCompactTextKeys.Normalize("X.description") == "X.description",
            "short keys sit next to the full key and ignore the mod prefix");

        int count = 0;
        foreach ((string table, string[] keys) in HbrCompactTextKeys.Entries)
        {
            Dictionary<string, string> zh = zhs[table];
            Check(keys.Distinct().Count() == keys.Length, $"{table}: every listed entry appears once");
            foreach (string key in keys)
            {
                string[] fullKeys = new[] { key, HbrCompactTextKeys.ModPrefix + key }.Where(zh.ContainsKey).ToArray();
                Check(fullKeys.Length > 0, $"{table}:{key} exists in Chinese");
                string compactKey = HbrCompactTextKeys.CompactKey(key);
                Check(zh.TryGetValue(compactKey, out string compact) && compact.Trim().Length > 0,
                    $"{table}:{compactKey} short text exists");
                Check(!eng[table].ContainsKey(compactKey) && !eng[table].ContainsKey(HbrCompactTextKeys.ModPrefix + compactKey),
                    $"{table}:{compactKey} has no English copy, so English keeps the full text");
                HashSet<string> allowed = fullKeys.SelectMany(full => Variables(zh[full])).ToHashSet();
                if (HbrCompactTextKeys.ExtraVariables.TryGetValue(table + ":" + key, out string[] extra)) allowed.UnionWith(extra);
                string[] unknown = Variables(compact).Where(name => !allowed.Contains(name)).Distinct().ToArray();
                Check(unknown.Length == 0, $"{table}:{compactKey} uses only variables its full text receives" +
                    (unknown.Length > 0 ? ": " + string.Join(", ", unknown) : ""));
                count++;
            }
        }
        foreach (string extra in HbrCompactTextKeys.ExtraVariables.Keys)
        {
            string[] parts = extra.Split(':');
            Check(HbrCompactTextKeys.Entries[parts[0]].Contains(parts[1]), $"{extra}: extra variables belong to a listed entry");
        }

        foreach ((string key, string[] provided) in HbrCompactTextKeys.UiEntries)
        {
            Check(zhs["card_keywords"].TryGetValue(key, out string text), $"card_keywords:{key} exists");
            string[] unknown = Variables(text).Where(name => !provided.Contains(name)).Distinct().ToArray();
            Check(unknown.Length == 0, $"card_keywords:{key} uses only the variables its code passes" +
                (unknown.Length > 0 ? ": " + string.Join(", ", unknown) : ""));
            Check(!eng["card_keywords"].ContainsKey(key), $"card_keywords:{key} has no English copy");
        }
        // Six-sense card attributes were removed (2026-11-09): no card carries a six-sense
        // hover, so no short six-sense line is built or kept.
        Check(typeof(HbrCompactTextKeys).GetField("SixSenseEntries", BindingFlags.NonPublic | BindingFlags.Static) == null,
            "the compact hover keeps no six-sense attribute entries");
        string[] staleSixSense = zhs["card_keywords"].Keys.Where(key => key.StartsWith("HBRGO_SIX_SENSE.", StringComparison.Ordinal) &&
            (key.Contains("ompact", StringComparison.Ordinal) || key.EndsWith(".cardText", StringComparison.Ordinal))).ToArray();
        Check(staleSixSense.Length == 0, "no short six-sense card text is left in card_keywords: " + string.Join(", ", staleSixSense));
        Check(!HbrCompactTextKeys.Entries["powers"].Contains("SIX_SENSE_RESISTANCE_POWER.description"),
            "the removed six-sense resistance has no short hover entry");

        // Short texts the explicit hover code already used keep the variables that code passes.
        Dictionary<string, string> powers = zhs["powers"];
        string[] chain = { "RollBonus", "MaxRolls", "ChainUpgradeDescription", "AdvantageDescription", "SwordheartDescription" };
        Check(Variables(powers["NOT_OVER_YET_POWER.activeCompactDescription"]).All(chain.Contains),
            "not-over-yet short text uses only the variables NotOverYetPower passes");
        Check(!Variables(powers["PHANTOM_POWER.compactDescription"]).Any() &&
              !Variables(powers["PHANTOM_POWER.thunderCompactDescription"]).Any(),
            "phantom short texts need no variables");
        Lengths["listedEntries"] = count;
    }

    sealed class EnergyIconsStub : IFormatter
    {
        public string Name { get; set; } = "energyIcons";
        public bool CanAutoDetect { get; set; }

        public bool TryEvaluateFormat(IFormattingInfo formattingInfo)
        {
            formattingInfo.Write($"[img]energy×{formattingInfo.FormatterOptions}[/img]");
            return true;
        }
    }

    // The game's formatter (LocManager) with its native icon formatter stubbed.
    static SmartFormatter CreateFormatter()
    {
        SmartFormatter formatter = new();
        ListFormatter list = new();
        formatter.AddExtensions(list, new DictionarySource(), new ValueTupleSource(), new ReflectionSource(), new DefaultSource());
        formatter.AddExtensions(list, new PluralLocalizationFormatter(), new ConditionalFormatter(), new ChooseFormatter(),
            new SubStringFormatter(), new IsMatchFormatter(), new DefaultFormatter(), new EnergyIconsStub());
        return formatter;
    }

    static IEnumerable<Dictionary<string, object>> Samples(string text)
    {
        string[] branches = Branches.Where(name => Variables(text).Contains(name)).ToArray();
        IEnumerable<Dictionary<string, object>> samples = new[] { new Dictionary<string, object>(Sample, StringComparer.Ordinal) };
        foreach (string branch in branches)
        {
            decimal[] values = branch == HbrCompactTextKeys.Count ? new[] { 0m, 1m, 2m, 3m, 4m, 5m, 6m }
                : branch == HbrCompactTextKeys.Layers ? new[] { 1m, 2m, 3m, 4m, 5m, 6m }
                : branch == "loss" ? new[] { 0m, 1m, 2m } : new[] { 1m, 2m };
            samples = samples.SelectMany(sample => values.Select(value =>
                new Dictionary<string, object>(sample, StringComparer.Ordinal) { [branch] = value })).ToArray();
        }
        return samples;
    }

    static string Format(SmartFormatter formatter, string text, Dictionary<string, object> sample, string label)
    {
        try
        {
            return formatter.Format(CultureInfo.InvariantCulture, text, sample);
        }
        catch (Exception error)
        {
            throw new Exception($"FAILED: {label} formats ({error.GetType().Name}: {error.Message})");
        }
    }

    static int FormattedLength(SmartFormatter formatter, string text)
    {
        try { return PlainLength(formatter.Format(CultureInfo.InvariantCulture, text, Sample)); }
        catch (Exception) { return PlainLength(Placeholder.Replace(text, "")); }
    }

    static void FormattingChecks(Dictionary<string, Dictionary<string, string>> zhs, SmartFormatter formatter)
    {
        foreach ((string table, string[] keys) in HbrCompactTextKeys.Entries)
            foreach (string key in keys)
            {
                string label = $"{table}:{HbrCompactTextKeys.CompactKey(key)}";
                string compact = zhs[table][HbrCompactTextKeys.CompactKey(key)];
                int longest = 0;
                int count = 0;
                foreach (Dictionary<string, object> sample in Samples(compact))
                {
                    string text = Format(formatter, compact, sample, label);
                    if (text.Contains('{') || text.Contains('}') || text.Trim().Length == 0)
                        throw new Exception($"FAILED: {label} leaves a placeholder or nothing: {text}");
                    longest = Math.Max(longest, PlainLength(text));
                    count++;
                }
                int full = FormattedLength(formatter, FullText(zhs[table], key));
                Check(longest < full, $"{label} formats in {count} case(s); the longest ({longest}) is shorter than the full text ({full})");
                Lengths[$"{table}:{key}"] = new { full, compact = longest };
            }

        foreach (string key in HbrCompactTextKeys.UiEntries.Keys)
        {
            string text = zhs["card_keywords"][key];
            int count = 0;
            foreach (Dictionary<string, object> sample in Samples(text))
            {
                string result = Format(formatter, text, sample, key);
                if (result.Contains('{') || result.Contains('}')) throw new Exception($"FAILED: {key} leaves a placeholder: {result}");
                count++;
            }
            Check(count > 0, $"card_keywords:{key} formats in {count} case(s)");
        }

        string Fmt(string table, string key, params (string Name, object Value)[] values)
        {
            Dictionary<string, object> sample = new(Sample, StringComparer.Ordinal);
            foreach ((string name, object value) in values) sample[name] = value;
            return Markup.Replace(Format(formatter, zhs[table][key], sample, key), "");
        }

        string thirtyOne = "THIRTY_ONE_A_COLLECTION.compactDescription";
        string[] at0 = Fmt("relics", thirtyOne, (HbrCompactTextKeys.Count, 0m)).Split('\n');
        string[] at3 = Fmt("relics", thirtyOne, (HbrCompactTextKeys.Count, 3m)).Split('\n');
        string[] at6 = Fmt("relics", thirtyOne, (HbrCompactTextKeys.Count, 6m)).Split('\n');
        Check(at0.Length == 2 && at0[0] == "现在0种：每5层闪避+1力量、+1敏捷。" && at0[1].StartsWith("下一档（2种）"),
            "31A collection at 0 kinds shows only the base effect and the 2-kind tier next");
        Check(at3[0] == "现在3种：每5层闪避+1力量、+1敏捷；获得的格挡转为等量闪避。" && at3[1].StartsWith("下一档（4种）"),
            "31A collection at 3 kinds shows the 2-kind tier and the 4-kind tier next");
        Check(at6[0].Contains("闪避同时视为格挡") && at6[0].Contains("回合开始时不再失去") && at6[1] == "已全部生效。",
            "31A collection at 6 kinds shows every tier and no next tier");

        string iridescence = "HOTARU_IRIDESCENCE.compactDescription";
        string layer1 = Fmt("relics", iridescence, (HbrCompactTextKeys.Layers, 1m), ("RollBonus", 30m));
        string layer4 = Fmt("relics", iridescence, (HbrCompactTextKeys.Layers, 4m), ("RollBonus", 120m));
        string layer6 = Fmt("relics", iridescence, (HbrCompactTextKeys.Layers, 6m), ("RollBonus", 180m), ("StartingStats", 36m));
        Check(layer1.Contains("现在1层：出目+30%。") && layer1.Contains("下一层：每回合多抽2张牌"), "iridescence at 1 charge shows the roll bonus and the 2-charge effect next");
        Check(layer4.Contains("每回合额外获得") && layer4.Contains("下一层：战斗开始时获得力量和敏捷") && !layer4.Contains("层力量和敏捷"),
            "iridescence at 4 charges lists effects up to 4 and the 5-charge effect next");
        Check(layer6.Contains("36层力量和敏捷") && layer6.EndsWith("你的欲望，使你迎来终局。") && !layer6.Contains("下一层"),
            "iridescence at 6 charges lists every effect and the final line");

        foreach (string manualKey in new[] { "ELECTRONIC_SOLDIER_MANUAL", "ELECTRONIC_SOLDIER_MANUAL_31O", "ELECTRONIC_SOLDIER_MANUAL_31_O", "KETSU" })
            foreach (string field in new[] { "compactDescription", "hbrCompactDescription" })
            {
                string shortText = zhs["relics"][$"{manualKey}.{field}"];
                Check(!shortText.Contains("属性牌") && !shortText.Contains("抗性") && shortText.Contains("点数在手册中分配"),
                    $"{manualKey}.{field} no longer ties six-sense levels to card rolls or resistances");
            }
    }

    // Approximates the native tooltip: 360px boxes, 22px text in 293px, 30px
    // lines, CJK one unit, Latin half a unit, and a new column past 1030px.
    static int Units(char c) => c < 0x100 ? 1 : 2;

    static int Lines(string text)
    {
        int lines = 0;
        foreach (string paragraph in Markup.Replace(text, "").Split('\n'))
        {
            int width = 0, count = 1;
            const string noStart = "，。；：、）」』》！？…";
            foreach (char c in paragraph)
            {
                int w = Units(c);
                if (width + w > 26 && !noStart.Contains(c)) { count++; width = 0; }
                width += w;
            }
            lines += count;
        }
        return lines;
    }

    static int BoxHeight(string body, bool hint) => 80 + Lines(body) * 30 + (hint ? 26 : 0);

    static void LayoutChecks(Dictionary<string, Dictionary<string, string>> zhs, SmartFormatter formatter)
    {
        string Text(string table, string key) => Format(formatter, zhs[table][key], Sample, key);
        string Line(string title, string text) => $"{title}：{text}";
        string[] killKiss =
        {
            Text("card_keywords", "HBRGO_SAKI_MOD_KEYWORD_ROLL.sakiAttackSkillCompactDescription") + "\n" +
                Line("大成功", Text("card_keywords", "HBRGO_SAKI_MOD_KEYWORD_CRITICAL_SUCCESS.sakiCompactDescription")) + "\n" +
                Line("绝对大成功", Text("card_keywords", "HBRGO_SAKI_MOD_KEYWORD_ABSOLUTE_CRITICAL_SUCCESS.compactDescription")),
            Line("防御力下降", Text("powers", "HBR_DEFENSE_DOWN_POWER.hbrReferenceCompactDescription")) + "\n" +
                Line("脆弱", Text("powers", "FRAGILE_POWER.hbrReferenceCompactDescription")),
            Text("powers", "KILL_KISS_FUSION_POWER.compactDescription"),
            zhs["powers"]["PHANTOM_POWER.compactDescription"]
        };
        int height = killKiss.Select((body, i) => BoxHeight(body, i == killKiss.Length - 1)).Sum() + 5 * (killKiss.Length - 1);
        Layouts["killKiss"] = new { boxes = killKiss.Length, height, lines = killKiss.Select(Lines).ToArray() };
        Check(height <= 1030, $"KillKiss (roll, two debuffs, its power, phantom; no six-sense box since 2026-11-09) fits one 1080p column: about {height}px");

        string manual = Format(formatter, zhs["relics"]["ELECTRONIC_SOLDIER_MANUAL_31O.hbrCompactDescription"], Sample, "manual") + "\n" +
            "按住Shift查看完整说明";
        int manualHeight = BoxHeight(manual, false) + 5 + BoxHeight(zhs["powers"]["NORMAL_STATE_POWER.compactDescription"], false);
        Layouts["electronicSoldierManual"] = new { height = manualHeight };
        Check(manualHeight <= 600, $"the 31O manual and normal state fit in about {manualHeight}px");
    }

    static bool StringNameLeaf(Godot.StringName __instance) { GC.SuppressFinalize(__instance); return false; }
    static bool Skip() => false;

    static void PatchChecks()
    {
        Harmony harmony = new(Owner);
        harmony.Patch(typeof(Godot.StringName).GetConstructor(new[] { typeof(string) }),
            prefix: new HarmonyMethod(typeof(CompactHoverChecks).GetMethod(nameof(StringNameLeaf), F)));
        Type logger = Native.GetType("MegaCrit.Sts2.Core.Logging.Logger", true);
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor)
            harmony.Patch(editor, prefix: new HarmonyMethod(typeof(CompactHoverChecks).GetMethod(nameof(Skip), F)));

        foreach ((string name, int count) in new[]
                 {
                     ("HbrCompactHoverTipPatch", 9), ("HbrCompactTextFormatPatch", 1),
                     ("HbrCompactRelicDescriptionPatch", 1), ("HbrHoverDetailPollPatch", 1),
                     ("HbrCompactKeywordCachePatch", 1)
                 })
        {
            Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches." + name, true);
            List<MethodInfo> patched = harmony.CreateClassProcessor(patch).Patch();
            Check(patched.Count == count, $"{name} installs on {count} native method(s)");
        }
        Type keywordCache = Mod.GetType("HBRGOSakiMod.Core.Patches.HbrCompactKeywordCachePatch", true);
        Check(keywordCache.GetCustomAttribute<HarmonyPriority>()?.info.priority > 800,
            "keyword tooltips are built with the full text before RitsuLib's priority-800 prefix builds and caches them");
        MethodInfo fromKeyword = Native.GetType("MegaCrit.Sts2.Core.HoverTips.HoverTipFactory", true).GetMethod("FromKeyword", F);
        Patches keywordPatches = Harmony.GetPatchInfo(fromKeyword);
        Check(keywordPatches.Prefixes.Any(p => p.owner == Owner) && keywordPatches.Finalizers.Any(p => p.owner == Owner),
            "the keyword cache guard restores compact text in a finalizer, even if building the tooltip throws");

        Type helper = Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrCompactHoverTipHelper", true);
        foreach ((string type, string method, Type[] parameters, string model) in new[]
                 {
                     ("MegaCrit.Sts2.Core.Nodes.Combat.NPower", "OnHovered", Type.EmptyTypes, "PowerModel"),
                     ("MegaCrit.Sts2.Core.Nodes.Combat.NPower", "ShowPowerHoverTips", new[] { Native.GetType("MegaCrit.Sts2.Core.Combat.CombatState", true) }, "PowerModel"),
                     ("MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder", "CreateHoverTips", Type.EmptyTypes, "CardModel"),
                     ("MegaCrit.Sts2.Core.Nodes.Cards.Holders.NPreviewCardHolder", "CreateHoverTips", Type.EmptyTypes, "CardModel"),
                     ("MegaCrit.Sts2.Core.Nodes.Screens.NInspectCardScreen", "UpdateCardDisplay", Type.EmptyTypes, "CardModel"),
                     ("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCard", "CreateHoverTip", Type.EmptyTypes, "CardModel"),
                     ("MegaCrit.Sts2.Core.Nodes.Combat.NCreature", "OnFocus", Type.EmptyTypes, "Creature"),
                     ("MegaCrit.Sts2.Core.Nodes.Combat.NCreature", "ShowCreatureHoverTips", new[] { Native.GetType("MegaCrit.Sts2.Core.Combat.CombatState", true) }, "Creature"),
                     ("MegaCrit.Sts2.Core.Nodes.Relics.NRelicInventoryHolder", "OnFocus", Type.EmptyTypes, "RelicModel")
                 })
        {
            MethodBase original = AccessTools.DeclaredMethod(Native.GetType(type, true), method, parameters);
            List<CodeInstruction> current = PatchProcessor.GetCurrentInstructions(original);
            bool replaced = current.Any(instruction => instruction.operand is MethodInfo call && call.DeclaringType == helper &&
                call.Name.StartsWith("For", StringComparison.Ordinal));
            bool native = current.Any(instruction => instruction.operand is MethodInfo call && call.Name == "get_HoverTips" &&
                call.DeclaringType?.Name == model);
            Check(replaced && !native, $"{type.Split('.').Last()}.{method} builds its tips through the compact helper");
        }

        // The real patch on a real table: listed entries read their short text
        // only inside a compact scope, prefixed copies included.
        Type tableType = Native.GetType("MegaCrit.Sts2.Core.Localization.LocTable", true);
        Check(tableType.GetField("_name", F)?.FieldType == typeof(string), "native tables keep their name in the field the swap reads");
        object english = Activator.CreateInstance(tableType, "powers", new Dictionary<string, string>
        {
            ["CHARGE_POWER.description"] = "english full"
        }, null);
        object chinese = Activator.CreateInstance(tableType, "powers", new Dictionary<string, string>
        {
            ["CHARGE_POWER.description"] = "full",
            ["CHARGE_POWER.compactDescription"] = "short",
            ["HBRGOSAKIMOD-ITS_MYGO_PROTECTION_POWER.description"] = "prefixed full",
            ["ITS_MYGO_PROTECTION_POWER.compactDescription"] = "prefixed short",
            ["PHANTOM_POWER.description"] = "unlisted full",
            ["PHANTOM_POWER.compactDescription"] = "unlisted short"
        }, english);
        MethodInfo raw = tableType.GetMethod("GetRawText", new[] { typeof(string) });
        string Raw(object table, string key) => (string)raw.Invoke(table, new object[] { key });
        Type compactText = Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrCompactText", true);
        Check(Raw(chinese, "CHARGE_POWER.description") == "full", "outside a compact hover the full text is read");
        using (IDisposable scope = (IDisposable)compactText.GetMethod("Enter", F).Invoke(null, null))
        {
            Check(Raw(chinese, "CHARGE_POWER.description") == "short", "inside a compact hover a listed entry reads its short text");
            Check(Raw(chinese, "HBRGOSAKIMOD-ITS_MYGO_PROTECTION_POWER.description") == "prefixed short",
                "a prefixed copy of a listed entry reads the same short text");
            Check(Raw(chinese, "PHANTOM_POWER.description") == "unlisted full", "entries off the list keep their full text");
            Check(Raw(english, "CHARGE_POWER.description") == "english full", "a language without short texts keeps the full text");
            Check((int)compactText.GetProperty("Swapped", F).GetValue(null) == 2, "the scope counts exactly the two swapped texts");
            Check((bool)compactText.GetMethod("Suspend", F).Invoke(null, null), "a cache-building call can suspend the swap inside a hover");
            Check(Raw(chinese, "CHARGE_POWER.description") == "full", "while suspended, cached tooltips are built with the full text");
            compactText.GetMethod("Resume", F).Invoke(null, null);
            Check(Raw(chinese, "CHARGE_POWER.description") == "short", "after the cached tooltip is built, the hover reads short text again");
        }
        Check(!(bool)compactText.GetMethod("Suspend", F).Invoke(null, null), "outside a hover there is nothing to suspend");
        Check(Raw(chinese, "CHARGE_POWER.description") == "full", "leaving the scope restores the full text");

        Type detail = Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrHoverDetail", true);
        foreach (string field in new[]
                 {
                     "ActiveSetsField", "CardHolderTips", "MerchantCardTip", "InspectCardDisplay", "PowerTips", "CreatureTips",
                     "RelicFocus", "ControllerManager", "UsingController"
                 })
            Check(detail.GetField(field, F)?.GetValue(null) != null, $"full-text key resolves the native member behind {field}");
        FieldInfo sets = (FieldInfo)detail.GetField("ActiveSetsField", F).GetValue(null);
        Type tipSet = Native.GetType("MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet", true);
        Check(sets.IsStatic && sets.FieldType == typeof(Dictionary<,>).MakeGenericType(typeof(Godot.Control), tipSet),
            "the native open-tooltip table is a static Dictionary<Control, NHoverTipSet>");
        Check(Harmony.GetPatchInfo(AccessTools.DeclaredMethod(tipSet, "Remove", new[] { typeof(Godot.Control) }))?.Owners.Contains(Owner) != true,
            "the native tooltip Remove stays unpatched");
    }
}
