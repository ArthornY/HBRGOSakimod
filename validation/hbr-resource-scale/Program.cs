using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

static partial class Checks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    const decimal Maximum = 18_446_744_073_709_551_616m;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static int FixedCalls;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static T Blank<T>()
    {
        var result = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        GC.SuppressFinalize(result!);
        return result;
    }
    static FieldInfo Field(Type type, string name)
    {
        for (Type? current = type; current != null; current = current.BaseType)
            if (current.GetField(name, F) is { } field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object target, string name, object? value) => Field(target.GetType(), name).SetValue(target, value);
    static object? Call(Type type, string name, object? target, params object?[] args) => type.GetMethod(name, F)!.Invoke(target, args);
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Passed.Add(name);
    }
    static void Equal(decimal actual, decimal expected, string name) => Check(actual == expected, $"{name}: {actual} = {expected}");
    static bool Skip() => false;
    static void FixedEntered() => FixedCalls++;
    static AbstractModel Canonical(Type type)
    {
        var content = (Dictionary<ModelId, AbstractModel>)typeof(ModelDb).GetField("_contentById", F)!.GetValue(null)!;
        if (content.Values.FirstOrDefault(model => model.GetType() == type) is { } existing) return existing;
        var model = (AbstractModel)Activator.CreateInstance(type)!;
        content[model.Id] = model;
        return model;
    }
    static T Relic<T>(Player player) where T : RelicModel
    {
        var result = (T)((RelicModel)Canonical(typeof(T))).ToMutable();
        Set(result, "_owner", player);
        return result;
    }
    static void Data(ElectronicSoldierManual manual, string value) =>
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!.SetValue(manual, value);
    static void Initialize(ElectronicSoldierManual manual) => Call(typeof(ElectronicSoldierManual), "InitializeHbrBattleNumerics", manual);
    sealed class Fixture
    {
        internal readonly Player Player = Blank<Player>();
        internal readonly Creature Hero = Blank<Creature>();
        internal readonly RunState Run = Blank<RunState>();
        internal readonly CombatState Combat = Blank<CombatState>();
        internal readonly ElectronicSoldierManual Book;
        internal Fixture(CharacterModel? character = null, bool enabled = true, int? current = null, bool hotaruBook = false)
        {
            character ??= Blank<HbrgoSaki>();
            int maximum = character.StartingHp;
            Set(Player, "<Character>k__BackingField", character); Set(Player, "<Creature>k__BackingField", Hero); Set(Player, "_runState", Run);
            Set(Hero, "<Player>k__BackingField", Player); Set(Hero, "<Side>k__BackingField", CombatSide.Player);
            Set(Hero, "_powers", new List<PowerModel>()); Set(Hero, "_maxHp", maximum); Set(Hero, "_currentHp", current ?? maximum);
            Set(Run, "_players", new List<Player> { Player });
            foreach (string name in new[] { "_mapPointHistory", "_visitedMapCoords", "_currentRooms" })
            {
                FieldInfo field = Field(Run.GetType(), name); field.SetValue(Run, Activator.CreateInstance(field.FieldType));
            }
            Set(Combat, "<RunState>k__BackingField", Run); Set(Combat, "_allies", new List<Creature> { Hero });
            Set(Combat, "_enemies", new List<Creature>()); Hero.CombatState = Combat;
            Book = hotaruBook ? Relic<HotaruElectronicSoldierManual>(Player) : Relic<ElectronicSoldierManual>(Player);
            Set(Player, "_relics", new List<RelicModel> { Book });
            if (enabled) Initialize(Book);
        }
        internal DeflectorProtection Shield()
        {
            var shield = Relic<DeflectorProtection>(Player);
            ((List<RelicModel>)Field(typeof(Player), "_relics").GetValue(Player)!).Add(shield);
            shield.AfterObtained().GetAwaiter().GetResult();
            return shield;
        }
        internal Creature Phantom(int raw = 80)
        {
            var creature = Blank<Creature>(); var monster = Blank<SmallHopper>();
            Set(creature, "_petOwner", Player); Set(creature, "<Side>k__BackingField", CombatSide.Player);
            Set(creature, "_maxHp", raw); Set(creature, "_currentHp", raw); Set(creature, "_powers", new List<PowerModel>());
            Set(creature, "<Monster>k__BackingField", monster); Set(monster, "_creature", creature); creature.CombatState = Combat;
            ((List<Creature>)Field(typeof(CombatState), "_allies").GetValue(Combat)!).Add(creature);
            return creature;
        }
    }
    static void Install()
    {
        var harmony = new Harmony("hbr.resource.scale.model.checks");
        void Prefix(MethodBase method) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Skip), F)!));
        Prefix(typeof(StringName).GetConstructor([typeof(string)])!);
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor) Prefix(editor);
        foreach (MethodInfo method in logger.GetMethods(F).Where(method => method.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Prefix(method);
        Prefix(typeof(RelicModel).GetMethod("Flash", F, null, Type.EmptyTypes, null)!);
        harmony.Patch(Helper("HbrResourceValueHelper").GetMethod("Fixed", F)!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(FixedEntered), F)!));
        Check(harmony.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches.HotaruAncientHealthRecoveryPatch", true)!).Patch().Count == 1,
            "production Hotaru ancient recovery guard installs on native BeforeEventStarted");
        TestMode.IsOn = true;
        InitializeResourceSavedPropertyCache(new[] { typeof(ElectronicSoldierManual), typeof(HotaruElectronicSoldierManual), typeof(DeflectorProtection) });
    }
    static void InitializeResourceSavedPropertyCache(IEnumerable<Type> types)
    {
        Assembly native = typeof(Creature).Assembly;
        Type? legacy = native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache");
        if (legacy != null)
        {
            MethodInfo inject = AccessTools.Method(legacy, "InjectTypeIntoCache");
            foreach (Type type in types) inject.Invoke(null, new object[] { type });
            return;
        }
        native.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo", true)!.GetMethod("Init", F)!.Invoke(null, null);
        Type cache = native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        cache.GetMethod("Init", F)!.Invoke(null, null);
        MethodInfo register = cache.GetMethod("CacheSavedPropertiesForTypeDebug", F)!;
        foreach (Type type in types) register.Invoke(null, new object[] { type });
    }
    static void InitialCases()
    {
        Equal(Blank<HbrgoSaki>().StartingHp, 3500, "base character original35 maximum gains100 scale");
        Equal(Blank<HbrgoSaki31O>().StartingHp, 3500, "31O inherits scaled original35 maximum");
        Equal(Blank<HbrgoSaki30A>().StartingHp, 1000, "30A original10 maximum gains100 scale");
        Equal(Blank<HbrgoHotaru>().StartingHp, 3000, "Hotaru original30 maximum gains100 scale");
        Equal((int)typeof(HbrgoHotaru).GetField("InitialCurrentHp", F)!.GetRawConstantValue()!, 1, "Hotaru initial current HP is the explicit unscaled1 exception");
        Check(Blank<HotaruHappinessCannon>().MinInitialHp == 1 && Blank<HotaruHappinessCannon>().MaxInitialHp == 1,
            "non-targetable cannon hidden HP sentinel remains1");
        Fixture fresh = new();
        var shield = fresh.Shield();
        Equal(shield.MaxDp, 7000, "Deflector maximum retains2DP per scaled HP");
        Equal(shield.Dp, 7000, "fresh Deflector starts full");
        string receipt = fresh.Book.HbrBattleProgressData;
        fresh.Hero.SetCurrentHpInternal(1200); Initialize(fresh.Book);
        Check(fresh.Hero.MaxHp == 3500 && fresh.Hero.CurrentHp == 1200 && fresh.Book.HbrBattleProgressData == receipt,
            "fresh custom initialization receipt prevents repeat scaling and healing");
        Fixture native = new(Blank<Ironclad>(), current: 50);
        Check(native.Hero.MaxHp == 8000 && native.Hero.CurrentHp == 5000, "native teammate actual fresh initialization scales maximum80 and current50 once");
        native.Hero.SetCurrentHpInternal(4900); Initialize(native.Book);
        Check(native.Hero.MaxHp == 8000 && native.Hero.CurrentHp == 4900, "native teammate repeated initialization preserves damage");
        Fixture old = new(Blank<Ironclad>(), enabled: false, current: 37);
        var restored = (ElectronicSoldierManual)RelicModel.FromSerializable(old.Book.ToSerializable());
        Set(restored, "_owner", old.Player);
        Check(!restored.IsHbrNumericsEnabled && old.Hero.MaxHp == 80 && old.Hero.CurrentHp == 37,
            "legacy missing progress native save round trip preserves disabled mode and HP");
        Fixture saved = new(); saved.Hero.SetMaxHpInternal(1750); saved.Hero.SetCurrentHpInternal(555);
        var loaded = (ElectronicSoldierManual)RelicModel.FromSerializable(saved.Book.ToSerializable()); Set(loaded, "_owner", saved.Player);
        Initialize(loaded);
        Check(loaded.IsHbrNumericsEnabled && saved.Hero.MaxHp == 1750 && saved.Hero.CurrentHp == 555,
            "existing enabled profile save round trip and repeated initializer never rescale stored50-era HP");
        Fixture hotaru = new(Blank<HbrgoHotaru>(), hotaruBook: true);
        var hotaruManual = (HotaruElectronicSoldierManual)hotaru.Book;
        Call(typeof(HotaruElectronicSoldierManual), "InitializeStartingHealth", hotaruManual);
        Equal(hotaru.Hero.CurrentHp, 1, "Hotaru actual fresh initialization sets1 currentHP");
        Equal(hotaru.Hero.MaxHp, 3000, "Hotaru currentHP exception leaves scaled maximum3000");
        var hotaruShield = hotaru.Shield();
        Check(hotaruShield.Dp == 6000 && hotaruShield.MaxDp == 6000,
            "Hotaru initial1HP retains full6000DP from maximum3000");
        hotaru.Hero.SetCurrentHpInternal(73); Call(typeof(HotaruElectronicSoldierManual), "InitializeStartingHealth", hotaruManual);
        Equal(hotaru.Hero.CurrentHp, 73, "Hotaru initial currentHP receipt is once-only");
        Fixture hotaruSaved = new(Blank<HbrgoHotaru>(), enabled: false, current: 7, hotaruBook: true);
        Call(typeof(HotaruElectronicSoldierManual), "PreserveLoadedHealth", hotaruSaved.Book);
        Call(typeof(HotaruElectronicSoldierManual), "InitializeStartingHealth", hotaruSaved.Book);
        Check(hotaruSaved.Hero.CurrentHp == 7 && ((HotaruElectronicSoldierManual)hotaruSaved.Book).StartingHealthInitialized,
            "Hotaru loaded legacy currentHP remains7 rather than resetting to1 or rescaling");
        Creature phantom = fresh.Phantom();
        Call(Helper("PhantomHelper"), "ConfigureSummonedMinionHealth", null, phantom, 1, fresh.Player);
        Equal(phantom.MaxHp, 4000, "summoned phantom uses raw80 half baseline then100 scale");
        Call(Helper("PhantomHelper"), "ConfigureSummonedMinionHealth", null, phantom, 1, fresh.Player);
        Equal(phantom.MaxHp, 4000, "repeat phantom setup reuses raw unscaled baseline");
        Equal((decimal)Call(typeof(PhantomHighDimensionalShellPower), "CalculateInitialAmount", null, phantom)!, 800,
            "phantom DP stays20percent of scaled HP");
        Creature oldPhantom = old.Phantom();
        Call(Helper("PhantomHelper"), "ConfigureSummonedMinionHealth", null, oldPhantom, 1, old.Player);
        Equal(oldPhantom.MaxHp, 40, "legacy summoned phantom stays native half baseline");
    }
    static void AncientEntryCases()
    {
        Type patch = Mod.GetType("HBRGOSakiMod.Core.Patches.HotaruAncientHealthRecoveryPatch", true)!;
        MethodInfo prefix = patch.GetMethod("Prefix", F)!;
        MethodInfo native = AccessTools.Method(typeof(AncientEventModel), "BeforeEventStarted");
        Neow AncientFor(Player player)
        {
            var ancient = Blank<Neow>(); Set(ancient, "<IsMutable>k__BackingField", true);
            PropertyInfo owner = typeof(EventModel).GetProperty("Owner", F)!;
            if (owner.SetMethod != null) owner.SetValue(ancient, player);
            else Set(ancient, "_owner", player);
            return ancient;
        }
        Fixture fresh = new(Blank<HbrgoHotaru>(), hotaruBook: true);
        Call(typeof(HotaruElectronicSoldierManual), "InitializeStartingHealth", fresh.Book);
        var shield = fresh.Shield(); Neow neow = AncientFor(fresh.Player);
        Task entry = (Task)native.Invoke(neow, [false])!; entry.GetAwaiter().GetResult();
        Check(entry.IsCompletedSuccessfully && fresh.Hero.CurrentHp == 1 && fresh.Hero.MaxHp == 3000 && shield.Dp == 6000,
            "actual patched native Neow BeforeEventStarted skips HP reset and healing, preserving1of3000 and6000DP");
        fresh.Hero.SetCurrentHpInternal(73);
        ((Task)native.Invoke(neow, [false])!).GetAwaiter().GetResult();
        Equal(fresh.Hero.CurrentHp, 73, "repeat patched ancient entry preserves later73HP without resetting or healing");
        Task original = new TaskCompletionSource().Task;
        object?[] preFinished = [neow, true, original];
        Check((bool)prefix.Invoke(null, preFinished)! && ReferenceEquals(preFinished[2], original),
            "pre-finished Hotaru ancient retains native path and original task");
        Fixture nativePlayer = new(Blank<Ironclad>(), current: 50);
        object?[] otherCharacter = [AncientFor(nativePlayer.Player), false, original];
        Check((bool)prefix.Invoke(null, otherCharacter)! && ReferenceEquals(otherCharacter[2], original),
            "non-Hotaru ancient retains native path and original task");
    }
    static void FixedCases()
    {
        Fixture enabled = new(), old = new(enabled: false);
        Type resource = Helper("HbrResourceValueHelper");
        decimal Fixed(Creature source, decimal amount) => (decimal)Call(resource, "Fixed", null, source, amount)!;
        foreach ((decimal amount, decimal expected) in new (decimal, decimal)[] { (6, 600), (-6, -600), (0, 0), (0.125m, 12.5m), (-0.125m, -12.5m), (decimal.MaxValue, Maximum), (decimal.MinValue, -Maximum) })
            Equal(Fixed(enabled.Hero, amount), expected, "fixed signed resource " + amount);
        foreach (decimal amount in new[] { 6m, -6m, 0m, 0.125m, decimal.MaxValue, decimal.MinValue })
            Equal(Fixed(old.Hero, amount), amount, "disabled mode preserves original resource " + amount);
        Equal((int)Call(resource, "FixedInt", null, enabled.Hero, decimal.MaxValue)!, int.MaxValue, "integer DP conversion saturates positive int boundary");
        Equal((int)Call(resource, "FixedInt", null, enabled.Hero, decimal.MinValue)!, int.MinValue, "integer DP conversion saturates signed negative int boundary");
        Equal((int)Call(resource, "FixedInt", null, enabled.Hero, 0.125m)!, 12, "integer DP conversion truncates fractional final amount");
        Equal((decimal)Call(resource, "ConvertFixedRecovery", null, enabled.Hero, 6m)!, 600, "fixed recovery6 becomes600 at initial INT100");
        Data(enabled.Book, "{\"Schema\":1,\"Enabled\":true,\"HealthScaleApplied\":true,\"Level\":1000}");
        Equal((decimal)Call(resource, "ConvertFixedRecovery", null, enabled.Hero, 6m)!, 600, "fixed recovery6 stays600 after INT level growth");
        typeof(ElectronicSoldierManual).GetProperty("Talent", F)!.SetValue(enabled.Book, 99);
        Equal((decimal)Call(resource, "ConvertFixedRecovery", null, enabled.Hero, 6m)!, 600, "fixed recovery ignores additional99 talent INT levels");
        Equal((decimal)Call(resource, "ConvertFixedRecovery", null, old.Hero, 6m)!, 6, "legacy fixed recovery retains6");
    }
    static void DerivedCases()
    {
        Fixture fixture = new(); var shield = fixture.Shield();
        FixedCalls = 0;
        Equal(shield.LoseDp(1000), 1000, "low-level DP loss accepts already resolved1000 units");
        Equal(shield.RestoreDp(125), 125, "low-level DP restoration accepts already resolved125 units");
        Equal(shield.RestoreDpByMaxPercent(0.1m), 700, "ten-percent DP recovery remains700");
        Equal(shield.LoseDpByCurrentPercent(0.1m), 682, "ten-percent current DP cost remains proportional");
        Equal(shield.GainMaxDp(37), 37, "low-level maxDP transfer retains actual37 units");
        Equal(shield.LoseMaxDp(12), 12, "low-level maxDP loss retains actual12 units");
        Equal(FixedCalls, 0, "derived and percentage DP ledger APIs never reenter fixed converter");
        Equal(shield.MaxDp, 7025, "DP transfers add net25 without changing HP scale");
        Equal((decimal)Call(typeof(MomentOfReign), "GetPhantomRevivalHp", null, 4000m, 0.25m)!, 1000,
            "phantom25percent revival remains1000 without fixed100 multiplier");
        Equal(FixedCalls, 0, "percentage phantom revival does not cross fixed boundary");
    }
    public static Task Run()
    {
        bool ok = false; string? error = null;
        try { Install(); InitialCases(); AncientEntryCases(); FixedCases(); DerivedCases(); PhantomGrowthCases(); ok = true; }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, cases = Passed, error,
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            limits = "Actual candidate model methods and native internal HP setters, relic clone/save serialization, stored manual progress and phantom roster/fusion metadata readers are exercised. StringName native construction, log output and relic Flash are controlled leaves. Temporary phantom growth additionally controls terminal GainMaxHp/SetMaxHp, DP refill and intent refresh leaves; production bonus ledger and expiry stay intact. Actual Harmony-guarded Hotaru AncientEventModel.BeforeEventStarted skips the native HP reset/heal and preserves current HP; direct prefix controls cover pre-finished and non-Hotaru branches. No Godot process, complete Neow event synchronizer execution, rendered selection UI, complete fusion command, full combat, real save/config IO, network, packaging or Steam upload. Legacy HP test models serialized relic progress attached to existing stored native HP; it does not load a complete gameplay save."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {Passed.Count} resource-scale checks");
        if (!ok) throw new Exception("Resource-scale checks failed; inspect report.json");
        return Task.CompletedTask;
    }
}
