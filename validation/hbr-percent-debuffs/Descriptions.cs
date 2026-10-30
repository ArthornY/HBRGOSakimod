using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

static partial class PercentDebuffChecks
{
    static string RepositoryRoot
    {
        get
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "HBRGOSakiMod.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Cannot locate fixture repository root");
        }
    }

    static void InitializeDescriptions()
    {
        var manager = Blank<LocManager>();
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        Set(manager, "<Language>k__BackingField", "zhs"); Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture);
        Set(manager, "<StringComparer>k__BackingField", StringComparer.Ordinal);
        var tables = new Dictionary<string, LocTable>();
        string native = Environment.GetEnvironmentVariable("HBR_NATIVE_LOCALIZATION_DIRECTORY") ?? "D:/stsmod/sts/localization/zhs";
        foreach (string file in new[] { "cards", "powers", "card_keywords" })
        {
            var text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(native, file + ".json")))!;
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs", file + ".json")))!) text[pair.Key] = pair.Value;
            tables[file] = new LocTable(file, text, null!);
        }
        Set(manager, "_tables", tables); typeof(LocManager).GetMethod("LoadLocFormatters", F)!.Invoke(manager, null);
        var harmony = new Harmony("hbr.percent-debuff.description.fixture");
        foreach (Type type in Mod.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false) &&
            (t.Name.StartsWith("HbrPercentDebuff", StringComparison.Ordinal) || t.Name == "CardDescriptionLocPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "installs production description " + type.Name);
    }

    static LocString Bind(CardModel card)
    {
        var text = card.Description; card.DynamicVars.AddTo(text);
        AccessTools.Method(card.GetType(), "AddExtraArgsToDescription").Invoke(card, [text]);
        text.Add(new IfUpgradedVar(card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));
        text.Add("OnTable", false); text.Add("InCombat", false); text.Add("IsTargeting", false);
        text.Add("TargetType", card.TargetType.ToString()); text.Add("GainsBlock", card.GainsBlock);
        text.Add("IsOstyAlive", false); text.Add("energyPrefix", "energy"); text.Add("singleStarIcon", "[img]star.png[/img]");
        return text;
    }
    static IDisposable DescriptionScope(CardModel card, Creature? target) =>
        (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F).Single(m => m.Name == "Begin" && m.GetParameters().Length == 4), null, card, false, false, target)!;
    static string Clean(string text) => Regex.Replace(text, "\\[[^\\]]+\\]", "");
    static string Render(CardModel card, Creature? target = null)
    {
        using var scope = DescriptionScope(card, target);
        var raw = card.DynamicVars.Values.ToDictionary(v => v.Name, v => v.BaseValue);
        string formatted = Bind(card).GetFormattedText();
        Check(!formatted.Contains('{') && !formatted.Contains('}'), card.GetType().Name + " actual native SmartFormat resolves every placeholder");
        Check(raw.All(pair => card.DynamicVars[pair.Key].BaseValue == pair.Value), card.GetType().Name + " description never changes actual raw application strength");
        return Clean(formatted);
    }
    static string PercentVariable(CardModel card, string name, Creature? target = null)
    {
        using var scope = DescriptionScope(card, target);
        return (string)Bind(card).Variables[name]!;
    }

    static void DescriptionCases()
    {
        InitializeDescriptions(); Reset();
        var bash = Card<Bash>(); var neutralize = Card<Neutralize>(); var soul = Card<SoulIntimidationHbrgoSaki>();
        Check(Render(bash).Contains("37.5%的易伤（2回合）"), "native Bash raw2 displays percentage and duration without changing native template");
        Check(Render(neutralize).Contains("30%的虚弱（2回合）"), "native Neutralize raw1 displays percentage and duration");
        Check(Render(soul).Contains("37.5%的虚弱（2回合）"), "mod SoulIntimidation raw2 uses the same conversion");
        var scythe = Card<MimicCursedScytheHbrgoSaki>(); string scytheText = Render(scythe);
        Check(scytheText.Contains("30%的虚弱（2回合）") && scytheText.Contains("30%的易伤（2回合）"), "one card independently formats both native debuff types");
        Sense("Talent", 100);
        Check(Render(bash).Contains("37.5%的易伤"), "no target shows coefficient base despite current INT300");
        Check(Render(bash, Enemy).Contains("40.6%的易伤"), "selected target resolves INT-weighted effect with one-decimal display");
        Check(PercentVariable(bash, "VulnerablePowerPercent", Enemy).Contains("[green]"), "increased target potency retains green comparison formatting");
        Scars(25);
        Check(Render(bash, Enemy).Contains("50.8%的易伤"), "selected target display includes Scars final enhancement exactly once");
        Reset();
        ModelDb.Inject(typeof(Bash));
        var upgraded = (Bash)bash.ClonePreservingMutability(); upgraded.UpgradeInternal();
        Check(PercentVariable(upgraded, "VulnerablePowerPercent").Contains("[green]"), "freshly upgraded native debuff coefficient keeps upgrade color");
        upgraded.FinalizeUpgradeInternal();
        Check(Render(upgraded).Contains("46.9%的易伤"), "native upgraded Bash preserves raw3 strength conversion");
        var untagged = new LocString("cards", "BASH.description");
        Check(untagged.GetRawText().Contains("层[gold]易伤[/gold]"), "untagged native LocString is never globally rewritten");
        Mode(false); Check(Render(bash).Contains("2层易伤") && !Render(bash).Contains("37.5%"), "legacy card descriptions retain original raw layers");
        Mode(true); Reset(); Sense("Talent", 75); Sense("Simplicity", 75);
        Power<HbrCalamityPower>(Enemy, 150);
        var lastStand = Card<LastStandHbrgoSaki>();
        Check(Render(lastStand, Enemy).Contains("获得41.3%的易伤"), "self-inflicted LastStand ignores selected zero-E enemy and evaluates its actual player recipient");
        Reset(); var weak = Apply<WeakPower>(2, Hero)!; var vulnerable = Apply<VulnerablePower>(3, Hero)!;
        Check(Clean(weak.Description.GetFormattedText()).Contains("37.5%") && Clean(vulnerable.Description.GetFormattedText()).Contains("46.9%"), "actual native state descriptions show stored snapshots rather than25/50 percent defaults");
        Tick(weak); Equal((decimal)weak.Description.Variables["Turns"], 1m, "native state description tracks each current remaining duration");
        Scars(25); Check(Clean(weak.Description.GetFormattedText()).Contains("46.9%"), "native state description applies live Scars once to stored snapshot");
        Equal(Percent(weak), 37.5m, "formatting and Scars never overwrite saved base effect");
        Reset();
    }
}
