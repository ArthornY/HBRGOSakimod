using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

// Act rosters: the monsters and encounters added on 2026-11-05 for acts one,
// two and three, their registries and native pool patches, move bodies,
// HBR/legacy debuff boundaries, phantom side effects, fusion effects (including
// the phantom segment builder), localization keys and art files.
static partial class ActRosterChecks
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Assembly Mod = typeof(HbrgoStaticMonster).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> Cases = [];
    static readonly List<(Type Monster, int Damage, int Hits)> Attacks = [];
    static readonly List<(Creature Target, decimal Amount, ValueProp Props)> Blocks = [];
    static readonly List<(Creature[] Targets, decimal Amount, ValueProp Props, Creature Dealer)> Extra = [];
    static int Ascension;
    // Stands in for the active Moment of Reign roster entry's fusion data.
    static string FusionData = "";

    static Type Fixture => typeof(Checks);
    static T State<T>(string name) => (T)Fixture.GetField(name, F)!.GetValue(null)!;
    static Creature Hero => State<Creature>("Hero");
    static CombatState Combat => State<CombatState>("Combat");
    static object? Invoke(MethodInfo method, object? owner, params object?[] args)
    {
        try { return method.Invoke(owner, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static object? FixtureCall(string method, params object?[] args) => Invoke(Fixture.GetMethod(method, F)!, null, args);
    static void Set(object owner, string field, object? value) => FixtureCall("Set", owner, field, value);
    static T Blank<T>() => (T)Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(typeof(T)), null)!;
    static object Blank(Type type) => Invoke(Fixture.GetMethod("Blank", F)!.MakeGenericMethod(type), null)!;
    static T Power<T>(Creature owner, int amount) where T : PowerModel => (T)Invoke(Fixture.GetMethod("Power", F)!.MakeGenericMethod(typeof(T)), null, owner, amount)!;
    static List<PowerModel> Powers(Creature target) => (List<PowerModel>)FixtureCall("Powers", target)!;
    static Type ModType(string name) => Mod.GetType(name, true)!;
    // Overloads differ by arity or by a string versus dictionary first argument.
    static object? Static(string type, string method, params object?[] args) =>
        Invoke(ModType(type).GetMethods(F).Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
            m.GetParameters().Zip(args).All(pair => pair.Second == null || pair.First.ParameterType.IsInstanceOfType(pair.Second))), null, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Passed.Add(message); }
    static void Equal(decimal actual, decimal expected, string message) => Check(actual == expected, $"{message}: {actual} = {expected}");
    static string RepositoryRoot
    {
        get
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "HBRGOSakiMod.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Fixture must remain below its checkout");
        }
    }

    // ---- installation ------------------------------------------------------

    static bool AttackLeaf(object __instance, int damage, int hitCount, ref Task __result)
    { Attacks.Add((__instance.GetType(), damage, hitCount)); __result = Task.CompletedTask; return false; }
    static bool AnimationLeaf(ref Task __result) { __result = Task.CompletedTask; return false; }
    static bool BlockLeaf(object?[] __args, ref Task<decimal> __result)
    { Blocks.Add(((Creature)__args[0]!, (decimal)__args[1]!, (ValueProp)__args[2]!)); __result = Task.FromResult(0m); return false; }
    static bool ExtraLeaf(object?[] __args, ref Task<IEnumerable<DamageResult>> __result)
    {
        Extra.Add((((IEnumerable<Creature>)__args[1]!).ToArray(), (decimal)__args[2]!, (ValueProp)__args[3]!, (Creature)__args[4]!));
        __result = Task.FromResult<IEnumerable<DamageResult>>(Array.Empty<DamageResult>());
        return false;
    }
    static bool AscensionLeaf(AscensionLevel level, int ascensionValue, int fallbackValue, ref int __result)
    { __result = Ascension >= (int)level ? ascensionValue : fallbackValue; return false; }
    static bool FusionDataLeaf(ref string __result) { __result = FusionData; return false; }
    static bool Skip() => false;

    static void Inject(Type type)
    {
        ModelDb.Inject(type);
        Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(type)), "<Id>k__BackingField", ModelDb.GetId(type));
    }

    static void Install()
    {
        FixtureCall("Setup");
        var harmony = new Harmony("hbr.act-rosters.checks");
        // Production HBR attack/defense-down application boundaries, as in
        // validation/hbr-separated-debuffs.
        foreach (Type type in Mod.GetTypes().Where(t => t.IsDefined(typeof(HarmonyPatch), false) &&
            t.Name.StartsWith("HbrPercentDebuff", StringComparison.Ordinal)))
            Check(harmony.CreateClassProcessor(type).Patch().Count > 0, "installs " + type.Name);
        harmony.Patch(AccessTools.Method(typeof(HbrgoStaticMonster), "PerformAttack"),
            prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(AttackLeaf), F)!));
        harmony.Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim)),
            prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(AnimationLeaf), F)!));
        harmony.Patch(AccessTools.Method(typeof(PowerModel), "Flash"),
            prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(Skip), F)!));
        harmony.Patch(typeof(AscensionHelper).GetMethod(nameof(AscensionHelper.GetValueIfAscension),
            [typeof(AscensionLevel), typeof(int), typeof(int)])!,
            prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(AscensionLeaf), F)!));
        MethodInfo block = typeof(CreatureCmd).GetMethods(F).Single(m => m.Name == nameof(CreatureCmd.GainBlock) &&
            m.GetParameters() is var p && p.Length == 5 && p[1].ParameterType == typeof(decimal));
        harmony.Patch(block, prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(BlockLeaf), F)!));
        MethodInfo extra = ModType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility").GetMethods(F)
            .Single(m => m.Name == "Damage" && m.GetParameters() is var p && p.Length == 5 &&
                p[1].ParameterType == typeof(IEnumerable<Creature>));
        harmony.Patch(extra, prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(ExtraLeaf), F)!));
        // Only the roster lookup is replaced; the segment builder itself runs.
        harmony.Patch(AccessTools.Method(ModType("HBRGOSakiMod.Core.Helpers.PhantomHelper"), "GetFusionEffectData"),
            prefix: new HarmonyMethod(typeof(ActRosterChecks).GetMethod(nameof(FusionDataLeaf), F)!));
        Type ledger = ModType("HBRGOSakiMod.Core.Patches.Combat.HighDimensionalShellNativeAmountPatch");
        Check(harmony.CreateClassProcessor(ledger).Patch().Count == 1, "installs production exact-DP SetAmount bridge");
        // The production act pool patches for acts one to three, on the real ActModel getters.
        foreach (ActSpec act in Acts)
        foreach (string pool in PoolPatches)
            Check(harmony.CreateClassProcessor(ModType($"HBRGOSakiMod.Core.Patches.HbrgoAct{act.Word}{pool}PoolPatch")).Patch().Count == 1,
                $"installs HbrgoAct{act.Word}{pool}PoolPatch");
        foreach (var row in new[] { ("_enemyHighDimensionalShellPercent", 20), ("_enemyDpGrowthPerActPercent", 0),
            ("_enemyMaxHpGrowthPerActPercent", 0) })
            typeof(HBRGOSakiConfig).GetField(row.Item1, F)!.SetValue(null, row.Item2);

        foreach (Type type in new[] { typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower),
            typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower) })
            Inject(type);
