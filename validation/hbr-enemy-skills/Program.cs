using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class Checks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const decimal Tolerance = 0.00000000000000000001m;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static Type PatchType(string name) => Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
    static Type Context => Helper("HbrCriticalDamageContext");
    static Type Actual => PatchType("StrongestShieldDamagePatch");
    static MethodInfo Canonical = null!, Wrapper = null!;
    static RunState RunState = null!;
    static CombatState Combat = null!;
    static Creature Hero = null!, Enemy = null!;
    static Player Player = null!;
    static ElectronicSoldierManual Manual = null!;
    static bool FailListeners, Nested, InNested;
    static int LazyEnumerations;
    static object? ParentScope;
    static readonly List<decimal> LastActual = [];
    static object? Current => Context.GetField("Current", F)!.GetValue(null)!.GetType().GetProperty("Value")!
        .GetValue(Context.GetField("Current", F)!.GetValue(null));
    static bool ActualFlag => (bool)Actual.GetProperty("IsResolvingActualDamage", F)!.GetValue(null)!;
    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static FieldInfo Field(Type type, string name)
    {
        for (Type? cursor = type; cursor != null; cursor = cursor.BaseType)
            if (cursor.GetField(name, F) is { } field) return field;
        throw new MissingFieldException(type.FullName, name);
    }
    static void Set(object owner, string name, object? value) => Field(owner.GetType(), name).SetValue(owner, value);
    static void EmptyList(object owner, string name)
    {
        FieldInfo field = Field(owner.GetType(), name);
        field.SetValue(owner, Activator.CreateInstance(field.FieldType));
    }
    static object? Call(Type type, string name, params object?[] args) => type.GetMethod(name, F)!.Invoke(null, args);
    static decimal Number(Type type, string name, params object?[] args) => (decimal)Call(type, name, args)!;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Passed.Add(label);
    }
    static void Equal(decimal actual, decimal expected, string label) => Check(Math.Abs(actual - expected) <= Tolerance, $"{label}: {actual} = {expected}");
    static void Progress(bool enabled, int level = 1) => typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!
        .SetValue(Manual, JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, Level = level }));
    static object Stats(Creature creature, out bool found)
    {
        object?[] args = [creature, null];
        found = (bool)Helper("HbrBattleStatsHelper").GetMethod("TryGetStats", F)!.Invoke(null, args)!;
        return args[1]!;
    }
    static decimal Stat(object stats, string name) => (decimal)stats.GetType().GetProperty(name, F)!.GetValue(stats)!;
    static Creature Creature(MonsterModel? monster = null, Player? player = null, Player? owner = null)
    {
        var creature = Blank<Creature>();
        Set(creature, "_powers", new List<PowerModel>()); Set(creature, "_currentHp", 1000); Set(creature, "_maxHp", 1000);
        Set(creature, "<Side>k__BackingField", player != null || owner != null ? CombatSide.Player : CombatSide.Enemy);
        Set(creature, "<Player>k__BackingField", player); Set(creature, "_petOwner", owner); creature.CombatState = Combat;
        if (monster != null)
        {
            Set(monster, "<IsMutable>k__BackingField", true); Set(monster, "_creature", creature);
            Set(monster, "<NextMove>k__BackingField", new MoveState("UNKNOWN_MOVE", _ => Task.CompletedTask));
            Set(creature, "<Monster>k__BackingField", monster);
        }
        return creature;
    }
    static void AddPower(Creature creature, PowerModel power)
    {
        Set(power, "<IsMutable>k__BackingField", true); Set(power, "_owner", creature);
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(creature)!).Add(power);
    }
    static void Move(Creature creature, string id) => Set(creature.Monster!, "<NextMove>k__BackingField", new MoveState(id, _ => Task.CompletedTask));
    static void Sense(string name, int value) => typeof(ElectronicSoldierManual).GetProperty(name, F)!.SetValue(Manual, value);
    static void ResetSenses()
    {
        foreach (string name in new[] { "Madness", "LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity" }) Sense(name, 0);
    }
    static decimal Convert(Creature dealer, Creature target, decimal damage = 10m, ValueProp props = ValueProp.Move) =>
        Number(Helper("HbrDamageSkillAdapter"), "ConvertBaseDamage", target, dealer, damage, props, null);
    static decimal Parameter(Creature creature) => Number(Helper("HbrEnemyProfileHelper"), "GetEnemyParameter", creature);
    static object Skill(Creature creature) => Call(Helper("HbrEnemySkillProfiles"), "Get", creature)!;
    static string Reference(Creature creature) => Skill(creature).GetType().GetProperty("DefenseReference", F)!.GetValue(Skill(creature))!.ToString()!;
    static decimal Preview(Creature dealer, Creature target, decimal amount = 10m, ValueProp props = ValueProp.Move) =>
        Hook.ModifyDamage(RunState, Combat, target, dealer, amount, props, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);
    static decimal ActualDamage(Creature dealer, Creature target, decimal amount = 10m, ValueProp props = ValueProp.Move)
    {
        object?[] stable = [RunState, Combat, target, dealer, amount, props, null, ModifyDamageHookType.All, CardPreviewMode.None, null];
        object?[] beta = [RunState, Combat, target, dealer, amount, props, null, null, ModifyDamageHookType.All, CardPreviewMode.None, null];
        return (decimal)Wrapper.Invoke(null, Wrapper.GetParameters().Length == 10 ? stable : beta)!;
    }
    static void Patch(Harmony harmony, MethodBase target, string method, int priority = Priority.Normal) =>
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(Checks).GetMethod(method, F)!) { priority = priority });
    static bool Skip() => false;
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    {
        if (FailListeners && ActualFlag) throw new InvalidOperationException("controlled-listener-fault");
        __result = Array.Empty<AbstractModel>(); return false;
    }
    static Task<IEnumerable<DamageResult>> Command(Creature dealer, IEnumerable<Creature>? targets = null)
    {
        object?[] args = [null, targets ?? new[] { dealer == Enemy ? Hero : Enemy }, 10m, ValueProp.Move, dealer, null, null];
        return (Task<IEnumerable<DamageResult>>)Canonical.Invoke(null, args.Take(Canonical.GetParameters().Length).ToArray())!;
    }
    static IEnumerable<Creature> LazyTargets()
    {
        LazyEnumerations++;
        yield return Enemy; yield return Enemy;
    }
    static int? Sample(object scope) => (int?)scope.GetType().GetField("Sample", F)!.GetValue(scope);
    static bool Terminal(IEnumerable<Creature> targets, Creature dealer, ref Task<IEnumerable<DamageResult>> __result)
    {
        Check(LazyEnumerations == 0, "canonical prefix never pre-enumerates target query");
        __result = Body(targets, dealer); return false;
    }
    static async Task<IEnumerable<DamageResult>> Body(IEnumerable<Creature> targets, Creature dealer)
    {
        object? scope = Current;
        if (dealer == Enemy) Check(scope == null, "enemy canonical hit creates no natural-critical scope");
        else Check(scope != null && Sample(scope) == null, "player canonical hit owns a lazy unsampled scope");
        Creature[] snapshot = targets.ToArray();
        await Task.Yield();
        Check(ReferenceEquals(Current, scope), "scope survives canonical async continuation");
        if (Nested && !InNested)
        {
            InNested = true; ParentScope = scope; await Command(Hero); InNested = false;
            Check(ReferenceEquals(Current, scope), "nested canonical command resumes original parent scope");
        }
        else if (InNested) Check(!ReferenceEquals(scope, ParentScope), "nested command owns distinct critical scope");
        foreach (Creature target in snapshot)
        {
            int before = RunState.Rng.Niche.Counter;
            decimal preview = Preview(dealer, target);
            Check(RunState.Rng.Niche.Counter == before && !ActualFlag, "preview inside command never consumes RNG or marks actual damage");
            decimal actual = ActualDamage(dealer, target);
            int? sample = scope == null ? null : Sample(scope);
            decimal repeated = ActualDamage(dealer, target);
            if (dealer == Enemy)
            {
                Equal(actual, preview, "enemy same-move preview and actual share pipeline");
                Check(RunState.Rng.Niche.Counter == before && Current == null, "enemy actual hit never samples natural critical RNG");
            }
            else Check(sample.HasValue && Sample(scope!) == sample && RunState.Rng.Niche.Counter <= before + 1,
                "player repeated actual calculation reuses one bounded critical sample");
            Equal(actual, repeated, "repeated actual hook returns same result"); LastActual.Add(actual);
        }
        return Array.Empty<DamageResult>();
    }
    public static async Task Run()
    {
        var harmony = new Harmony("hbr.enemy.skills.native.checks");
        Patch(harmony, typeof(StringName).GetConstructor([typeof(string)])!, nameof(Skip));
        Type logger = typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger", true)!;
        foreach (MethodInfo method in logger.GetMethods(F).Where(m => m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose")) Patch(harmony, method, nameof(Skip));
        Patch(harmony, typeof(RunState).GetMethod(nameof(RunState.IterateHookListeners))!, nameof(Listeners));
        RunState = Blank<RunState>(); Set(RunState, "<Rng>k__BackingField", new RunRngSet("ENEMY-SKILL-CHECKS"));
        Set(RunState, "_visitedMapCoords", new List<MapCoord> { new(0, 0) });
        EmptyList(RunState, "_currentRooms"); EmptyList(RunState, "_mapPointHistory");
        Combat = Blank<CombatState>(); Set(Combat, "<RunState>k__BackingField", RunState);
        Player = Blank<Player>(); Set(Player, "_runState", RunState);
        Hero = Creature(player: Player); Set(Player, "<Creature>k__BackingField", Hero);
        Enemy = Creature(Blank<RedCrimsonBossMonster>());
        Set(RunState, "_players", new List<Player> { Player }); Set(Combat, "_allies", new List<Creature> { Hero }); Set(Combat, "_enemies", new List<Creature> { Enemy });
        Manual = Blank<ElectronicSoldierManual>(); Set(Manual, "<IsMutable>k__BackingField", true); Set(Manual, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { Manual }); Progress(true); ResetSenses();
        foreach (string patch in new[] { "HbrgoSakiAdditiveDamagePatch", "HbrNumericBlockPatch", "HbrNumericCriticalScopePatch" })
            Check(harmony.CreateClassProcessor(PatchType(patch)).Patch().Count > 0, "production patch installs " + patch);
        Canonical = (MethodInfo)Call(PatchType("HbrNumericCriticalScopePatch"), "TargetMethod")!;
        Wrapper = Actual.GetMethods(F).Single(m => m.Name == "ModifyDamageWithStrongestShield" && m.GetParameters().Length == (Canonical.GetParameters().Length == 6 ? 10 : 11));
        new Harmony("hbr.enemy.skills.native.terminal").Patch(Canonical, prefix: new HarmonyMethod(
            typeof(Checks).GetMethod(nameof(Terminal), F)!) { priority = Priority.Last, after = [harmony.Id] });
        object enemyStats = Stats(Enemy, out bool enemyFound);
        Check(!enemyFound && Stat(enemyStats, "Strength") == 0m, "enabled enemy exposes no fabricated six-stat profile");
        object heroStats = Stats(Hero, out bool heroFound);
        Check(heroFound && Stat(heroStats, "Strength") == 200m, "real saved manual supplies player stats");
        Creature cannon = Creature(Blank<HotaruContestCannon>(), owner: Player);
        Check(Stats(cannon, out bool cannonFound).Equals(heroStats) && cannonFound, "cannon inherits exact owner stats");
        Creature phantom = Creature(Blank<TestSubject>(), owner: Player); AddPower(phantom, Blank<PhantomPower>());
        Check(Stats(phantom, out bool phantomFound).Equals(heroStats) && phantomFound, "main phantom retains owner base stats at level one");
        Creature minion = Creature(Blank<TestSubject>(), owner: Player); var phantomPower = Blank<PhantomPower>();
        Set(phantomPower, "<IsSummonedMinion>k__BackingField", true); Set(phantomPower, "<SummonedMinionLevel>k__BackingField", 7); AddPower(minion, phantomPower);
        object minionStats = Stats(minion, out bool minionFound);
        Check(minionFound && Stat(minionStats, "Strength") == 212m && Stat(minionStats, "Spirit") == 212m, "summoned phantom preserves explicit level bonus");
        Equal(Parameter(Enemy), 173m, "enemy E comes from independent map profile");
        foreach (string name in new[] { "Madness", "LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity" })
        {
            Sense(name, 75); Equal(Parameter(Enemy), 173m, name + " changes cannot alter enemy E");
            Check(!((bool)Helper("HbrBattleStatsHelper").GetMethod("TryGetStats", F)!.Invoke(null, [Enemy, null])!), name + " cannot synthesize enemy stats");
            ResetSenses();
        }
        Progress(true, 1000); Equal(Parameter(Enemy), 173m, "player level cannot alter enemy E"); Progress(true);
        Sense("Gentleness", 25);
        foreach (string move in new[] { "DISAPPEAR_RAY_MOVE", "WORLDS_END_SUPER_NOVA_1_MOVE", "WORLDS_END_SUPER_NOVA_2_MOVE", "UNKNOWN_MOVE" })
        {
            Move(Enemy, move); bool spirit = move != "UNKNOWN_MOVE";
            Check(Reference(Enemy) == (spirit ? "Spirit" : "Vitality"), move + " keeps explicit skill defense reference");
            decimal defense = spirit ? 650m / 3m : 700m / 3m;
            decimal expected = 1000m * (1m + (173m - defense) / 140m);
            Equal(Convert(Enemy, Hero), expected, move + " uses normalized fixed defense weights");
            decimal preview = Preview(Enemy, Hero); int rng = RunState.Rng.Niche.Counter;
            await Command(Enemy);
            Equal(LastActual[^1], preview, move + " intent Hook and actual wrapper agree");
            Check(RunState.Rng.Niche.Counter == rng && Current == null && !ActualFlag, move + " enemy command leaves no critical scope or RNG usage");
        }
        Move(Enemy, "UNKNOWN_MOVE"); decimal single = Convert(Enemy, Hero);
        ((List<Creature>)Field(typeof(CombatState), "_allies").GetValue(Combat)!).Add(phantom);
        Equal(Convert(Enemy, Hero), single, "unknown skill keeps vitality reference with extra live targets");
        Move(Enemy, "DISAPPEAR_RAY_MOVE"); Creature unknown = Creature(Blank<TestSubject>()); Move(unknown, "DISAPPEAR_RAY_MOVE");
        Check(Reference(unknown) == "Vitality", "another monster cannot inherit ray profile by move name alone");
        ResetSenses(); Set(RunState, "_currentActIndex", 100);
        Equal(Convert(Enemy, Hero), 2000m, "incoming enemy curve caps at configured maximum");
        Set(RunState, "_currentActIndex", 0); Sense("Gentleness", 1000); Sense("MentalStrength", 1000);
        Equal(Convert(Enemy, Hero), 1m, "incoming enemy curve preserves one-point floor"); ResetSenses();
        Equal(Convert(Enemy, Hero, props: ValueProp.Unpowered), 10m, "Unpowered incoming keeps legacy amount");
        decimal support = Number(Helper("HbrSkillPower"), "EvaluateBlockFromUnits", 3m, Parameter(Enemy), 1000m);
        Equal(Number(Helper("HbrDamageSkillAdapter"), "ConvertBlock", Enemy, 3m, ValueProp.Move), support, "enemy block uses explicit E support baseline");
        Equal(Hook.ModifyBlock(Combat, Enemy, 3m, ValueProp.Move, null, null, out _), support, "enemy block native Hook uses same E baseline");
        Progress(false);
        Equal(Convert(Enemy, Hero), 10m, "disabled numeric mode keeps old enemy damage");
        Equal(Number(Helper("HbrDamageSkillAdapter"), "ConvertBlock", Enemy, 3m, ValueProp.Move), 3m, "disabled numeric mode keeps old enemy block");
        Stats(Hero, out bool disabledStats); Check(!disabledStats, "old manual data does not create player battle stats"); Progress(true);
        LazyEnumerations = 0; await Command(Hero, LazyTargets());
        Check(LazyEnumerations == 1, "canonical target query is consumed exactly once by terminal native stand-in"); LazyEnumerations = 0;
        Check(Current == null && !ActualFlag, "player success restores critical and actual scopes");
        Nested = true; await Command(Hero); Nested = false;
        Check(Current == null && !ActualFlag, "nested player command restores scopes without leakage");
        FailListeners = true;
        try { await Command(Hero); throw new Exception("missing controlled failure"); }
        catch (Exception error) when (error.ToString().Contains("controlled-listener-fault")) { Check(true, "controlled hook failure propagates from canonical command"); }
        finally { FailListeners = false; }
        Check(Current == null && !ActualFlag, "faulted player command closes critical and actual scopes");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            passed = true, checks = Passed.Count, cases = Passed,
            modSha256 = System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            boundary = "Actual saved manual, native actor models, stat helpers, Hook.ModifyDamage/Block and full production pipeline. Actual calls use production StrongestShield wrapper; canonical Damage terminal HP mutation is replaced with an asynchronous hook driver. Hook listeners are empty controlled leaves. No rendering, full attack/resource execution, real save/config writes or Godot."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS " + Passed.Count + " enemy skill native checks");
    }
}
