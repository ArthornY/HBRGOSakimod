using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

static class HotaruDebuffDurationChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Assembly Mod = typeof(HotaruBuoy).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> Rendered = [];
    static readonly Type[] Kinds = [typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower), typeof(FragilePower)];
    static readonly string Root = FindRoot();
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    { try { return method.Invoke(owner, args); } catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; } }
    static object? Fixture(string name, params object?[] args) => Invoke(typeof(EnemyStatusChecks).GetMethod(name, F)!, null, args);
    static T State<T>(string name) => (T)Invoke(typeof(EnemyStatusChecks).GetMethod("State", F)!.MakeGenericMethod(typeof(T)), null, name)!;
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static Player Player => State<Player>("Player");
    static CombatState Combat => State<CombatState>("Combat");
    static void Set(object owner, string field, object? value) => Fixture("Set", owner, field, value);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string name, params object?[] args) => Invoke(Helper(helper).GetMethod(name, F)!, null, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Passed.Add(name); }
    static T Blank<T>() { var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static PowerModel Raw(Type type) => (PowerModel)Invoke(typeof(EnemyStatusChecks).GetMethod("Raw", F)!.MakeGenericMethod(type), null, Enemy, 1)!;
    static void Reset() { Fixture("Clear"); Set(Combat, "<CurrentSide>k__BackingField", CombatSide.Player); }
    static string FindRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "HBRGOSakiMod.csproj"))) return directory.FullName;
        throw new Exception("Cannot locate repository");
    }
    static CardModel Card(Type type, bool upgraded = false)
    {
        if (!ModelDb.Contains(type)) ModelDb.Inject(type);
        var card = (CardModel)ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();
        Set(card, "_owner", Player); card.UpgradePreviewType = CardUpgradePreviewType.Combat;
        if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
        return card;
    }
    static async Task Play(CardModel card)
    {
        var play = Blank<CardPlay>(); Set(play, "<Card>k__BackingField", card); Set(play, "<Target>k__BackingField", Enemy);
        await (Task)Invoke(AccessTools.Method(card.GetType(), "OnPlay"), card, new ThrowingPlayerChoiceContext(), play)!;
    }
    static bool Skip() => false;
    static bool DrawLeaf(ref Task<IEnumerable<CardModel>> __result) { __result = Task.FromResult<IEnumerable<CardModel>>([]); return false; }
    static bool IconLeaf(ref Godot.Texture2D? __result) { __result = null; return false; }
    static bool EnergyPrefix(ref string __result) { __result = "energy"; return false; }
    static void Setup()
    {
        Fixture("Setup");
        var h = new Harmony("hbr.hotaru-debuff-duration");
        h.Patch(typeof(CardPileCmd).GetMethods(F).Single(m => m.Name == "Draw" && m.GetParameters().Length == 4),
            prefix: new HarmonyMethod(typeof(HotaruDebuffDurationChecks).GetMethod(nameof(DrawLeaf), F)!));
        h.Patch(AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Icon)),
            prefix: new HarmonyMethod(typeof(HotaruDebuffDurationChecks).GetMethod(nameof(IconLeaf), F)!));
        h.Patch(AccessTools.PropertyGetter(typeof(CardModel), "Pile"),
            prefix: new HarmonyMethod(typeof(HotaruDebuffDurationChecks).GetMethod(nameof(Skip), F)!));
        Type energy = typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper", true)!;
        foreach (MethodInfo method in energy.GetMethods(F).Where(m => m.Name == "GetPrefix"))
            h.Patch(method, prefix: new HarmonyMethod(typeof(HotaruDebuffDurationChecks).GetMethod(nameof(EnergyPrefix), F)!));
        foreach (string patch in new[] { "CardDescriptionLocPatch", "HbrCompactTextFormatPatch" })
            h.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches." + patch, true)!).Patch();
        var manager = Blank<LocManager>();
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        Set(manager, "<Language>k__BackingField", "zhs"); Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture);
        Set(manager, "<StringComparer>k__BackingField", StringComparer.Ordinal);
        var tables = new Dictionary<string, LocTable>();
        foreach (string name in new[] { "cards", "powers", "card_keywords" })
        {
            var rows = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Root, "HBRGOSakiMod/localization/zhs", name + ".json")))!;
            tables[name] = new LocTable(name, rows, null!);
        }
        Set(manager, "_tables", tables); Invoke(typeof(LocManager).GetMethod("LoadLocFormatters", F)!, manager);
        Type pool = Mod.GetType("HBRGOSakiMod.Core.Models.CardPools.HBRGOSaki31OCardPool", true)!;
        if (!ModelDb.Contains(pool)) ModelDb.Inject(pool);
    }
    static string Face(CardModel card)
    {
        using var scope = (IDisposable)Invoke(Helper("HbrCardDescriptionContext").GetMethods(F)
            .Single(m => m.Name == "Begin" && m.GetParameters().Length == 4), null, card, false, false, null)!;
        LocString text = card.Description; card.DynamicVars.AddTo(text);
        Invoke(AccessTools.Method(card.GetType(), "AddExtraArgsToDescription"), card, text);
        text.Add(new IfUpgradedVar(card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));
        text.Add("OnTable", false); text.Add("InCombat", true); text.Add("IsTargeting", false); text.Add("TargetType", card.TargetType.ToString());
        text.Add("GainsBlock", card.GainsBlock); text.Add("IsOstyAlive", false); text.Add("energyPrefix", "energy");
        string formatted = text.GetFormattedText();
        Check(!formatted.Contains('{') && !formatted.Contains('}'), card.GetType().Name + "+" + card.IsUpgraded + " card face fully formats through native localization");
        Rendered.Add(new { card = card.GetType().Name, upgraded = card.IsUpgraded, key = text.LocEntryKey, face = formatted });
        return Regex.Replace(formatted, "\\[[^\\]]+\\]", "");
    }
    static async Task CardCases()
    {
        foreach (Type type in new[] { typeof(HotaruBuoy), typeof(HotaruGentleTouch) })
        foreach (bool upgraded in new[] { false, true })
        {
            CardModel card = Card(type, upgraded); string label = type.Name + "+" + upgraded;
            Check(type == typeof(HotaruBuoy)
                ? card.DynamicVars["Times"].IntValue == (upgraded ? 2 : 1) && card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0
                : card.DynamicVars.Cards.IntValue == 1 && card.EnergyCost.GetWithModifiers(CostModifiers.Local) == (upgraded ? 1 : 2),
                label + " retains original missing-kind count, draw count and upgrade cost");
            foreach (Type missing in Kinds)
            {
                Reset(); foreach (Type existing in Kinds.Where(t => t != missing)) Raw(existing);
                await Play(card); PowerModel effect = Enemy.Powers.Single(p => p.GetType() == missing);
                Check(effect.Amount == 2, label + " real OnPlay gives " + missing.Name + " initial Amount2 (actual " + effect.Amount + ")");
                int modifierCount = (int)effect.GetType().GetProperty("HbrModifierCount", F)!.GetValue(effect)!;
                string percentProperty = missing == typeof(FragilePower) ? "HbrEffectPercent" : "EffectPercent";
                decimal percent = (decimal)effect.GetType().GetProperty(percentProperty, F)!.GetValue(effect)!;
                decimal single = (decimal)Call("HbrPercentDebuffHelper", "ResolvePercent", effect, 1m, Hero, Enemy)!;
                Check(modifierCount == 1 && percent == single,
                    label + " " + missing.Name + " retains one modifier potency despite two-turn duration");
                await effect.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
                Check(effect.Amount == 2 && Enemy.Powers.Contains(effect), label + " " + missing.Name + " survives immediate player end with2");
                await effect.AfterSideTurnEnd(null!, CombatSide.Enemy, [Enemy]);
                Check(effect.Amount == 1 && Enemy.Powers.Contains(effect), label + " " + missing.Name + " first enemy end gives1");
                await effect.AfterSideTurnEnd(null!, CombatSide.Enemy, [Enemy]);
                Check(!Enemy.Powers.Contains(effect), label + " " + missing.Name + " second enemy end removes");
            }
            string face = Face(card);
            Check(face.Contains("2回合"), label + " card face reports actual two turns");
        }
        foreach (bool upgraded in new[] { false, true })
        {
            Reset(); var card = Card(typeof(HotaruGentleTouch), upgraded); await Play(card);
            Check(Enemy.Powers.Count == 3 && Enemy.Powers.All(p => p.Amount == 2 && Kinds.Contains(p.GetType())),
                "GentleTouch+" + upgraded + " gives all three missing kinds at2 in one actual play");
            Reset(); Set(State<object>("RunState"), "<Rng>k__BackingField", new RunRngSet("hotaru-debuff-duration-" + upgraded));
            card = Card(typeof(HotaruBuoy), upgraded); await Play(card);
            Check(Enemy.Powers.Count == (upgraded ? 2 : 1) && Enemy.Powers.All(p => p.Amount == 2 && Kinds.Contains(p.GetType())),
                "Buoy+" + upgraded + " actual synced random missing selection gives correct distinct count at2");
        }
    }
    static async Task DefaultCases()
    {
        foreach (Type kind in Kinds)
        {
            Reset(); PowerModel power = (PowerModel)ModelDb.GetById<PowerModel>(ModelDb.GetId(kind)).ToMutable();
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), power, Enemy, 1m, Hero, Card(typeof(MimicThunderGaleHbrgoSaki)), true);
            Check(power.Amount == 1, kind.Name + " original card default remains one turn");
            await power.AfterSideTurnEnd(null!, CombatSide.Enemy, [Enemy]);
            Check(!Enemy.Powers.Contains(power), kind.Name + " original default removes at first enemy end");
        }
        Reset(); await (Task)Call("HbrDpBreakStatusHelper", "Apply", new ThrowingPlayerChoiceContext(), Enemy)!;
        var penalty = Enemy.Powers.OfType<HbrAttackDownPower>().Single();
        Check(penalty.Amount == 1 && (decimal)penalty.GetType().GetProperty("EffectPercent", F)!.GetValue(penalty)! == 30m,
            "DP break remains fixed30 with one turn");
        await penalty.AfterSideTurnEnd(null!, CombatSide.Player, [Hero]);
        Check(Enemy.Powers.Contains(penalty), "DP break survives opposite side end");
        await penalty.BeforeSideTurnStart(null!, CombatSide.Enemy, [Enemy], Combat);
        await penalty.AfterSideTurnEnd(null!, CombatSide.Enemy, [Enemy]);
        Check(!Enemy.Powers.Contains(penalty), "DP break still removes after next participating own turn end");
    }
    static void MergeCases()
    {
        // Exercise the actual merger with real native HoverTip instances. No
        // input-device/UI calls or precomputed merged strings are substituted.
        foreach (Type source in new[] { typeof(HotaruBuoy), typeof(HotaruGentleTouch), typeof(MimicIceFlowerThousandArrowsHbrgoSaki) })
        {
            CardModel card = Card(source);
            using var compact = (IDisposable)Call("HbrCompactText", "Enter")!;
            List<IHoverTip> tips = Kinds.Select(kind =>
            {
                var power = ModelDb.GetById<PowerModel>(ModelDb.GetId(kind));
                var text = new LocString("powers", power.Id.Entry + ".hbrReferenceDescription");
                return (IHoverTip)new HoverTip(power, text.GetFormattedText(), false);
            }).ToList();
            Check((bool)Call("HbrCompactHoverTipHelper", "MergeDebuffs", tips)!, source.Name + " three debuffs merge through production merger");
            string body = ((HoverTip)tips.Single()).Description;
            Check(!body.Contains("都持续2回合") && !body.Contains("持续2回合，最多并存2份") && !body.Contains("都持续"),
                source.Name + " merged reference does not invent a shared fixed2 duration");
            Rendered.Add(new { source = source.Name, merged = body });
        }
        object attack = Call("HbrEffectProfileResolver", "ForType", typeof(HbrAttackDownPower), Card(typeof(MimicIceFlowerThousandArrowsHbrgoSaki)))!;
        object defense = Call("HbrEffectProfileResolver", "ForType", typeof(HbrDefenseDownPower), Card(typeof(MimicIceFlowerThousandArrowsHbrgoSaki)))!;
        Check(!attack.Equals(defense), "mixed original Ice reference actually contains different duration profiles");
    }
    static void LegacyCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "HBRGOSakiMod/localization/zhs/cards.json")));
        Check(document.RootElement.GetProperty("HOTARU_BUOY.description").GetString() ==
            "随机给予目标1层它还没有的[gold]虚弱[/gold]、[gold]易伤[/gold]或[gold]脆弱[/gold]，共{Times:diff()}次；三种都有时不再给予。",
            "Buoy legacy localization remains byte-for-byte text equivalent");
        Check(document.RootElement.GetProperty("HOTARU_GENTLE_TOUCH.description").GetString() ==
            "给予目标它还没有的[gold]虚弱[/gold]、[gold]易伤[/gold]、[gold]脆弱[/gold]各1层。抽{Cards:diff()}张牌。",
            "GentleTouch legacy localization remains byte-for-byte text equivalent");
        Invoke(typeof(Checks).GetMethod("Mode", F)!, null, false, 1);
        try
        {
            foreach (Type type in new[] { typeof(HotaruBuoy), typeof(HotaruGentleTouch) })
            foreach (bool upgraded in new[] { false, true })
            {
                string face = Face(Card(type, upgraded));
                Check(face.Contains("1层") && face.Contains("虚弱") && !face.Contains("2回合") && !face.Contains("%"),
                    type.Name + "+" + upgraded + " actual legacy card face keeps native stack wording");
            }
        }
        finally { Invoke(typeof(Checks).GetMethod("Mode", F)!, null, true, 1); }
    }
    public static void Run(string path)
    {
        bool ok = false; string? error = null;
        try { Setup(); CardCases().GetAwaiter().GetResult(); DefaultCases().GetAwaiter().GetResult(); MergeCases(); LegacyCases(); ok = true; }
        catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(ex); }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { ok, checks = Passed.Count, passed = Passed, rendered = Rendered, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"), nativeSourceSha256 = Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            telemetryIsolation = Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary = "Production real card OnPlay, native PowerCmd/application hooks and turn-end callbacks; native localization formatter and HoverTip merger. Draw/texture/animation/UI leaves isolated, no Godot or full in-game turn loop." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " Hotaru duration checks");
        if (!ok) throw new Exception("Inspect " + path);
    }
}