#if HBR_BETA_NATIVE
        typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Modding.AssemblyInfo", true)!.GetMethod("Init", F)!.Invoke(null, null);
        typeof(ModelIdSerializationCache).GetMethod("Init", F)!.Invoke(null, null);
        foreach (Type type in new[] { typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower), typeof(CrestHopper) })
            typeof(ModelIdSerializationCache).GetMethod("CacheSavedPropertiesForTypeDebug", F)!.Invoke(null, [type]);
#else
        foreach (Type type in new[] { typeof(HbrAttackDownPower), typeof(HbrDefenseDownPower), typeof(CrestHopper) })
            SavedPropertiesTypeCache.InjectTypeIntoCache(type);
#endif
        // Canonical monster and encounter models for ModelDb.Monster<T>() and
        // the act registries. Construction runs their real constructors.
        Type[] needed = Monsters.Select(m => m.Type)
            .Concat([typeof(SmallHopper), typeof(CrestHopper), typeof(HealHopper), typeof(GoldHopper),
                typeof(Knocker), typeof(ClayKnocker), typeof(AbyssKnocker), typeof(TezukaSaki),
                typeof(DeathSlug), typeof(RedCrimsonBossMonster)])
            .Concat(Mod.GetTypes().Where(t => !t.IsAbstract && t.Namespace == "HBRGOSakiMod.Core.Models.Encounters" &&
                typeof(EncounterModel).IsAssignableFrom(t)))
            .Distinct().ToArray();
        foreach (Type type in needed) Inject(type);
        Check(needed.All(ModelDb.Contains), $"injects {needed.Length} canonical roster monster and encounter models");
    }

    static readonly string[] PoolPatches = ["Encounter", "WeakEncounter", "RegularEncounter", "EliteEncounter", "BossEncounter", "Monster"];

    static IReadOnlyList<EncounterModel> RegistryEncounters(ActSpec act) =>
        (IReadOnlyList<EncounterModel>)Static($"HBRGOSakiMod.Core.Models.Encounters.HbrgoAct{act.Word}EncounterRegistry", "GetAll")!;

    // ---- structural checks ------------------------------------------------

    static MonsterModel Mutable(Type type)
    {
        var monster = (MonsterModel)Blank(type);
        Set(monster, "<IsMutable>k__BackingField", true);
        return monster;
    }

    static MonsterMoveStateMachine Machine(MonsterModel monster) =>
        (MonsterMoveStateMachine)Invoke(monster.GetType().GetMethod("GenerateMoveStateMachine", F)!, monster)!;

    static MoveState[] CycleOrder(MonsterMoveStateMachine machine)
    {
        var initial = (MonsterState)typeof(MonsterMoveStateMachine).GetField("_initialState", F)!.GetValue(machine)!;
        var order = new List<MoveState>();
        MonsterState? current = initial;
        while (current is MoveState move && !order.Contains(move))
        {
            order.Add(move);
            current = move.FollowUpState ?? (move.FollowUpStateId is { } id && machine.States.TryGetValue(id, out var next) ? next : null);
        }
        return order.ToArray();
    }

    static void MonsterStructureCases()
    {
        Check(Monsters.Select(m => m.Type).Distinct().Count() == Monsters.Length, "roster lists each monster once");
        foreach (ActSpec act in Acts)
            Check(Monsters.Any(m => m.Act == act.Act), $"act {act.Act} adds monsters");
        foreach (MonsterSpec spec in Monsters)
        {
            foreach (int ascension in new[] { 0, 10 })
            {
                Ascension = ascension;
                MonsterModel monster = Mutable(spec.Type);
                Equal(monster.MinInitialHp, ascension == 0 ? spec.Min0 : spec.Min10, $"{spec.Type.Name} asc{ascension} min native HP");
                Equal(monster.MaxInitialHp, ascension == 0 ? spec.Max0 : spec.Max10, $"{spec.Type.Name} asc{ascension} max native HP");
                MoveState[] order = CycleOrder(Machine(monster));
                Check(order.Select(m => m.Id).SequenceEqual(spec.Moves.Select(m => m.Id)),
                    $"{spec.Type.Name} asc{ascension} cycles {string.Join(" -> ", spec.Moves.Select(m => m.Id))}");
                MoveState last = order[^1];
                Check(ReferenceEquals(last.FollowUpState, order[0]) || last.FollowUpStateId == order[0].Id,
                    $"{spec.Type.Name} asc{ascension} loops back to its first move");
            }
            Check(typeof(HbrgoStaticMonster).IsAssignableFrom(spec.Type), spec.Type.Name + " is a static HBR monster (DP shell, death fade)");
        }
        // The swarm flag only lowers HP for the three-hopper encounter.
        foreach (int ascension in new[] { 0, 10 })
        {
            Ascension = ascension;
            var plain = (CrestHopper)Mutable(typeof(CrestHopper));
            var swarm = (CrestHopper)Mutable(typeof(CrestHopper));
            typeof(CrestHopper).GetProperty(nameof(CrestHopper.IsSwarmMember), F)!.GetSetMethod(true)!.Invoke(swarm, [true]);
            Equal(plain.MinInitialHp, ascension == 0 ? 46 : 48, $"CrestHopper asc{ascension} keeps its ordinary min HP");
            Equal(plain.MaxInitialHp, ascension == 0 ? 50 : 52, $"CrestHopper asc{ascension} keeps its ordinary max HP");
            Equal(swarm.MinInitialHp, ascension == 0 ? 26 : 28, $"CrestHopper swarm asc{ascension} min HP");
            Equal(swarm.MaxInitialHp, ascension == 0 ? 30 : 32, $"CrestHopper swarm asc{ascension} max HP");
        }
        Ascension = 0;
    }

    static void EncounterCases()
    {
        var owner = new Dictionary<Type, int>();
        foreach (ActSpec act in Acts)
        {
            IReadOnlyList<EncounterModel> registry = RegistryEncounters(act);
            string name = $"act-{act.Word.ToLowerInvariant()}";
            Check(registry.Select(e => e.Id).Distinct().Count() == registry.Count, $"{name} registry has no duplicate encounters");
            int weak = registry.Count(e => e.RoomType == RoomType.Monster && e.IsWeak);
            int normal = registry.Count(e => e.RoomType == RoomType.Monster && !e.IsWeak);
            int elite = registry.Count(e => e.RoomType == RoomType.Elite);
            int boss = registry.Count(e => e.RoomType == RoomType.Boss);
            string native = act.NativeAct.Name;
            Equal(weak, act.Weak, $"{name} mod weak pool matches native {native}'s {act.Weak}");
            Equal(normal, act.Normal, $"{name} mod normal pool matches native {native}'s {act.Normal}");
            Equal(elite, act.Elite, $"{name} mod elite pool matches native {native}'s {act.Elite}");
            Equal(boss, act.Boss, $"{name} mod boss pool matches native {native}'s {act.Boss}");
            Equal(registry.Count, act.Weak + act.Normal + act.Elite + act.Boss, $"{name} registry holds only pool-sized rooms");
            Check(registry.All(e => e is IHbrgoEncounter), $"every {name} registry entry is an HBR encounter");
            foreach (EncounterModel encounter in registry)
                Check(owner.TryAdd(encounter.GetType(), act.Act), $"{encounter.GetType().Name} is registered for act {act.Act} only");

            foreach (EncounterSpec spec in Encounters.Where(e => e.Act == act.Act))
            {
                Check(registry.Any(e => e.GetType() == spec.Type), $"{spec.Type.Name} is registered for act {act.Act}");
                EncounterModel canonical = registry.Single(e => e.GetType() == spec.Type);
                Check(canonical.RoomType == spec.Room && canonical.IsWeak == spec.Weak,
                    $"{spec.Type.Name} is a {(spec.Weak ? "weak " : "")}{spec.Room} encounter");
                Ascension = 0;
                EncounterModel encounter = (EncounterModel)Blank(spec.Type);
                Set(encounter, "<IsMutable>k__BackingField", true);
                var lineup = ((IEnumerable<(MonsterModel, string)>)Invoke(spec.Type.GetMethod("GenerateMonsters", F)!, encounter)!)
                    .Select(row => row.Item1).ToArray();
                Check(lineup.Select(m => m.GetType()).SequenceEqual(spec.Lineup),
                    $"{spec.Type.Name} lineup is {string.Join(" + ", spec.Lineup.Select(t => t.Name))}");
                Check(lineup.All(m => m.IsMutable), spec.Type.Name + " generates mutable monster copies");
                Check(lineup.Select(m => m.GetType()).Distinct().All(t => canonical.AllPossibleMonsters.Any(p => p.GetType() == t)),
                    spec.Type.Name + " lists every generated monster in AllPossibleMonsters");
                Check(canonical.AllPossibleMonsters.All(p => spec.Lineup.Contains(p.GetType())),
                    spec.Type.Name + " lists no monster it never generates");
                if (spec.Type == typeof(CrestHopperSwarmNormal))
                    Check(lineup.Cast<CrestHopper>().All(h => h.IsSwarmMember), "the CrestHopper trio uses the swarm HP range");
                int low = lineup.Sum(m => m.MinInitialHp), high = lineup.Sum(m => m.MaxInitialHp);
                Check(low >= spec.Low && high <= spec.High, $"{spec.Type.Name} native HP {low}..{high} within {spec.Low}..{spec.High}");
                Cases.Add(new { act = spec.Act, encounter = spec.Type.Name, room = spec.Room.ToString(), weak = spec.Weak,
                    lineup = spec.Lineup.Select(t => t.Name), nativeHp = new[] { low, high } });
                if (spec.Room == RoomType.Boss)
                {
                    string node = canonical.BossNodePath.Replace("res://", "");
                    foreach (string suffix in new[] { "", "_outline" })
                        Check(File.Exists(Path.Combine(RepositoryRoot, node + suffix + ".png")), $"{spec.Type.Name} boss node {node}{suffix}.png exists");
                }
            }
        }
        // Every new monster appears in an encounter of its own act.
        foreach (MonsterSpec spec in Monsters)
            Check(Encounters.Any(e => e.Act == spec.Act && e.Lineup.Contains(spec.Type)), $"{spec.Type.Name} fights in an act {spec.Act} encounter");
    }

    // The production pool patches append exactly their own act's registry to a
    // native act's getters. The native lists are emptied so the result is the
    // mod contribution alone; other acts' patches must leave it untouched.
    static void PoolCases()
    {
        foreach (ActSpec act in Acts)
        {
            IReadOnlyList<EncounterModel> registry = RegistryEncounters(act);
            var expected = new (string Getter, IEnumerable<string> Ids)[]
            {
                (nameof(ActModel.AllEncounters), registry.Select(e => e.Id.ToString())),
                (nameof(ActModel.AllWeakEncounters), registry.Where(e => e.RoomType == RoomType.Monster && e.IsWeak).Select(e => e.Id.ToString())),
                (nameof(ActModel.AllRegularEncounters), registry.Where(e => e.RoomType == RoomType.Monster && !e.IsWeak).Select(e => e.Id.ToString())),
                (nameof(ActModel.AllEliteEncounters), registry.Where(e => e.RoomType == RoomType.Elite).Select(e => e.Id.ToString())),
                (nameof(ActModel.AllBossEncounters), registry.Where(e => e.RoomType == RoomType.Boss).Select(e => e.Id.ToString())),
                (nameof(ActModel.AllMonsters), registry.SelectMany(e => e.AllPossibleMonsters).Select(m => m.Id.ToString()).Distinct()),
            };
            foreach ((string getter, IEnumerable<string> ids) in expected)
            {
                var model = (ActModel)Blank(act.NativeAct);
                Set(model, "_allEncounters", new List<EncounterModel>());
                Check(model.Index == act.Act - 1, $"{act.NativeAct.Name} is native act index {act.Act - 1}");
                var actual = ((System.Collections.IEnumerable)typeof(ActModel).GetProperty(getter)!.GetValue(model)!)
                    .Cast<AbstractModel>().Select(m => m.Id.ToString()).ToArray();
                Check(actual.SequenceEqual(ids), $"{act.NativeAct.Name}.{getter} gains exactly the act {act.Act} roster ({actual.Length})");
                Cases.Add(new { pool = act.NativeAct.Name, getter, ids = actual });
            }
        }
    }

    // ---- move bodies -------------------------------------------------------

    static Creature Actor(Type type, int hp = 1_000_000)
    {
        var actor = (Creature)FixtureCall("Actor", Blank(type), null, null)!;
        Set(actor, "_maxHp", hp); Set(actor, "_currentHp", hp);
        ((List<Creature>)AccessTools.Field(typeof(CombatState), "_enemies").GetValue(Combat)!).Add(actor);
        return actor;
    }

    static HighDimensionalShellPower Shell(Creature actor, decimal initial)
    {
        var shell = Power<HighDimensionalShellPower>(actor, 1);
        FieldInfo queue = typeof(HighDimensionalShellPower).GetField("_dpChanges", F)!;
        Set(shell, "_dpChanges", Activator.CreateInstance(queue.FieldType));
        Set(shell, "_pendingExtraDpDamage", new Stack<decimal>());
        typeof(HighDimensionalShellPower).GetProperty(nameof(HighDimensionalShellPower.CurrentDpData))!
            .SetValue(shell, initial.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return shell;
    }

    static void Reset(bool hbr, int ascension)
    {
        FixtureCall("Reset"); FixtureCall("Mode", hbr, 1);
        Ascension = ascension;
        Set(State<object>("RunState"), "<AscensionLevel>k__BackingField", ascension);
        Set(Combat, "_enemies", new List<Creature>());
        Attacks.Clear(); Blocks.Clear(); Extra.Clear();
        FusionData = "";
    }

    static int Modifier(PowerModel power) =>
        (int)Static("HBRGOSakiMod.Core.Helpers.HbrPercentDebuffHelper", "GetModifierAmount", power)!;

    static void ExpectDebuff(bool hbr, string kind, int raw, string label)
    {
        PowerModel[] native = Hero.Powers.Where(p => p is WeakPower or VulnerablePower).ToArray();
        PowerModel[] hbrPowers = Hero.Powers.Where(p => p is HbrAttackDownPower or HbrDefenseDownPower).ToArray();
        if (kind == "")
        {
            Check(native.Length == 0 && hbrPowers.Length == 0, label + " applies no debuff");
            return;
        }
        if (hbr)
        {
            Type expected = kind == "AttackDown" ? typeof(HbrAttackDownPower) : typeof(HbrDefenseDownPower);
            PowerModel power = hbrPowers.Single();
            Check(power.GetType() == expected && native.Length == 0, $"{label} applies HBR {kind} through the mod adapter");
            Equal(Modifier(power), raw, label + " HBR modifier strength");
        }
        else
        {
            Type expected = kind == "AttackDown" ? typeof(WeakPower) : typeof(VulnerablePower);
            PowerModel power = native.Single();
            Check(power.GetType() == expected && hbrPowers.Length == 0, $"{label} applies native {expected.Name} in legacy mode");
            Equal(power.Amount, raw, label + " legacy stacks");
        }
    }

    static void MoveCases()
    {
        foreach (MonsterSpec spec in Monsters)
        foreach (bool hbr in new[] { true, false })
        foreach (int ascension in new[] { 0, 10 })
        foreach (MoveSpec move in spec.Moves)
        foreach (bool heroDp in move.DpBonus0 > 0 ? new[] { true, false } : new[] { false })
        {
            Reset(hbr, ascension);
            string label = $"HBR{hbr} asc{ascension} {spec.Type.Name}.{move.Id}{(move.DpBonus0 > 0 ? heroDp ? " vs DP" : " vs no DP" : "")}";
            Creature actor = Actor(spec.Type);
            HighDimensionalShellPower? shell = move.Dp > 0 ? Shell(actor, 100m) : null;
            if (heroDp) Shell(Hero, 100m);
            MoveState state = CycleOrder(Machine(actor.Monster!)).Single(m => m.Id == move.Id);
            state.PerformMove(new[] { Hero }).GetAwaiter().GetResult();

            int damage = ascension == 0 ? move.Damage0 : move.Damage10;
            int hits = ascension == 0 ? move.Hits0 : move.Hits10;
            if (damage > 0)
                Check(Attacks.Count == 1 && Attacks[0] == (spec.Type, damage, hits), $"{label} attacks {damage} x{hits}");
            else
                Check(Attacks.Count == 0, label + " does not attack");
            int blockAmount = ascension == 0 ? move.Block0 : move.Block10;
            if (blockAmount > 0)
                Check(Blocks.Count == 1 && ReferenceEquals(Blocks[0].Target, actor) && Blocks[0].Amount == blockAmount &&
                    Blocks[0].Props == ValueProp.Move, $"{label} gains raw {blockAmount} Move block for its own block pipeline");
            else
                Check(Blocks.Count == 0, label + " gains no block");
            Equal(actor.GetPowerAmount<StrengthPower>(), move.SelfStrength, label + " self Strength");
            ExpectDebuff(hbr, move.Debuff, move.DebuffRaw, label);
            if (shell != null)
                Equal(shell.CurrentDp, 100m + (hbr ? move.Dp * 100m : move.Dp), label + " restores fixed DP through the HBR resource boundary");
            int bonus = ascension == 0 ? move.DpBonus0 : move.DpBonus10;
            if (bonus > 0 && heroDp)
                Check(Extra.Count == 1 && Extra[0].Amount == bonus && Extra[0].Targets.SequenceEqual([Hero]) &&
                    Extra[0].Props == ValueProp.Move && ReferenceEquals(Extra[0].Dealer, actor),
                    $"{label} adds {bonus} Move damage against a target with DP");
            else
                Check(Extra.Count == 0, label + " adds no DP bonus damage");
            Cases.Add(new { monster = spec.Type.Name, move = move.Id, hbr, ascension, heroDp,
                attacks = Attacks.Select(a => new[] { a.Damage, a.Hits }).ToArray(), blocks = Blocks.Select(b => b.Amount).ToArray(),
                extra = Extra.Select(e => e.Amount).ToArray(), strength = actor.GetPowerAmount<StrengthPower>(),
                heroPowers = Hero.Powers.Select(p => p.GetType().Name).ToArray(), dp = shell?.CurrentDp });
        }
    }

    // Phantoms replay attack damage from the intent; the interface restores the
    // move's debuff once, and only for its own move.
    static void PhantomEffectCases()
    {
        Type effects = ModType("HBRGOSakiMod.Core.Models.Monsters.IHbrgoPhantomAttackEffects");
        MethodInfo after = effects.GetMethod("AfterPhantomAttackIntent", F)!;
        var expected = Monsters.Where(m => m.Moves.Any(x => x.Debuff != "" && x.Damage0 > 0)).ToArray();
        Check(Monsters.Where(m => effects.IsAssignableFrom(m.Type)).Select(m => m.Type)
            .SequenceEqual(expected.Select(m => m.Type)), "exactly the attack+debuff monsters restore phantom side effects");
        foreach (MonsterSpec spec in expected)
        foreach (bool hbr in new[] { true, false })
        foreach (MoveState state in CycleOrder(Machine(Actor(spec.Type).Monster!)))
        {
            Reset(hbr, 0);
            Creature actor = Actor(spec.Type);
            MoveSpec move = spec.Moves.Single(m => m.Id == state.Id);
            Invoke(after, actor.Monster, state, new[] { Hero }).As<Task>().GetAwaiter().GetResult();
            string label = $"HBR{hbr} phantom {spec.Type.Name}.{state.Id}";
            Check(Attacks.Count == 0, label + " does not attack again");
            ExpectDebuff(hbr, move.Damage0 > 0 ? move.Debuff : "", move.DebuffRaw, label);
        }
    }

    // ---- fusion ------------------------------------------------------------

    const string FusionHelper = "HBRGOSakiMod.Core.Helpers.PhantomFusionEffectHelper";

    static string Fused(MonsterSpec spec, int copies)
    {
        string data = "";
        for (int copy = 0; copy < copies; copy++)
            data = (string)Static(FusionHelper, "AddDirectEffect", data, ModelDb.GetId(spec.Type))!;
        return data;
    }

    static string Describes(FusionSpec fusion) => fusion.Method switch
    {
        "GetStaticDamageBonusPercent" => $"伤害增幅+{fusion.First:0.##}%。",
        "GetStaticDamageReductionPercent" => $"伤害减免+{fusion.First:0.##}%。",
        "GetBonusMaxHp" => "最大生命值+",
        "GetExtraMaxDp" => "最大偏导护盾+",
        "FirstAttack" => $"第1次攻击意图段数+{fusion.First:0.##}。",
        "EveryThirdAttack" => $"每第3次攻击意图，额外造成{fusion.First:0.##}点伤害。",
        _ => throw new Exception("unknown fusion method " + fusion.Method),
    };

    static void FusionCases()
    {
        Type effectType = ModType("HBRGOSakiMod.Core.Helpers.PhantomFusionEffectType");
        var values = new HashSet<int>();
        foreach (MonsterSpec spec in Monsters)
        {
            string fusion = spec.Type.Name;
            ModelId id = ModelDb.GetId(spec.Type);
            object type = Static(FusionHelper, "GetMonsterEffectType", id)!;
            Check(type.ToString() == fusion, $"{spec.Type.Name} fuses as {fusion}");
            Check(values.Add(Convert.ToInt32(type)) && Convert.ToInt32(type) >= 100, spec.Type.Name + " has its own new fusion id");
            string once = Fused(spec, 1), twice = Fused(spec, 2);
            (string method, decimal first, decimal next) = spec.Fusion;
            Equal((decimal)Static(FusionHelper, "GetEffect", once, type, first, next)!, first, fusion + " first fusion value");
            Equal((decimal)Static(FusionHelper, "GetEffect", twice, type, first, next)!, first + next, fusion + " second fusion value");
            if (method.StartsWith("Get", StringComparison.Ordinal))
            {
                Equal((decimal)Static(FusionHelper, method, once)!, first, $"{fusion} feeds {method} once");
                Equal((decimal)Static(FusionHelper, method, twice)!, first + next, $"{fusion} feeds {method} twice");
            }
            var lines = (IReadOnlyList<string>)Static(FusionHelper, "GetDescriptionLines", once, null)!;
            Check(lines.Count == 1 && lines[0].StartsWith(fusion + "：", StringComparison.Ordinal) && lines[0].Contains(Describes(spec.Fusion)),
                $"{fusion} describes itself: {lines.FirstOrDefault()}");
        }
        Check(Enum.GetNames(effectType).Length == Enum.GetValues(effectType).Length, "fusion enum has unique names");
        Check(Enum.GetValues(effectType).Cast<object>().Select(Convert.ToInt32).Distinct().Count() == Enum.GetValues(effectType).Length,
            "fusion enum has unique values");
    }

    // First-attack segments and every-third-attack bonus damage are consumed by
    // PhantomHelper.BuildAttackSegments, so drive the real builder.
    static void SegmentCases()
    {
        MethodInfo build = ModType("HBRGOSakiMod.Core.Helpers.PhantomHelper").GetMethod("BuildAttackSegments", F)!;
        PropertyInfo performed = typeof(PhantomPower).GetProperty(nameof(PhantomPower.AttackIntentsPerformed))!;
        decimal[] Segments(Creature phantom, int attacksPerformed)
        {
            performed.GetSetMethod(true)!.Invoke(phantom.GetPower<PhantomPower>(), [attacksPerformed]);
            AttackIntent[] intents = [new SingleAttackIntent(10)];
            return ((IReadOnlyList<decimal>)Invoke(build, null, phantom, intents, 0, false)!).ToArray();
        }
        foreach (MonsterSpec spec in Monsters.Where(m => m.Fusion.Method is "FirstAttack" or "EveryThirdAttack"))
        {
            Reset(false, 0);
            Creature phantom = Actor(typeof(SmallHopper));
            Power<PhantomPower>(phantom, 1);
            Check(Segments(phantom, 0).SequenceEqual([10m]) && Segments(phantom, 2).SequenceEqual([10m]),
                $"{spec.Type.Name} phantom baseline has a single 10-damage segment without fusion");
            foreach (int copies in new[] { 1, 2 })
            {
                FusionData = Fused(spec, copies);
                decimal value = spec.Fusion.First + (copies - 1) * spec.Fusion.Next;
                string label = $"{spec.Type.Name} x{copies} phantom";
                if (spec.Fusion.Method == "FirstAttack")
                {
                    decimal[] first = Segments(phantom, 0);
                    Check(first.Length == 1 + (int)value && first.All(d => d == 10m), $"{label} first attack has {1 + value} segments");
                    Check(Segments(phantom, 1).SequenceEqual([10m]), $"{label} later attacks keep one segment");
                    Cases.Add(new { segments = spec.Type.Name, copies, first });
                }
                else
                {
                    foreach (int performedBefore in new[] { 2, 5 })
                        Check(Segments(phantom, performedBefore).SequenceEqual([10m, value]),
                            $"{label} attack {performedBefore + 1} adds a {value}-damage segment");
                    foreach (int performedBefore in new[] { 0, 1, 3 })
                        Check(Segments(phantom, performedBefore).SequenceEqual([10m]), $"{label} attack {performedBefore + 1} adds nothing");
                    Cases.Add(new { segments = spec.Type.Name, copies, third = Segments(phantom, 2) });
                }
            }
        }
    }

    // ---- files and localization -------------------------------------------

    static Dictionary<string, string> Loc(string name) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
            Path.Combine(RepositoryRoot, "HBRGOSakiMod/localization/zhs", name + ".json")))!;

    static (int Width, int Height, byte ColorType) Png(string path)
    {
        byte[] head = File.ReadAllBytes(path).Take(33).ToArray();
        Check(head.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), Path.GetFileName(path) + " is a PNG");
        int Read(int at) => head[at] << 24 | head[at + 1] << 16 | head[at + 2] << 8 | head[at + 3];
        return (Read(16), Read(20), head[25]);
    }

    static void FileCases()
    {
        Dictionary<string, string> monsters = Loc("monsters"), encounters = Loc("encounters");
        var recorded = new Dictionary<string, JsonElement>();
        foreach (ActSpec act in Acts)
        {
            using var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, $"images/monsters/hbrgo_act{act.Act}/sources.json")));
            foreach (JsonElement item in sources.RootElement.GetProperty("items").EnumerateArray())
                Check(recorded.TryAdd(item.GetProperty("monster").GetString()!, item.Clone()),
                    $"{item.GetProperty("monster").GetString()} has one provenance record");
            Equal(sources.RootElement.GetProperty("items").GetArrayLength(), Monsters.Count(m => m.Act == act.Act),
                $"act {act.Act} provenance lists exactly its new monsters");
        }
        foreach (MonsterSpec spec in Monsters)
        {
            string entry = ModelDb.GetEntry(spec.Type);
            Check(monsters.TryGetValue(entry + ".name", out string? name) && name.Length > 0, $"{entry}.name is localized ({name})");
            foreach (MoveSpec move in spec.Moves)
            {
                string key = $"{entry}.moves.{(move.Id.EndsWith("_MOVE") ? move.Id[..^5] : move.Id)}.title";
                Check(monsters.TryGetValue(key, out string? title) && title.Length > 0, $"{key} is localized ({title})");
            }
            string scene = Path.Combine(RepositoryRoot, "scenes/creature_visuals", entry.ToLowerInvariant() + ".tscn");
            Check(File.Exists(scene), $"{spec.Type.Name} default visuals scene {Path.GetFileName(scene)} exists");
            string text = File.ReadAllText(scene);
            string texture = $"images/monsters/hbrgo_act{spec.Act}/{spec.Art}_cutout.png";
            Check(text.Contains("res://" + texture) && text.Contains("res://Core/Nodes/Combat/NStaticHbrgoMonsterVisuals.cs"),
                $"{spec.Type.Name} scene uses the static visuals script and {texture}");
            (int width, int height, byte color) = Png(Path.Combine(RepositoryRoot, texture));
            Check(width >= 150 && height >= 300 && color == 6, $"{texture} is an RGBA cutout {width}x{height}");
            JsonElement record = recorded[spec.Type.Name];
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepositoryRoot, texture))));
            Check(record.GetProperty("file").GetString() == texture && record.GetProperty("sha256").GetString() == hash,
                $"{texture} matches its provenance record");
            Check(record.GetProperty("source").GetProperty("asset_path").GetString()!.StartsWith("Assets/Contents/Image/Arcade/Enemy/"),
                $"{spec.Type.Name} art comes from an HBR arcade enemy card");
        }
        foreach (EncounterSpec spec in Encounters)
        {
            string entry = ModelDb.GetEntry(spec.Type);
            foreach (string suffix in new[] { ".title", ".loss" })
                Check(encounters.TryGetValue(entry + suffix, out string? value) && value.Length > 0, $"{entry}{suffix} is localized");
            Check(encounters[entry + ".loss"].Contains("{encounter}") && encounters[entry + ".loss"].Contains("{character}"),
                entry + ".loss names both the character and the encounter");
        }
    }

    // ---- HBR resources -----------------------------------------------------

    static void ResourceCases()
    {
        foreach (MonsterSpec spec in Monsters)
        {
            Reset(true, 0);
            Creature actor = Actor(spec.Type, 1000);
            Equal((decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyResourceAdapter", "GetMaxHpScale", actor)!, 700m,
                spec.Type.Name + " uses the shared x700 HBR HP profile");
            Reset(false, 0);
            actor = Actor(spec.Type, 1000);
            Equal((decimal)Static("HBRGOSakiMod.Core.Helpers.HbrEnemyResourceAdapter", "GetMaxHpScale", actor)!, 1m,
                spec.Type.Name + " keeps native HP in legacy mode");
        }
    }

    public static void Run(string reportPath)
    {
        Install();
        MonsterStructureCases();
        EncounterCases();
        PoolCases();
        MoveCases();
        PhantomEffectCases();
        FusionCases();
        SegmentCases();
        FileCases();
        ResourceCases();
        string mod = Mod.Location;
        var report = new
        {
            suite = "hbr-act-rosters",
            modSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mod))),
            nativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ModelDb).Assembly.Location))),
            profile = Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE") ?? "stable",
            passed = Passed.Count,
            checks = Passed,
            cases = Cases,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"hbr-act-rosters: {Passed.Count} checks passed");
    }
}

static class TaskCast
{
    public static T As<T>(this object? value) => (T)value!;
}
