using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static partial class MergedDamageBuffChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(HotaruDamageAmplificationPower).Assembly;
    static readonly List<string> Passed = [];
    static Type Fixture => typeof(Checks);
    static Creature Hero => State<Creature>("Hero");
    static Creature Enemy => State<Creature>("Enemy");
    static Player Player => State<Player>("Player");
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string method, params object?[] args) => Invoke(Fixture.GetMethod(method, F)!, null, args);
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string method, params object?[] args) => Invoke(Helper(helper).GetMethod(method, F)!, null, args);
    static void Reset() => FixtureCall("Reset");
    static void Mode(bool enabled) => FixtureCall("Mode", enabled, 1);
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static List<PowerModel> Powers(Creature target) => (List<PowerModel>)FixtureCall("Powers", target)!;
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Passed.Add(message); }
    static void Equal(decimal actual, decimal expected, string message) => Check(Math.Abs(actual - expected) < .00000000000000000001m, $"{message}: {actual} = {expected}");
    static HotaruDamageAmplificationPower? Apply(decimal percent, Creature? owner = null) =>
        PowerCmd.Apply<HotaruDamageAmplificationPower>(null!, owner ?? Hero, percent, Hero, null, true).GetAwaiter().GetResult();
    static decimal Preview(Creature dealer, ValueProp props = ValueProp.Move) =>
        (decimal)FixtureCall("Preview", Enemy, dealer, 6m, props, null)!;
    static decimal Actual(Creature dealer, ValueProp props = ValueProp.Move) =>
        (decimal)FixtureCall("Actual", Enemy, dealer, 6m, props)!;
    static HotaruDamageAmplificationPower Fresh(Creature owner)
    {
        var power = (HotaruDamageAmplificationPower)ModelDb.Power<HotaruDamageAmplificationPower>().ToMutable();
        Set(power, "_owner", owner); return power;
    }

    static void Install()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.merged-damage-buff.checks");
        foreach (Type type in Mod.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false) &&
            (t.Name.StartsWith("ExactDamagePercent", StringComparison.Ordinal) || t.Name is "HighDimensionalShellStackAmountPatch" or "DamageAmplificationStackingPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "installs " + type.Name);
        foreach (Type type in new[] { typeof(HotaruDamageAmplificationPower), typeof(PhantomDamageInheritancePower), typeof(PhantomPower) })
        {
            ModelDb.Inject(type);
            Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
        }
#if HBR_BETA_NATIVE
        typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo", true)!.GetMethod("Init", F)!.Invoke(null, null);
        typeof(ModelIdSerializationCache).GetMethod("Init", F)!.Invoke(null, null);
        foreach (Type type in new[] { typeof(HotaruDamageAmplificationPower), typeof(PhantomDamageInheritancePower) })
            typeof(ModelIdSerializationCache).GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]);
#else
        foreach (Type type in new[] { typeof(HotaruDamageAmplificationPower), typeof(PhantomDamageInheritancePower) }) SavedPropertiesTypeCache.InjectTypeIntoCache(type);
