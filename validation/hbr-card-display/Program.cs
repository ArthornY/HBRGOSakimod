using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

static partial class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> Rows = [];
    static readonly List<object> TemplateErrors = [];
    static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static Player Player = null!;
    static Creature Hero = null!;
    static Creature Enemy = null!;
    static ElectronicSoldierManual Manual = null!;
    static bool ObservePreviewGuard, ThrowPreviewListener;
    static int ObservedPreviewGuards;
    static Dictionary<string, string> CardText = null!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(Type type, string name, object? target, params object?[] args) =>
        type.GetMethod(name, F)!.Invoke(target, args);
    static FieldInfo Field(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
            if (current.GetField(name, F) is { } field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object target, string name, object? value) => Field(target.GetType(), name).SetValue(target, value);
    static T Blank<T>()
    {
        var result = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(result!); return result;
    }
    static void Check(bool result, string label)
    {
        if (!result) throw new Exception(label); Passed.Add(label);
    }
    static bool Skip() => false;
    static bool EmptyListeners(ref IEnumerable<AbstractModel> __result)
    {
        if (ObservePreviewGuard)
        {
            Type shield = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.StrongestShieldDamagePatch", true)!;
            Check((int)shield.GetField("_intentPreviewDepth", F)!.GetValue(null)! > 0,
                "live description calculation enters StrongestShield intent-preview guard before listener evaluation");
            ObservedPreviewGuards++;
        }
        if (ThrowPreviewListener) throw new InvalidOperationException("controlled-preview-listener-fault");
        __result = Array.Empty<AbstractModel>(); return false;
    }
    static bool EnergyPrefix(ref string __result) { __result = "energy"; return false; }
    static void Mode(bool enabled) => typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!
        .SetValue(Manual, JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, HealthScaleApplied = true, Level = 1 }));
    static void Install()
    {
        var h = new Harmony("hbr.card.numeric.display.checks");
        void Prefix(MethodBase method, string name = nameof(Skip)) => h.Patch(method,
            prefix: new HarmonyMethod(typeof(Checks).GetMethod(name, F)!));
        Prefix(typeof(StringName).GetConstructor([typeof(string)])!);
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor) Prefix(editor);
        foreach (MethodInfo method in logger.GetMethods(F).Where(m => m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Prefix(method);
        Prefix(AccessTools.Method(typeof(RunState), nameof(RunState.IterateHookListeners)), nameof(EmptyListeners));
        Prefix(AccessTools.PropertyGetter(typeof(CardModel), "Pile"));
        Type energy = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!;
        foreach (MethodInfo method in energy.GetMethods(F).Where(m => m.Name == "GetPrefix")) Prefix(method, nameof(EnergyPrefix));
        foreach (string name in new[] { "HBRGOSakiTargetTypes", "HotaruTargetTypes" })
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Integration." + name, true)!;
            foreach (PropertyInfo property in type.GetProperties(F).Where(p => p.PropertyType == typeof(TargetType))) Prefix(property.GetMethod!);
        }
        TestMode.IsOn = true;
        Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.CardDescriptionLocPatch", true)!).Patch().Count > 0,
            "real card description localization patch installs");
        Type displayPatch = Mod.GetType("HBRGOSakiMod.Core.Patches.HbrSkillCardDisplayPatch", true)!;
        Check(h.CreateClassProcessor(displayPatch).Patch().Count > 0,
            "actual NCard UpdateVisuals display prefix/postfix/finalizer Harmony patch installs without a Godot node");
        Type nativeCardNode = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Cards.NCard", true)!;
        MethodInfo update = AccessTools.Method(nativeCardNode, "UpdateVisuals");
        Patches patchInfo = Harmony.GetPatchInfo(update)!;
        Check(patchInfo.Prefixes.Any(p => p.PatchMethod.DeclaringType == displayPatch) &&
            patchInfo.Postfixes.Any(p => p.PatchMethod.DeclaringType == displayPatch) &&
            patchInfo.Finalizers.Any(p => p.PatchMethod.DeclaringType == displayPatch),
            "native NCard UpdateVisuals owns production prefix plus both success and exception cleanup callbacks");
        // Real numeric adapters are used by target-aware display, rather than
        // replacing calculation with a fixture implementation of the formula.
        foreach (string name in new[] { "HbrgoSakiAdditiveDamagePatch", "HbrNumericBlockPatch" })
            Check(h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!).Patch().Count > 0,
                "production numeric patch installs " + name);
    }
    static void Localization()
    {
        string runtime = System.Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY") ??
            "D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(runtime, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        var manager = Blank<LocManager>();
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        Set(manager, "<Language>k__BackingField", "zhs");
        Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture);
        Set(manager, "<StringComparer>k__BackingField", StringComparer.Ordinal);
        var tables = new Dictionary<string, LocTable>();
        foreach (string file in new[] { "cards", "powers", "card_keywords" })
        {
            var text = new Dictionary<string, string>();
            string native = Path.Combine("D:/stsmod/sts/localization/zhs", file + ".json");
            if (File.Exists(native)) text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(native))!;
            foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(
                Root, "HBRGOSakiMod/localization/zhs", file + ".json")))!) text[pair.Key] = pair.Value;
            if (file == "cards") CardText = text;
            tables[file] = new LocTable(file, text, null!);
        }
        Set(manager, "_tables", tables); Call(typeof(LocManager), "LoadLocFormatters", manager);
    }
    static void World()
    {
        Player = Blank<Player>(); Hero = Blank<Creature>(); Manual = Blank<ElectronicSoldierManual>();
        var run = Blank<RunState>(); var combat = Blank<CombatState>();
        Set(Player, "<Character>k__BackingField", Blank<HbrgoSaki>());
        Set(Player, "<Creature>k__BackingField", Hero); Set(Player, "_runState", run);
        Set(Hero, "<Player>k__BackingField", Player); Set(Hero, "<Side>k__BackingField", CombatSide.Player);
        Set(Hero, "_powers", new List<PowerModel>()); Set(Hero, "_maxHp", 3500); Set(Hero, "_currentHp", 3500);
        Set(Manual, "<IsMutable>k__BackingField", true); Set(Manual, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { Manual }); Mode(true);
        Set(run, "_players", new List<Player> { Player });
        foreach (string name in new[] { "_visitedMapCoords", "_mapPointHistory", "_currentRooms" })
            Set(run, name, Activator.CreateInstance(Field(typeof(RunState), name).FieldType));
        Set(combat, "<RunState>k__BackingField", run); Hero.CombatState = combat;
        Enemy = Blank<Creature>(); Set(Enemy, "_powers", new List<PowerModel>());
        Set(Enemy, "<Side>k__BackingField", CombatSide.Enemy); Set(Enemy, "_maxHp", 10000); Set(Enemy, "_currentHp", 10000);
        Set(Enemy, "<Monster>k__BackingField", Blank<SmallHopper>()); Enemy.CombatState = combat;
        Set(combat, "_allies", new List<Creature> { Hero }); Set(combat, "_enemies", new List<Creature> { Enemy });
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Models.CardPools.HBRGOSaki31OCardPool", true)!;
        var model = (AbstractModel)Activator.CreateInstance(pool)!;
        ((Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!)[model.Id] = model;
    }
    static CardModel Card(Type type, bool owned = true)
    {
        var content = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
        if (content.Values.FirstOrDefault(m => m.GetType() == type) is not CardModel canonical)
        { canonical = (CardModel)Activator.CreateInstance(type)!; content[canonical.Id] = canonical; }
        var card = canonical.ToMutable(); Set(card, "_owner", owned ? Player : null); return card;
    }
    static IDisposable Scope(CardModel card, bool detail, bool preview = false, Creature? target = null) =>
        (IDisposable)Helper("HbrCardDescriptionContext").GetMethods(F)
            .Single(m => m.Name == "Begin" && m.GetParameters().Length == 4)
            .Invoke(null, [card, detail, preview, target])!;
    static LocString Bind(CardModel card, string? key = null, bool extras = true)
    {
        LocString text = card.Description;
        if (key != null) { var replacement = new LocString("cards", key); replacement.AddVariablesFrom(text); text = replacement; }
        card.DynamicVars.AddTo(text);
        if (extras) AccessTools.Method(card.GetType(), "AddExtraArgsToDescription").Invoke(card, [text]);
        text.Add(new IfUpgradedVar(card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));
        text.Add("OnTable", false); text.Add("InCombat", false); text.Add("IsTargeting", false);
        text.Add("TargetType", card.TargetType.ToString()); text.Add("GainsBlock", card.GainsBlock);
        text.Add("IsOstyAlive", false); text.Add("energyPrefix", "energy"); text.Add("singleStarIcon", "[img]star.png[/img]");
        return text;
    }
    static string Clean(string text) => Regex.Replace(text, "\\[[^\\]]+\\]", "");
    static string Render(CardModel card, bool detail, bool preview = false, Creature? target = null, string? key = null)
    {
        using var scope = Scope(card, detail, preview, target);
        var before = card.DynamicVars.Values.ToDictionary(v => v.Name, v => v.BaseValue);
        LocString text = Bind(card, key); string rendered = text.GetFormattedText();
        string label = card.GetType().Name + (card.IsUpgraded ? "+" : "") + (detail ? " detail " : " card ") + text.LocEntryKey;
        Check(!rendered.Contains('{') && !rendered.Contains('}'), label + " fully formats with native SmartFormat");
        Check(!Regex.IsMatch(rendered, @"\d+(?:\.\d+)?%倍率(?:伤害|格挡)"), label + " omits former coefficient-only damage/block text");
        Check(before.All(pair => card.DynamicVars[pair.Key].BaseValue == pair.Value), label + " formatting does not mutate raw variables");
        Rows.Add(new { card = card.GetType().Name, upgraded = card.IsUpgraded, detail, preview, target = target != null, key = text.LocEntryKey, rendered });
        return Clean(rendered);
    }
    static void Upgrade(CardModel card)
    {
        if (!card.IsUpgradable) return;
        card.UpgradeInternal(); card.FinalizeUpgradeInternal();
    }
    static void MainCases()
    {
        CardModel strike = Card(typeof(StrikeHbrgoSaki)); CardModel defend = Card(typeof(DefendHbrgoSaki));
        Check(Render(strike, false).Contains("造成6000点伤害") && !Render(strike, false).Contains('%'), "noncombat Strike shows numeric base damage6000 without coefficient");
        Check(Render(strike, true).Contains("造成6000点伤害（100%）"), "noncombat Strike detail appends exactly parentheses100percent");
        Check(Render(defend, false).Contains("获得500点格挡") && !Render(defend, false).Contains('%'), "INT100 Defend shows500 block without coefficient");
        Check(Render(defend, true).Contains("获得500点格挡（100%）"), "INT100 Defend detail appends100percent");
    }
    static void MoreCases()
    {
        CardModel strike = Card(typeof(StrikeHbrgoSaki)); CardModel defend = Card(typeof(DefendHbrgoSaki));
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 50);
        Check(Render(defend, false).Contains("获得1000点格挡"), "owned noncombat Defend uses current INT200");
        Check(Render(Card(typeof(DefendHbrgoSaki), false), false).Contains("获得500点格挡"), "ownerless Defend uses INT100 reference500");
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 0);
        strike.DynamicVars.Damage.PreviewValue = 4321m;
        Check(Render(strike, false, true).Contains("造成4321点伤害"), "ordinary native preview displays real4321 without rescaling");
        Check(Render(strike, true, true).Contains("造成4321点伤害（100%）"), "detail retains exact4321 native preview and100percent");
        Check(Render(strike, false).Contains("造成6000点伤害"), "leaving preview context restores noncombat base rather than leaked4321");
        Upgrade(strike); Upgrade(defend);
        Check(strike.DynamicVars.Damage.BaseValue == 9m && defend.DynamicVars.Block.BaseValue == 8m,
            "real upgrades retain raw nine damage and eight block units");
        Check(Render(strike, true).Contains("造成9000点伤害（150%）"), "upgraded Strike shows9000 and150percent in detail");
        Check(Render(defend, true).Contains("获得800点格挡（160%）"), "upgraded Defend shows800 and160percent in detail");
        DynamicVar clone = strike.DynamicVars.Damage.Clone(); clone.UpgradeValueBy(3m);
        Check(clone.BaseValue == 12m && strike.DynamicVars.Damage.BaseValue == 9m,
            "clone and later upgrade stay raw and independent after description calls");
        strike.DynamicVars.Damage.PreviewValue = 12000m;
        string boosted = Render(strike, false, true);
        Check(boosted.Contains("12000"),
            "upgraded boosted preview keeps actual damage number");
        using (Scope(strike, false, true))
            Check((string)Bind(strike).Variables["DamageDisplay"] == "[green]12000[/green]",
                "positive native preview retains green numeric highlighting");
        strike.DynamicVars.Damage.PreviewValue = 1000m;
        using (Scope(strike, false, true))
            Check((string)Bind(strike).Variables["DamageDisplay"] == "[red]1000[/red]",
                "reduced native preview retains red numeric highlighting");
        strike.DynamicVars.Damage.ResetToBase();
        Mode(false);
        string legacy = Render(strike, false);
        Check(legacy.Contains("造成9点伤害") && !legacy.Contains('%'), "disabled mode retains original nine-damage description");
        Mode(true);
        CardModel growth = Card(typeof(HotaruFingertipHabit));
        using (Scope(growth, true))
        {
            LocString growthText = Bind(growth);
            Check(Clean((string)growthText.Variables["BaseGainDisplay"]) == "2000" &&
                (string)growthText.Variables["BaseGainSkillDetail"] == "（33.33%）" &&
                Clean((string)growthText.Variables["BaseGainBlockDisplay"]) == "200" &&
                (string)growthText.Variables["BaseGainBlockSkillDetail"] == "（40%）",
                "one shared cannon growth variable exposes separate damage/block numeric units and inspect coefficients");
        }
        CardModel step = Card(typeof(HotaruStepUpward));
        using (Scope(step, true))
        {
            LocString stepText = Bind(step);
            Check(Clean((string)stepText.Variables["CurrentDamageDisplay"]) == "8000" &&
                (string)stepText.Variables["CurrentDamageSkillDetail"] == "（133.33%）",
                "computed StepUpward CurrentDamage uses real extra-argument numeric display helper");
        }
        var targeted = Card(typeof(StrikeHbrgoSaki));
        typeof(CardModel).GetProperty("UpgradePreviewType", F)!.SetValue(targeted, CardUpgradePreviewType.Combat);
        string targetedText = Render(targeted, true, target: Enemy);
        Check(targetedText.Contains("造成3857点伤害（100%）"),
            "target-aware inspect uses real HBR damage adapter against E150 and floors3857point14");
        targeted.DynamicVars.Damage.PreviewValue = 7777m;
        Check(Render(targeted, false, true, Enemy).Contains("造成7777点伤害"),
            "native NCard preview takes precedence over recomputation when explicitly requested");
        using ((IDisposable)Helper("HbrCardDescriptionContext").GetMethods(F)
            .Single(m => m.Name == "Begin" && m.GetParameters().Length == 5)
            .Invoke(null, [targeted, true, false, Enemy, true])!)
            Check(Clean(Bind(targeted).GetFormattedText()).Contains("造成6000点伤害（100%）"),
                "force-unpowered inspect retains world-space6000 rather than native compatibility6");
        Type shield = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.StrongestShieldDamagePatch", true)!;
        FieldInfo depth = shield.GetField("_intentPreviewDepth", F)!;
        int beforeDepth = (int)depth.GetValue(null)!;
        ObservePreviewGuard = true;
        try { Render(targeted, true, target: Enemy); }
        finally { ObservePreviewGuard = false; }
        Check(ObservedPreviewGuards > 0 && (int)depth.GetValue(null)! == beforeDepth,
            "successful live description calculation restores preview guard depth");
        ThrowPreviewListener = true;
        try
        {
            using var scope = Scope(targeted, true, false, Enemy);
            try { Bind(targeted); throw new Exception("expected controlled preview failure"); }
            catch (Exception exception) when (exception.ToString().Contains("controlled-preview-listener-fault")) { }
        }
        finally { ThrowPreviewListener = false; }
        Check((int)depth.GetValue(null)! == beforeDepth, "throwing live description calculation restores StrongestShield guard depth");
    }
    static void ContextCases()
    {
        CardModel first = Card(typeof(StrikeHbrgoSaki)); CardModel second = Card(typeof(DefendHbrgoSaki));
        using (Scope(first, true, true, Enemy))
        {
            using (Scope(null!, false))
                Check(!(bool)Call(Helper("HbrCardDescriptionContext"), "IsDetail", null, first)!,
                    "null-card scope is safe and cannot borrow another card's detail flag");
            first.DynamicVars.Damage.PreviewValue = 3333m;
            string outer = Clean(Bind(first).GetFormattedText());
            Check(outer.Contains("3333点伤害（100%）"), "outer detail/native-preview scope applies to its own card");
            Check(Clean(Bind(second).GetFormattedText()).Contains("500点格挡") &&
                !Clean(Bind(second).GetFormattedText()).Contains("（100%）"), "detail context cannot leak onto another card");
            using (Scope(second, true))
                Check(Clean(Bind(second).GetFormattedText()).Contains("500点格挡（100%）"),
                    "nested different-card detail scope applies to nested card");
            Check(Clean(Bind(first).GetFormattedText()) == outer, "nested scope disposal restores exact outer card preview/detail");
            try { using (Scope(first, false)) throw new InvalidOperationException("controlled-scope-fault"); }
            catch (InvalidOperationException) { }
            Check(Clean(Bind(first).GetFormattedText()) == outer, "throwing nested scope also restores exact outer context");
        }
        string after = Clean(Bind(first).GetFormattedText());
        Check(after.Contains("6000点伤害") && !after.Contains("（100%）") && !after.Contains("3333"),
            "all scopes disposed leaves no detail, target or stale native preview context");
        Check(!(bool)Call(Helper("HbrCardDescriptionContext"), "IsDetail", null, first)! &&
            !(bool)Call(Helper("HbrCardDescriptionContext"), "UsesNativePreview", null, first)! &&
            Call(Helper("HbrCardDescriptionContext"), "GetTarget", null, first) == null,
            "context query API confirms exact detail, native-preview and target cleanup after disposal");
    }
    static void DeferredBlockCases()
    {
        foreach ((Type type, decimal raw) in new[] { (typeof(HotaruBreathAfterMiss), 4m),
            (typeof(HotaruBreathingRhythm), 4m), (typeof(HotaruStillCovered), 6m) })
        {
            CardModel card = Card(type); card.DynamicVars.Block.PreviewValue = raw;
            using var scope = Scope(card, true, true);
            LocString text = Bind(card);
            Check(Clean((string)text.Variables["BlockDisplay"]) == (raw * 100m).ToString("0", CultureInfo.InvariantCulture),
                type.Name + " future Unpowered block grant displays resolved" + raw * 100m + " even when native preview remains raw" + raw);
            Check(card.DynamicVars.Block.BaseValue == raw && card.DynamicVars.Block.PreviewValue == raw,
                type.Name + " future grant display never overwrites raw storage or native preview");
            Check(!text.GetFormattedText().Contains('{'), type.Name + " future grant template fully formats");
        }
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 50);
        using (Scope(Card(typeof(StrikeHbrgoSaki)), false))
        {
            CardModel card = Card(typeof(RefuseToLeaveHbrgoSaki));
            Check(Clean((string)Bind(card).Variables["RefuseToLeavePowerDisplay"]) == "2000" &&
                card.DynamicVars["RefuseToLeavePower"].BaseValue == 10m,
                "RefuseToLeavePower stored raw10 renders actual future block2000 at owner INT200");
        }
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(Manual, 0);
    }
    static void TemplateCases()
    {
        Type pack = Mod.GetType("HBRGOSakiMod.Core.Integration.HBRGOSakiContentPack", true)!;
        Type[] types = new[] { "CardTypes", "HotaruCardTypes", "HotaruDerivedCardTypes" }
            .SelectMany(name => (Type[])pack.GetField(name, F)!.GetValue(null)!).Append(typeof(DragonArrowHbrgoSaki))
            .Distinct().OrderBy(t => t.FullName).ToArray();
        var covered = new HashSet<string>(); int skillVariables = 0;
        foreach (Type type in types)
        {
            try
            {
            CardModel card = Card(type);
            for (int upgraded = 0; upgraded < 2; upgraded++)
            {
                if (upgraded == 1) { if (!card.IsUpgradable) break; Upgrade(card); }
                foreach (string key in CardText.Keys.Where(k => k.StartsWith(card.Id.Entry + ".", StringComparison.Ordinal) && k.EndsWith(".hbr", StringComparison.Ordinal)).Order())
                {
                    string template = CardText[key];
                    using (Scope(card, true))
                    {
                        LocString text = Bind(card, key);
                        foreach (string name in Regex.Matches(template, @"\{([A-Za-z][A-Za-z0-9_]*(?:Display|SkillDetail))\b")
                            .Select(m => m.Groups[1].Value).Distinct())
                        {
                            Check(text.Variables.ContainsKey(name), card.GetType().Name + "." + name + " real display binder covers template field");
                            skillVariables++;
                        }
                    }
                    Render(card, false, key: key); Render(card, true, key: key); covered.Add(key);
                }
            }
            }
            catch (Exception exception) { TemplateErrors.Add(new { type = type.FullName, error = exception.ToString() }); }
        }
        Check(covered.Count > 100, "more than100 real HBR card templates fully formatted in ordinary and detail context");
        Check(skillVariables > 300, "more than300 real display/detail bindings checked across raw and upgraded cards");
        Rows.Add(new { templates = covered.Count, skillVariables, uncoveredHbrKeys = CardText.Keys.Where(k => k.EndsWith(".hbr") && !covered.Contains(k)).ToArray() });
        Check(TemplateErrors.Count == 0, "all registered HBR templates format without model or placeholder errors; inspect templateErrors");
    }
    public static Task Run()
    {
        bool ok = false; string? error = null;
        try { Install(); Localization(); World(); MainCases(); MoreCases(); ContextCases(); DeferredBlockCases(); TemplateCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, rows = Rows, templateErrors = TemplateErrors, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CardModel).Assembly.Location))),
            localizationSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, "HBRGOSakiMod/localization/zhs/cards.json")))),
            limits = "Real canonical/mutable card models, upgrade routines, dynamic-variable clones, production localization/numeric adapters, description scopes, LocManager, LocTable and native SmartFormat formatters execute. Godot StringName/logging and custom target registration leaves are isolated; hook listeners are an empty controlled list. Native preview values are prepared as the NCard display boundary input. No rendered Godot UI, game process, save files, packaging or Steam upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} card display checks");
        if (!ok) throw new Exception("Card display fixture failed; inspect report.json");
        return Task.CompletedTask;
    }
}
