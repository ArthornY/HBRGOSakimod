using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

// ★6 equipment effects through the production damage pipeline and the real
// destruction, OD and roll boundaries. The fixture follows hbr-stat-powers.
static class Checks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const decimal Epsilon = 0.00000000000000000001m;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<Creature> Actors = [];
    static readonly List<AbstractModel> ExtraListeners = [];
    static RunState RunState = null!;
    static CombatState Combat = null!;
    static Player Player = null!;
    static Creature Hero = null!, Enemy = null!;
    static ElectronicSoldierManual Manual = null!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string name, params object?[] args) => Helper(helper).GetMethod(name, F)!.Invoke(null, args);
    static object? Gear(string name, params object?[] args) => typeof(ElectronicSoldierManual).GetMethod(name, F)!.Invoke(null, args);
    static FieldInfo Field(Type type, string name)
    {
        for (Type? cursor = type; cursor != null; cursor = cursor.BaseType)
            if (cursor.GetField(name, F) is { } value) return value;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object owner, string field, object? value) => Field(owner.GetType(), field).SetValue(owner, value);
    static T Blank<T>() { var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); GC.SuppressFinalize(value!); return value; }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Passed.Add(name); }
    static void Equal(decimal actual, decimal expected, string name) => Check(Math.Abs(actual - expected) <= Epsilon, $"{name}: {actual} = {expected}");
    static List<PowerModel> Powers(Creature creature) => (List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(creature)!;
    static T Power<T>(Creature creature, int amount) where T : PowerModel
    {
        var power = Blank<T>(); Set(power, "<IsMutable>k__BackingField", true); Set(power, "_owner", creature);
        Set(power, "<Id>k__BackingField", ModelDb.GetId(typeof(T))); Set(power, "_amount", amount);
        Set(power, "_internalData", AccessTools.Method(typeof(T), "InitInternalData").Invoke(power, null));
        Powers(creature).Add(power); return power;
    }
    static Creature Actor(MonsterModel? monster = null, Player? player = null, Player? owner = null)
    {
        var actor = Blank<Creature>(); Set(actor, "_powers", new List<PowerModel>()); Set(actor, "_maxHp", 5000); Set(actor, "_currentHp", 5000);
        Set(actor, "<Side>k__BackingField", player != null || owner != null ? CombatSide.Player : CombatSide.Enemy);
        Set(actor, "<Player>k__BackingField", player); Set(actor, "_petOwner", owner); actor.CombatState = Combat;
        if (monster != null)
        {
            Set(monster, "<IsMutable>k__BackingField", true); Set(monster, "_creature", actor);
            Set(monster, "<NextMove>k__BackingField", new MoveState("UNKNOWN_MOVE", _ => Task.CompletedTask));
            Set(actor, "<Monster>k__BackingField", monster);
        }
        Actors.Add(actor); return actor;
    }
    static ElectronicSoldierManual NewManual(Player owner)
    {
        var manual = Blank<ElectronicSoldierManual>(); Set(manual, "<IsMutable>k__BackingField", true); Set(manual, "_owner", owner);
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!.SetValue(manual,
            JsonSerializer.Serialize(new { Schema = 1, Enabled = true, HealthScaleApplied = true, Level = 1 }));
        Set(owner, "_relics", new List<RelicModel> { manual });
        return manual;
    }
    static void Mode(ElectronicSoldierManual manual, bool enabled) => typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!
        .SetValue(manual, JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, HealthScaleApplied = true, Level = 1 }));

    // Worn rows as the manual saves them. Slots 1..4 are ring, earring, bracelet, necklace.
    static void Wear(ElectronicSoldierManual manual, params (int Slot, string Template, int[] Rolls)[] rows)
    {
        int[] equipped = new int[15];
        var items = rows.Select((row, index) =>
        {
            equipped[row.Slot] = index + 1;
            return new { Id = index + 1, row.Template, row.Rolls, Plus = Array.Empty<object>() };
        }).ToArray();
        string json = JsonSerializer.Serialize(new
        {
            Schema = 1, Revision = 1, NextItemId = rows.Length + 1, Seed = 1UL, RandomCounter = 0, Items = items,
            Equipped = equipped, PlusRelease = 0, PlusChange = 0, Receipts = Array.Empty<string>()
        });
        typeof(ElectronicSoldierManual).GetProperty("HbrEquipmentData", F)!.SetValue(manual, json);
        Check(manual.HbrEquipmentData == json, "saved equipment row is stored");
    }
    static void Bare(ElectronicSoldierManual manual) =>
        typeof(ElectronicSoldierManual).GetProperty("HbrEquipmentData", F)!.SetValue(manual, string.Empty);

    static decimal Preview(Creature target, Creature dealer, decimal raw = 6m, ValueProp props = ValueProp.Move, CardModel? card = null) =>
        (decimal)Mod.GetType("HBRGOSakiMod.Core.Compatibility.HookCompatibility", true)!.GetMethod("ModifyDamage", F)!.Invoke(null,
            [RunState, Combat, target, dealer, raw, props, card, ModifyDamageHookType.All, CardPreviewMode.None, null, null])!;
    static decimal Outgoing(decimal str, decimal dex, decimal enemy, bool dp = false, decimal raw = 6m)
    {
        decimal parameter = dp ? (str + dex * 2m) / 3m : (str * 2m + dex) / 3m;
        return decimal.Floor(Math.Max(1m, raw * 1000m * Math.Max(0m, 1m + (parameter - enemy) / 140m)) * 100m) / 100m;
    }
    static decimal Incoming(decimal enemy, decimal vit, decimal spirit, decimal raw = 10m) =>
        Math.Max(1m, raw * 100m * (1m + (enemy - (vit * 2m + spirit) / 3m) / 140m));
    static IDisposable Skill(Creature dealer, decimal hits) => (IDisposable)Call("HbrDestructionGainHelper", "BeginSkill", dealer, null, hits, 3m)!;
    static decimal Destruction(Creature dealer) => (decimal)Call("HbrDestructionGainHelper", "GetGain", Enemy, dealer, null, ValueProp.Move)!;
    static decimal Overdrive(Creature dealer) => (decimal)Gear("GetEquipmentOverdriveMultiplier", dealer, null)!;
    static int Roll(Player player, CardType? type) => (int)Gear("GetEquipmentRollBonus", player, type)!;

    static bool Skip() => false;
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    { __result = Actors.SelectMany(c => c.Powers).Cast<AbstractModel>().Concat(ExtraListeners).ToArray(); return false; }
    static void Setup()
    {
        TestMode.IsOn = true; var h = new Harmony("hbr.equipment.native.checks");
        h.Patch(typeof(StringName).GetConstructor([typeof(string)])!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Skip), F)!));
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        if (logger.GetMethod("GetIsRunningFromGodotEditor", F) is { } editor) h.Patch(editor, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Skip), F)!));
        foreach (var method in logger.GetMethods(F).Where(m => m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))
            h.Patch(method, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Skip), F)!));
        h.Patch(typeof(RunState).GetMethod(nameof(RunState.IterateHookListeners))!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Listeners), F)!));
        h.Patch(typeof(CombatState).GetMethod(nameof(CombatState.IterateHookListeners))!, prefix: new HarmonyMethod(typeof(Checks).GetMethod(nameof(Listeners), F)!));
        RunState = Blank<RunState>(); Set(RunState, "_visitedMapCoords", new List<MegaCrit.Sts2.Core.Map.MapCoord>());
        Set(RunState, "<ActFloor>k__BackingField", 1);
        foreach (string field in new[] { "_currentRooms", "_mapPointHistory" })
            Set(RunState, field, Activator.CreateInstance(Field(typeof(RunState), field).FieldType));
        Combat = Blank<CombatState>(); Set(Combat, "<RunState>k__BackingField", RunState);
        Player = Blank<Player>(); Set(Player, "_runState", RunState); Hero = Actor(player: Player); Set(Player, "<Creature>k__BackingField", Hero);
        Manual = NewManual(Player); ExtraListeners.Add(Manual);
        Set(RunState, "_players", new List<Player> { Player });
        Enemy = Actor(Blank<SmallHopper>()); Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
