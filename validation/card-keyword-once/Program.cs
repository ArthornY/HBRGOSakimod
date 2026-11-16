using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

static class KeywordOnceStandalone { public static void Run(string path) => EffectChecks.RunKeywordOnce(path); }
static partial class EffectChecks
{
    static readonly List<object> KeywordFaces = new();
    static readonly List<object> RenamedTitles = new();
    static string KeywordPlain(string text) => Regex.Replace(Regex.Replace(text, @"\[[^\]]*\]", ""), @"<[^>]*>", "");
    static void KeywordCase(CardModel card, string keyword, string? choice = null)
    {
        string text = FaceRender(card), plain = KeywordPlain(text);
        int count = Regex.Matches(plain, Regex.Escape(keyword) + "[。.]").Count;
        bool before = Environment.GetEnvironmentVariable("HBR_KEYWORD_BASELINE") == "1";
        KeywordFaces.Add(new { model = card.GetType().Name, keyword, text, plain, count, choice });
        bool additional = Environment.GetEnvironmentVariable("HBR_KEYWORD_ADDITIONAL") == "1";
        Check(count == (before && !additional ? 2 : 1), (before && !additional ? "99 witnessed duplicate: " : "99 native keyword appears once: ") + card.GetType().Name + "/" + keyword + "/" + count);
        if (choice != null) Check(KeywordPlain(choice).Contains(keyword), "99 upgrade choice still names its granted keyword: " + card.GetType().Name);
    }
    static void KeywordCases()
    {
        foreach (Type type in Mod.GetTypes().Concat(typeof(CardModel).Assembly.GetTypes())
            .Where(t => !t.IsAbstract && !t.ContainsGenericParameters &&
                (typeof(CardModel).IsAssignableFrom(t) || typeof(PowerModel).IsAssignableFrom(t) ||
                 typeof(RelicModel).IsAssignableFrom(t) || typeof(CardPoolModel).IsAssignableFrom(t))))
        {
            if (!ModelDb.Contains(type)) ModelDb.Inject(type);
            Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
        var h = new Harmony(ProductionId + ".keyword99");
        void Patch(MethodBase method, string prefix) => h.Patch(method, prefix: new HarmonyMethod(typeof(EffectChecks).GetMethod(prefix, F)!));
        try
        {
            Patch(AccessTools.PropertyGetter(typeof(CombatManager), "IsOverOrEnding"), nameof(CostLive));
            Patch(Helper("CombatPreviewRefreshHelper").GetMethod("RefreshCombatState", F)!, nameof(Skip));
            Patch(AccessTools.PropertyGetter(typeof(PowerModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(AccessTools.PropertyGetter(typeof(RelicModel), "Icon"), nameof(FormPowerIconLeaf));
            Patch(AccessTools.Method(typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Assets.AssetCache", true)!, "GetTexture2D"), nameof(FormPowerIconLeaf));
            foreach (MethodInfo energy in typeof(EnergyIconHelper).GetMethods(F).Where(m => m.Name == "GetPath" && m.ReturnType == typeof(string))) Patch(energy, nameof(Energy));
            foreach (MethodInfo wait in typeof(Cmd).GetMethods(F).Where(m => (m.Name is "Wait" or "CustomScaledWait") && m.ReturnType == typeof(Task))) Patch(wait, nameof(VisualTask));
            foreach (string name in new[] { "CardDescriptionLocPatch", "ThirtyOneBCardDescriptionPatch", "HbrCardFaceRulesHoverPatch" })
                h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + name, true)!).Patch();
            bool additional = Environment.GetEnvironmentVariable("HBR_KEYWORD_ADDITIONAL") == "1";
            if (!additional) foreach (Type type in new[] { typeof(NoblesseObligeHbrgoSaki), typeof(KuraRiceBallHbrgoSaki), typeof(CuteIsInvincibleHbrgoSaki), typeof(AsTheYoungLadyCommandsHbrgoSaki), typeof(LetMeShowYouClassHbrgoSaki), typeof(WitnessMySupremacyHbrgoSaki) })
            {
                GardenReset(); var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone(); Set(card, "_owner", Player);
                KeywordCase(card, "消耗");
            }
            if (!additional) foreach (var row in new[] { (typeof(ItsNinjaShowtimeHbrgoSaki), 6, "保留"), (typeof(AbsolutelyNoEffortHbrgoSaki), 1, "固有"), (typeof(ExorcisingStringResoundHbrgoSaki), 6, "固有"), (typeof(WindForestFireMountainHbrgoSaki), 5, "保留") })
            {
                GardenReset(); var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(row.Item1)).MutableClone(); Set(card, "_owner", Player);
                GardenUpgrade(card, row.Item2);
                string choice = (string)Call("HbrMemoryGardenUpgradeCatalog", "GetEffectDescription", card, row.Item2.ToString())!;
                KeywordCase(card, row.Item3, choice);
            }
            if (additional) foreach (var row in new[] { (typeof(SoyoAidHbrgoSaki), 3, "固有"), (typeof(SoyoAidHbrgoSaki), 4, "保留"), (typeof(AnonAidHbrgoSaki), 1, "固有") })
            {
                GardenReset(); var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(row.Item1)).MutableClone(); Set(card, "_owner", Player);
                GardenUpgrade(card, row.Item2);
                string choice = (string)Call("HbrMemoryGardenUpgradeCatalog", "GetEffectDescription", card, row.Item2.ToString())!;
                KeywordCase(card, row.Item3, choice);
            }
        }
        finally { h.UnpatchAll(h.Id); CombatInProgress(false); As31O(false); Reset(); }
    }
    static void RenameCases()
    {
        string directory = Path.Combine(RepositoryRoot, "outputs/class-relics-95-99-20261116");
        using var map = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "rename-map.json")));
        var manager = LocManager.Instance;
        var tables = (Dictionary<string, LocTable>)AccessTools.Field(typeof(LocManager), "_tables").GetValue(manager)!;
        LocTable previous = tables["cards"];
        try
        {
            foreach (string language in new[] { "zhs", "eng" })
            {
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization", language, "cards.json")))!;
                tables["cards"] = new LocTable("cards", entries, null!);
                foreach (var row in map.RootElement.EnumerateArray())
                {
                    string model = row.GetProperty("model").GetString()!;
                    Type type = Mod.GetType("HBRGOSakiMod.Core.Models.Cards." + model, true)!;
                    var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).MutableClone();
                    string title = card.Title, expected = row.GetProperty(language).GetString()!;
                    RenamedTitles.Add(new { language, model, title, expected });
                    Check(title == expected, "95 actual native title: " + language + "/" + model + "/" + title);
                }
            }
        }
        finally { tables["cards"] = previous; }
    }
    public static void RunKeywordOnce(string path)
    {
        bool ok = false; string? error = null; ReportDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        try { Install(); Localization(); KeywordCases(); if (Environment.GetEnvironmentVariable("HBR_KEYWORD_BASELINE") != "1") RenameCases(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, error, faces = KeywordFaces, renamedTitles = RenamedTitles,
            baseline = Environment.GetEnvironmentVariable("HBR_KEYWORD_BASELINE") == "1",
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            boundary = "Actual GetDescriptionForPile with native keyword insertion, real selected garden upgrades and formatting; texture/wait/preview scene leaves isolated. No Godot renderer." }, new JsonSerializerOptions { WriteIndented = true }));
        if (!ok) throw new Exception("Inspect " + path);
        Console.WriteLine("PASS " + Passed.Count + " keyword face checks");
    }
}