#endif
        InitializeLocalization(); Reset();
    }

    static string RepositoryRoot
    {
        get
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "HBRGOSakiMod.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Fixture must remain below its checkout");
        }
    }
    static void InitializeLocalization()
    {
        var manager = Blank<LocManager>();
        typeof(LocManager).GetField("<Instance>k__BackingField", F)!.SetValue(null, manager);
        Set(manager, "<Language>k__BackingField", "zhs"); Set(manager, "<CultureInfo>k__BackingField", CultureInfo.InvariantCulture); Set(manager, "<StringComparer>k__BackingField", StringComparer.Ordinal);
        var tables = new Dictionary<string, LocTable>();
        foreach (string name in new[] { "cards", "powers", "card_keywords" })
        {
            var text = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs", name + ".json")))!;
            tables[name] = new LocTable(name, text, null!);
        }
        Set(manager, "_tables", tables); typeof(LocManager).GetMethod("LoadLocFormatters", F)!.Invoke(manager, null);
    }

    static void CanonicalCases()
    {
        foreach (bool hbr in new[] { true, false })
        {
            Reset(); Mode(hbr); decimal basis = Preview(Hero);
            var power = Apply(.125m)!;
            Check(power.Amount == 1 && Powers(Hero).Count(p => p is HotaruDamageAmplificationPower) == 1, $"mode{hbr} positive fraction has one native proxy instance");
            Equal(power.DamagePercent, .125m, $"mode{hbr} initial fractional application keeps exact percent");
            var stacked = Apply(.375m)!;
            Check(ReferenceEquals(power, stacked) && power.Amount == 1, $"mode{hbr} stacked fraction keeps existing instance and native proxy");
            Equal(power.DamagePercent, .5m, $"mode{hbr} stack uses exact offset despite unchanged integer proxy");
            Apply(24.5m); Equal(power.DamagePercent, 25m, $"mode{hbr} whole and fractional awards combine");
            Equal(Preview(Hero), hbr ? decimal.Floor(basis * 1.25m) : basis * 1.25m, $"mode{hbr} full native hook applies canonical total once");
            Equal(Actual(Hero), Preview(Hero), $"mode{hbr} actual wrapper equals full preview");
            Equal(Preview(Hero, ValueProp.Move | ValueProp.Unpowered), 6m, $"mode{hbr} unpowered damage bypasses unified damage buff");
            PowerCmd.ModifyAmount(null!, power, -.125m, Hero, null, true).GetAwaiter().GetResult();
            Equal(power.DamagePercent, 24.875m, $"mode{hbr} native ModifyAmount subtracts exact fraction");
            Equal((decimal)power.Description.Variables["Amount"], 24.875m, $"mode{hbr} description binds exact damage percent");
            Check(!power.Description.GetFormattedText().Contains('{'), $"mode{hbr} unified power description renders");
        }
        Reset();
    }

    static void PhantomCases()
    {
        Reset(); var combat = State<CombatState>("Combat");
        var phantom = (Creature)FixtureCall("Actor", Blank<SmallHopper>(), null, Player)!;
        var phantomMarker = (PhantomPower)ModelDb.Power<PhantomPower>().ToMutable();
        Set(phantomMarker, "_owner", phantom); Powers(phantom).Add(phantomMarker);
        Set(combat, "_allies", new List<Creature> { Hero, phantom });
        try
        {
            decimal basis = Preview(phantom);
            Apply(90m, Hero); Equal(Preview(phantom), basis, "summoner's canonical buff is not inherited implicitly by phantom");
            Apply(12.5m, phantom); Apply(7.5m, phantom);
            Equal(Preview(phantom), decimal.Floor(basis * 1.2m), "phantom's own canonical buff combines and applies once");
            Equal(Actual(phantom), Preview(phantom), "phantom actual wrapper equals preview after canonical merge");
            Equal((decimal)Call("TemporaryDamageBonusHelper", "CalculateEffectiveBonusPercent", phantom)!, 20m,
                "phantom death/inheritance summary reads canonical buff exactly once");
            Check(Powers(phantom).OfType<HotaruDamageAmplificationPower>().Count() == 1, "phantom carries one unified positive damage power");
            var inheritanceCard = (TranscendSelfHbrgoSaki)Invoke(Fixture.GetMethod("OwnedCard", F)!.MakeGenericMethod(typeof(TranscendSelfHbrgoSaki)), null)!;
            FixtureCall("Play", inheritanceCard, null);
            Equal(Powers(Hero).OfType<HotaruDamageAmplificationPower>().Single().DamagePercent, 110m,
                "actual TranscendSelf OnPlay grants phantom inheritance into existing canonical damage buff");
            Check(Powers(Hero).All(p => p.GetType() != typeof(PhantomDamageInheritancePower)),
                "current inheritance card creates no legacy named positive power");
        }
        finally
        {
            Set(combat, "_allies", new List<Creature> { Hero }); Reset();
        }
    }

    static void SaveCases()
    {
        Reset(); var power = Apply(1234567.890125m)!;
        var saved = SavedProperties.From(power) ?? throw new Exception("Missing saved damage percent");
        Check(saved.strings?.Any(s => s.name == "AmplificationPercentData" && s.value == "1234567.890125") == true,
            "canonical saved properties retain exact decimal string");
        var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize(saved, options);
        var loaded = JsonSerializer.Deserialize<SavedProperties>(json, options)!;
        var restored = Fresh(Hero); loaded.FillInternal(restored);
        Equal(restored.DamagePercent, power.DamagePercent, "actual JSON saved-property roundtrip keeps exact amount");
        Check(restored.Amount == 1234567, "JSON load synchronizes native integer proxy");
        var writer = new PacketWriter(); saved.Serialize(writer); var reader = new PacketReader(); reader.Reset(writer.Buffer);
        var packet = new SavedProperties(); packet.Deserialize(reader); var packetRestored = Fresh(Hero); packet.FillInternal(packetRestored);
        Equal(packetRestored.DamagePercent, power.DamagePercent, "actual packet writer/reader retains precise buff");
        Check(reader.BitPosition == writer.BitPosition, "registered packet fields consume full payload");
        var clone = (HotaruDamageAmplificationPower)power.ClonePreservingMutability();
        Equal(clone.DamagePercent, power.DamagePercent, "mutable clone retains exact percent");
        typeof(HotaruDamageAmplificationPower).GetProperty("AmplificationPercentData", F)!.SetValue(clone, "0.125");
        Equal(power.DamagePercent, 1234567.890125m, "clone's exact state is independent from original");
        Equal(clone.DamagePercent, .125m, "clone can independently change its fractional state");
        Reset();
        PowerCmd.Apply(null!, clone, Hero, clone.Amount, Hero, null, true).GetAwaiter().GetResult();
        Equal(clone.DamagePercent, .125m, "clone attachment retains saved exact amount rather than reinterpreting proxy1 as1percent");
        Apply(.375m); Equal(clone.DamagePercent, .5m, "fresh native stacking after clone attachment uses preserved fractional value");
        Reset();

        var huge = Apply(2147483647.875m)!; Apply(.375m);
        Equal(huge.DamagePercent, 2147483648m, "merged exact total saturates at shared temporary damage percentage limit");
        Check(huge.Amount == 999999999, "large canonical percent uses bounded native display proxy");
        Check(Preview(Hero) > 0m && Actual(Hero) == Preview(Hero), "large unified bonus preserves positive preview and actual damage without native overflow");
        Reset();
    }

    static partial void LegacyMigrationCases();

    public static void Run(string reportPath)
    {
        bool ok = false; string? error = null;
        try { Install(); CanonicalCases(); PhantomCases(); SaveCases(); LegacyMigrationCases(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Actual native PowerCmd Apply/ModifyAmount, exact decimal proxy, full damage preview/actual wrapper and phantom outgoing summary; canonical saved properties JSON/PacketWriter/PacketReader and native clone. Shared fixture controls logging/StringName and hook-listener enumeration. No game process, Godot rendering, complete save loading or multiplayer session."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " merged-damage-buff checks");
        if (!ok) throw new Exception("Inspect " + reportPath);
    }
}