#if HBR_BETA_NATIVE
        Type turnType=typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState",true)!;
        object turn=RuntimeHelpers.GetUninitializedObject(turnType);
        Set(turn,"_cts",new CancellationTokenSource());Set(turn,"<State>k__BackingField",Combat);Set(turn,"<IsInProgress>k__BackingField",true);
        Set(turn,"<PlayersTakingExtraTurn>k__BackingField",new List<Player>());
        Set(turn,"<ReadyLock>k__BackingField",Activator.CreateInstance(typeof(System.Threading.Lock)));
        Set(turn,"<PlayersReadyToEndTurn>k__BackingField",new HashSet<Player>());
        Set(turn,"<PlayersReadyToBeginEnemyTurn>k__BackingField",new HashSet<Player>());
        Set(CombatManager.Instance,"_turnState",turn);
        object tracker=typeof(CombatManager).GetProperty("StateTracker",F)!.GetValue(CombatManager.Instance)!;
        Set(tracker,"_state",Combat);
#else
        Set(CombatManager.Instance, "_state", Combat); Set(CombatManager.Instance, "<IsInProgress>k__BackingField", true);
#endif
        foreach (var row in new[] { ("_enemyDamagePercent", 100), ("_enemyDamageGrowthPerActPercent", 0) })
            typeof(HBRGOSakiConfig).GetField(row.Item1, F)!.SetValue(null, row.Item2);
        foreach (string name in new[] { "HbrgoSakiAdditiveDamagePatch", "HbrNumericBlockPatch", "HotaruCannonHappinessStrengthPatch",
            "HbrStrengthAttributePatch", "HbrDexterityAttributePatch", "HbrAbsoluteBaseDamageHookPatch" })
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            Check(h.CreateClassProcessor(type).Patch().Count > 0, "installs production " + name);
        }
    }

    static void NoGear()
    {
        Bare(Manual);
        decimal hp = Preview(Enemy, Hero);
        Equal(hp, decimal.Floor(Outgoing(100m, 100m, 150m)), "bare manual: outgoing damage unchanged");
        Equal(Preview(Hero, Enemy, 10m), decimal.Floor(Incoming(150m, 100m, 100m)), "bare manual: incoming damage unchanged");
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, Hero, null), 1m, "bare manual adds no outgoing factor");
        Equal(Manual.ModifyDamageMultiplicative(Hero, 1m, ValueProp.Move, Enemy, null), 1m, "bare manual adds no incoming factor");
        Equal(Destruction(Hero), 6m, "bare manual: destruction gain 2 × 3");
        Equal(Overdrive(Hero), 1m, "bare manual: OD factor 1");
        Check(Roll(Player, CardType.Attack) == 0, "bare manual: no roll bonus");
    }

    static void Outgoing()
    {
        decimal unit = Outgoing(100m, 100m, 150m);
        // 攻击耳坠 top grade with its lowest DP/智慧 rows (DP and 智慧 do not change attack damage),
        // plus 火耀远古项链: +15% HP skill attack at one hit and +10% skill attack, added in one bucket.
        Wear(Manual, (2, "Acc.Earring006.F002", [2, 0, 0]), (4, "Acc.Necklace006.F009", []));
        Equal(Preview(Enemy, Hero), decimal.Floor(unit * 1.25m), "HP earring and chain add 25% to an HP hit");
        Equal(Preview(Enemy, Hero, props: ValueProp.Unpowered), 6m, "unpowered damage takes no gear effect");
        using (Skill(Hero, 10m))
            Equal(Preview(Enemy, Hero), decimal.Floor(unit * 1.15m), "at ten hits the HP earring falls to 5%");
        using (Skill(Hero, 4m))
            Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, Hero, null), 1m + (10m + 15m - 10m * 3m / 9m) / 100m,
                "at four hits the earring is 15 − 10 × 3 / 9");
        var shell = Power<HighDimensionalShellPower>(Enemy, 5000);
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, Hero, null), 1.10m, "an HP earring skips a target with DP");
        Wear(Manual, (2, "Acc.Earring006.F001", [2, 0, 0]), (4, "Acc.Necklace006.F009", []));
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, Hero, null), 1.25m, "a Break earring adds to a DP hit");
        decimal shielded = Preview(Enemy, Hero);
        Wear(Manual, (4, "Acc.Necklace006.F009", []));
        decimal chainOnly = Preview(Enemy, Hero);
        Check(Math.Abs(shielded * 1.10m - chainOnly * 1.25m) <= 1.25m + 1.10m, $"DP-hit pipeline ratio 1.25 / 1.10: {shielded} vs {chainOnly}");
        Wear(Manual, (2, "Acc.Earring006.F001", [2, 0, 0]), (4, "Acc.Necklace006.F009", []));
        Powers(Enemy).Remove(shell);
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, Hero, null), 1.10m, "a Break earring skips an HP hit");

        // Cannons share the owner's gear; phantoms keep out of summoner relic modifiers.
        var cannon = Actor(Blank<SmallHopper>(), owner: Player);
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, cannon, null), 1.10m, "a cannon uses its owner's chain");
        var phantom = Actor(Blank<SmallHopper>(), owner: Player); Power<PhantomPower>(phantom, 1);
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, phantom, null), 1m, "a phantom does not use the summoner's chain");

        // Another player's gear applies to that player only.
        var other = Blank<Player>(); Set(other, "_runState", RunState); var otherHero = Actor(player: other); Set(other, "<Creature>k__BackingField", otherHero);
        var otherManual = NewManual(other); ExtraListeners.Add(otherManual);
        Set(RunState, "_players", new List<Player> { Player, other });
        Bare(Manual); Wear(otherManual, (4, "Acc.Necklace006.F009", []));
        Equal(Preview(Enemy, Hero), decimal.Floor(unit), "another player's chain leaves this player's hit alone");
        Equal(Preview(Enemy, otherHero), decimal.Floor(unit * 1.10m), "the other player's own hit uses their chain");
        ExtraListeners.Remove(otherManual); Set(RunState, "_players", new List<Player> { Player });
        Actors.Remove(otherHero); Actors.Remove(cannon); Actors.Remove(phantom);
    }

    static void Incoming()
    {
        decimal unit = Incoming(150m, 100m, 100m);
        // 火套远古项链: DP +300 and damage taken −10%.
        Wear(Manual, (4, "Acc.Necklace006.F010", []));
        Equal(Preview(Hero, Enemy, 10m), decimal.Floor(unit * 0.9m), "defensive chain cuts an enemy hit by 10%");
        Equal(Manual.ModifyDamageMultiplicative(Enemy, 1m, ValueProp.Move, Hero, null), 1m, "defensive chain adds nothing to outgoing hits");
        // 火门手环Ⅲ大幅: −15% from bosses only.
        Wear(Manual, (3, "Acc.Bracelet016c", []), (4, "Acc.Necklace006.F010", []));
        Equal(Manual.ModifyDamageMultiplicative(Hero, 1m, ValueProp.Move, Enemy, null), 0.9m, "gate bracelet is idle outside a boss fight");
        FieldInfo encounter = typeof(CombatState).GetFields(F).Single(field => typeof(EncounterModel).IsAssignableFrom(field.FieldType));
        encounter.SetValue(Combat, Blank<TezukaSakiBoss>());
        try
        {
            Equal(Manual.ModifyDamageMultiplicative(Hero, 1m, ValueProp.Move, Enemy, null), 0.75m, "boss fight: gate 15% and chain 10% add up");
            Equal(Manual.ModifyDamageMultiplicative(Hero, 1m, ValueProp.Unpowered, Enemy, null), 1m, "unpowered boss damage keeps its value");
        }
        finally { encounter.SetValue(Combat, null); }
    }

    static void Gauges()
    {
        // 破坏耳坠 top grade and 火耀远古项链: blast 5%→15% by hits, plus a flat 10%.
        Wear(Manual, (2, "Acc.Earring006.F004", [2, 0, 0]), (4, "Acc.Necklace006.F009", []));
        Equal(Destruction(Hero), 6m * 1.15m, "one hit: blast 5% and chain 10%");
        using (Skill(Hero, 10m)) Equal(Destruction(Hero), 0.6m * 1.25m, "ten hits: blast 15% and chain 10% on the first share");
        Bare(Manual);
        using (Skill(Hero, 10m)) Equal(Destruction(Hero), 0.6m, "bare ten-hit share is 3 / 10 × 2");
        // 驱动耳坠 top grade: 5% at one hit, 15% at ten.
        Wear(Manual, (2, "Acc.Earring006.F003", [2, 0, 0]));
        Equal(Overdrive(Hero), 1.05m, "one hit: OD +5%");
        using (Skill(Hero, 10m)) Equal(Overdrive(Hero), 1.15m, "ten hits: OD +15%");
        using (Skill(Hero, 25m)) Equal(Overdrive(Hero), 1.15m, "hits above ten keep the top value");
        var cannon = Actor(Blank<SmallHopper>(), owner: Player);
        var phantom = Actor(Blank<SmallHopper>(), owner: Player); Power<PhantomPower>(phantom, 1);
        Equal(Overdrive(cannon), 1.05m, "a cannon's OD uses its owner's earring");
        Equal(Overdrive(phantom), 1m, "a phantom's OD does not");
        Equal(Overdrive(Enemy), 1m, "enemies have no gear");
        Actors.Remove(cannon); Actors.Remove(phantom);
    }

    static void Rolls()
    {
        Wear(Manual, (1, "Acc.Ring006a_04", []));
        Check(Roll(Player, CardType.Attack) == 20, "炎之戒指 10% → attack rolls +20");
        Check(Roll(Player, CardType.Skill) == 0 && Roll(Player, CardType.Power) == 0 && Roll(Player, null) == 0, "fire favours attacks only");
        Wear(Manual, (1, "Acc.Ring006b_00", []));
        Check(Roll(Player, CardType.Skill) == 10 && Roll(Player, CardType.Attack) == 0, "冰之戒指 4% → skill rolls +10");
        Wear(Manual, (1, "Acc.Ring006e_02", []));
        Check(Roll(Player, CardType.Power) == 13, "暗之戒指 6% → power rolls +13");
        Mode(Manual, false);
        Check(Roll(Player, CardType.Power) == 0, "gear is inert outside HBR runs");
        Mode(Manual, true);
        Bare(Manual);
    }

    public static Task Run()
    {
        bool ok = false; string? error = null;
        try { Setup(); NoGear(); Outgoing(); Incoming(); Gauges(); Rolls(); ok = true; }
        catch (Exception failure) { error = failure.ToString(); Console.Error.WriteLine(failure); }
        string report = System.Environment.GetEnvironmentVariable("HBR_EQUIPMENT_REPORT_PATH") ?? Path.Combine(AppContext.BaseDirectory, "report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(new
        {
            ok, checks = Passed.Count, passed = Passed, error,
            modSha256 = System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256 = System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile = System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary = "Saved manual equipment rows, the production HookCompatibility damage pipeline with its Harmony adapters, the manual's damage hook, HbrDestructionGainHelper skill scopes and GetGain, and the manual's OD and roll helpers. StringName/logging and hook-listener enumeration are isolated leaves. Not covered: the turn-start energy and the combat-start Perfected Strike transform (both need a full combat command lifecycle), Godot nodes, rewards, multiplayer sync, a game process, package or upload."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine((ok ? "PASS " : "FAIL ") + Passed.Count + " equipment native checks");
        if (!ok) throw new Exception("Equipment native checks failed; inspect report.json");
        return Task.CompletedTask;
    }
}
