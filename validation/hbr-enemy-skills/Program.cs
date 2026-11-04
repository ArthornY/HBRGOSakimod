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
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

static partial class Checks
{
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const decimal Tolerance = 0.00000000000000000001m;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<object> CarryoverTraces=[];
    static string ReportPath=>System.Environment.GetEnvironmentVariable("HBR_ENEMY_SKILLS_REPORT")??Path.Combine(AppContext.BaseDirectory,"report.json");
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
        if (!condition) throw new Exception(label+"; enemyHP="+Enemy?.CurrentHp+"; powers="+string.Join(",",Enemy?.Powers.Select(power=>power.GetType().Name+":"+power.Amount)??[]));
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
    static decimal ActualDamage(Creature dealer, Creature target, decimal amount = 10m, ValueProp props = ValueProp.Move, CardModel? card = null)
    {
        object?[] stable = [RunState, Combat, target, dealer, amount, props, card, ModifyDamageHookType.All, CardPreviewMode.None, null];
        object?[] beta = [RunState, Combat, target, dealer, amount, props, card, null, ModifyDamageHookType.All, CardPreviewMode.None, null];
        return (decimal)Wrapper.Invoke(null, Wrapper.GetParameters().Length == 10 ? stable : beta)!;
    }
    static void Patch(Harmony harmony, MethodBase target, string method, int priority = Priority.Normal) =>
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(Checks).GetMethod(method, F)!) { priority = priority });
    static bool Skip() => false;
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    {
        if (FailListeners && ActualFlag) throw new InvalidOperationException("controlled-listener-fault");
        __result = CalibrationListeners ? Hero.Powers.Concat(Enemy.Powers).Cast<AbstractModel>().ToArray() : Array.Empty<AbstractModel>(); return false;
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
    static EncounterModel Encounter(RoomType room)
    {
        string name = room switch
        {
            RoomType.Elite => "BygoneEffigyElite",
            RoomType.Boss => "AeonglassBoss",
            _ => "BowlbugsWeak"
        };
        var encounter = (EncounterModel)RuntimeHelpers.GetUninitializedObject(typeof(Creature).Assembly.GetType(
            "MegaCrit.Sts2.Core.Models.Encounters." + name, true)!);
        Check(encounter.RoomType == room, "native encounter supplies room classification " + room);
        return encounter;
    }
    static void EnemyMap(int act, int? localFloor, RoomType? room = RoomType.Monster,
        int totalFloor = 0, RoomType? currentRoom = null, int actFloor = 1)
    {
        Set(RunState, "_currentActIndex", act);
        Set(RunState, "<ActFloor>k__BackingField", actFloor);
        Set(RunState, "_visitedMapCoords", localFloor.HasValue
            ? new List<MapCoord> { new(0, localFloor.Value - 1) } : new List<MapCoord>());
        var history=Enumerable.Range(0,Math.Max(0,act)+1).Select(_=>new List<MapPointHistoryEntry>()).ToList();
        history[^1]=Enumerable.Range(0,totalFloor).Select(_=>Blank<MapPointHistoryEntry>()).ToList();
        Set(RunState,"_mapPointHistory",history);
        Set(Combat, "_encounter", room.HasValue ? Encounter(room.Value) : null);
        var rooms = new List<AbstractRoom>();
        if (currentRoom.HasValue)
        {
            var roomCombat = Blank<CombatState>(); Set(roomCombat, "_encounter", Encounter(currentRoom.Value));
            var nativeRoom = Blank<CombatRoom>(); Set(nativeRoom, "<CombatState>k__BackingField", roomCombat);
            rooms.Add(nativeRoom);
        }
        Set(RunState, "_currentRooms", rooms);
    }
    static async Task EnemyGrowthChecks()
    {
        Creature original = Enemy;
        CalibrationConfig(); ResetSenses(); Progress(true);
        // Expected E values are explicit design points. Incoming and outgoing
        // values are independently derived here, then checked through the real
        // native Hook and production actual-damage wrapper, not just the getter.
        var cases = new[]
        {
            (Act: 0, Floor: 1, Room: RoomType.Monster, E: 150m),
            (Act: 0, Floor: 2, Room: RoomType.Monster, E: 156m),
            (Act: 0, Floor: 17, Room: RoomType.Monster, E: 246m),
            (Act: 1, Floor: 1, Room: RoomType.Monster, E: 210m),
            (Act: 1, Floor: 10, Room: RoomType.Elite, E: 294m),
            (Act: 2, Floor: 17, Room: RoomType.Boss, E: 416m),
            (Act: 0, Floor: 1, Room: RoomType.Elite, E: 180m),
            (Act: 0, Floor: 1, Room: RoomType.Boss, E: 200m)
        };
        foreach (bool hopper in new[] { false, true })
        {
            Enemy = Creature(hopper ? Blank<SmallHopper>() : Blank<TestSubject>());
            Move(Enemy, "ATTACK_MOVE"); Set(Combat, "_enemies", new List<Creature> { Enemy });
            foreach (var row in cases)
            {
                EnemyMap(row.Act, row.Floor, row.Room);
                string label = $"{(hopper ? "Hopper" : "ordinary actor")} act{row.Act} floor{row.Floor} {row.Room}";
                Equal(Parameter(Enemy), row.E, label + " resolves authored E");
                decimal incoming = row.E - 100m >= 140m ? 2000m : 1000m * (1m + (row.E - 100m) / 140m);
                Equal(Convert(Enemy, Hero), incoming, label + " converts incoming skill from growing E");
                Equal(Preview(Enemy, Hero), decimal.Floor(incoming), label + " native intent Hook floors final damage using growing E");
                await Command(Enemy);
                Equal(LastActual[^1], decimal.Floor(incoming), label + " actual command floors independently calculated incoming");
                decimal outgoing = row.E - 100m >= 140m ? 1m :
                    decimal.Floor(6000m * (1m + (100m - row.E) / 140m) * 100m) / 100m;
                Equal(Convert(Hero, Enemy, 6m), outgoing, label + " player attack curve reads target growing E");
                Equal(Preview(Hero, Enemy, 6m), decimal.Floor(outgoing), label + " player native preview floors target-dependent final damage");
                Equal(ActualDamage(Hero, Enemy, 6m), decimal.Floor(outgoing), label + " player actual wrapper floors target-dependent final damage");
                decimal potency = row.E < 300m ? 0.8m + 0.4m * row.E / 300m :
                    1.2m * (1m + (row.E - 300m) * 0.0002m);
                decimal block = decimal.Floor(3000m * potency);
                Equal(Number(Helper("HbrDamageSkillAdapter"), "ConvertBlock", Enemy, 3m, ValueProp.Move),
                    block, label + " enemy support bridge uses growing E");
                Equal(Hook.ModifyBlock(Combat, Enemy, 3m, ValueProp.Move, null, null, out _),
                    block, label + " native support Hook uses growing E");
            }
        }
        EnemyMap(-2, 1); Equal(Parameter(Enemy), 150m, "negative act index clamps to act zero");
        EnemyMap(0, 0); Equal(Parameter(Enemy), 150m, "negative coordinate row clamps local floor to one");
        EnemyMap(1, 2, totalFloor: 100, actFloor: 11);
        Check(RunState.TotalFloor == 100 && RunState.CurrentMapCoord!.Value.row == 1,
            "native history total and coordinate intentionally disagree for precedence test");
        Equal(Parameter(Enemy), 216m, "current map coordinate takes precedence over native ActFloor fallback");
        EnemyMap(1, null, totalFloor: 35, actFloor: 3);
        Check(RunState.CurrentMapCoord == null && RunState.TotalFloor == 35 && RunState.ActFloor == 3,
            "fallback scenario uses real absent coordinate and independent native ActFloor");
        Equal(Parameter(Enemy), 222m, "missing coordinate uses native ActFloor across variable-length acts");
        EnemyMap(2, null, totalFloor: 100, actFloor: 0);
        Equal(Parameter(Enemy), 270m, "native ActFloor fallback clamps local floor to one");
        EnemyMap(2, null, totalFloor: 5, actFloor: 7);
        Equal(Parameter(Enemy), 306m, "native ActFloor fallback ignores total history length");
        EnemyMap(0, null); Equal(Parameter(Enemy), 150m, "empty history and ActFloor1 use opening baseline");
        EnemyMap(1, 3, room: RoomType.Monster, currentRoom: RoomType.Boss);
        Equal(Parameter(Enemy), 222m, "combat encounter classification takes precedence over current room");
        EnemyMap(1, 3, room: null, currentRoom: RoomType.Elite);
        Equal(Parameter(Enemy), 252m, "missing encounter falls back to native current room classification");
        EnemyMap(1, 3, room: null);
        Equal(Parameter(Enemy), 222m, "missing encounter and room default to ordinary bonus");
        foreach (var pair in new[] { ("Madness", "Strength"), ("LeadershipCharm", "Dexterity"),
            ("Talent", "Intelligence"), ("Gentleness", "Vitality"), ("MentalStrength", "Spirit"), ("Simplicity", "Luck") })
        {
            Sense(pair.Item1, 37); object stats = Stats(Hero, out bool found);
            Check(found && Stat(stats, pair.Item2) == 174m, pair.Item1 + " really changes corresponding player stat");
            Equal(Parameter(Enemy), 222m, pair.Item1 + " allocation does not affect growing enemy E");
            ResetSenses();
        }
        Progress(true, 1000); Equal(Parameter(Enemy), 222m, "high player level does not alter growing enemy E"); Progress(true);
        Set(Combat, "<RunState>k__BackingField", null);
        Equal(Parameter(Enemy), 150m, "creature without run uses baseline150 even after later Hopper scenario");
        Set(Combat, "<RunState>k__BackingField", RunState);
        var detached = Blank<Creature>();
        Equal(Parameter(detached), 150m, "creature without combat uses baseline150");
        Enemy = original; Set(Combat, "_enemies", new List<Creature> { Enemy });
        EnemyMap(0, 1, room: null);
    }
    static List<List<MapPointHistoryEntry>> NativeHistory(params int?[] lengths)=>lengths.Select(length=>length.HasValue
        ?Enumerable.Range(0,length.Value).Select(_=>Blank<MapPointHistoryEntry>()).ToList():null!).ToList();
    static void HistoryMap(int act,int? localFloor,List<List<MapPointHistoryEntry>>? history,int fallback=1,RoomType room=RoomType.Monster)
    {EnemyMap(act,localFloor,room,actFloor:fallback);Set(RunState,"_mapPointHistory",history);}
    static void CarryE(decimal expected,string label,bool pipeline=false)
    {
        decimal actual=Parameter(Enemy);
        var history=(List<List<MapPointHistoryEntry>>?)Field(typeof(RunState),"_mapPointHistory").GetValue(RunState);
        CarryoverTraces.Add(new{label,act=RunState.CurrentActIndex,coord=RunState.CurrentMapCoord?.row,RunState.ActFloor,
            history=history?.Select(bucket=>(int?)bucket?.Count).ToArray(),expected,actual});
        Equal(actual,expected,label);
        if(pipeline)CarryPipeline(expected,label);
    }
    static void CarryPipeline(decimal parameter,string label)
    {
        decimal incoming=parameter-100m>=140m?2000m:1000m*(1m+(parameter-100m)/140m);
        Equal(Preview(Enemy,Hero),decimal.Floor(incoming),label+" reaches real incoming preview Hook");
        Equal(ActualDamage(Enemy,Hero),decimal.Floor(incoming),label+" reaches production incoming actual wrapper");
        decimal potency=parameter<300m?.8m+.4m*parameter/300m:1.2m*(1m+(parameter-300m)*.0002m);
        Equal(Hook.ModifyBlock(Combat,Enemy,3m,ValueProp.Move,null,null,out _),decimal.Floor(3000m*potency),label+" reaches growing-E native support-block Hook");
    }
    static void ActCarryoverChecks(bool checkPipeline)
    {
        CalibrationConfig();ResetSenses();Progress(true);CalibrationListeners=false;Move(Enemy,"UNKNOWN_MOVE");
        foreach(int[] lengths in new[]{new[]{17,22,9},new[]{8,13,19}})
        {
            decimal previousGrowth=0m;
            for(int act=1;act<=3;act++)
            {
                int last=lengths[act-1];int?[] completed=lengths.Take(act).Select(length=>(int?)length).ToArray();
                HistoryMap(act-1,last,NativeHistory(completed));
                decimal prior=150m+60m*(act-1)+6m*(previousGrowth+last-1);
                CarryE(prior,$"lengths{string.Join('-',lengths)} act{act} final ordinary floor{last}");
                previousGrowth+=last-1;decimal next=150m+60m*act+6m*previousGrowth;
                HistoryMap(act,1,NativeHistory(completed));
                CarryE(next,$"lengths{string.Join('-',lengths)} transition to act{act+1} before current history bucket exists",checkPipeline);
                Equal(next-prior,60m,"act transition adds60 while retaining all completed floor growth");
                HistoryMap(act,null,NativeHistory([..completed,0]));CarryE(next,"empty current history and absent coordinate use fresh ActFloor1");
                HistoryMap(act,4,NativeHistory([..completed,0]),fallback:99);CarryE(next+18m,"new coordinate contributes local steps even before history is written and overrides ActFloor99");
                HistoryMap(act,null,NativeHistory([..completed,90]),fallback:3);CarryE(next+12m,"absent coordinate uses current native ActFloor and never double counts current history");
                var history=NativeHistory([..completed,0]);HistoryMap(act,1,history);int?[] counts=history.Select(bucket=>(int?)bucket?.Count).ToArray();
                for(int read=0;read<10;read++)Equal(Parameter(Enemy),next,"repeated enemy-E read retains the same accumulated value");
                Check(ReferenceEquals(Field(typeof(RunState),"_mapPointHistory").GetValue(RunState),history)&&history.Select(bucket=>(int?)bucket?.Count).SequenceEqual(counts),"E getter reads existing native history without changing bucket identity or counts");
            }
        }
        var retained=NativeHistory(17);HistoryMap(0,17,retained);
        RunState.CurrentActIndex=1;
        Check(RunState.CurrentActIndex==1&&RunState.CurrentMapCoord==null&&RunState.ActFloor==0,"real native CurrentActIndex setter clears coordinates and resets ActFloor to zero");
        Check(ReferenceEquals(Field(typeof(RunState),"_mapPointHistory").GetValue(RunState),retained)&&retained.Count==1&&retained[0].Count==17,"real native act setter preserves existing history reference and completed17 node entries");
        CarryE(306m,"real native act setter retains prior16 floor steps when coordinates and ActFloor reset",checkPipeline);
        HistoryMap(0,17,NativeHistory(17),room:RoomType.Boss);CarryE(296m,"act1 floor17 boss has only its current temporary50 bonus");
        var calamity=Blank<HbrCalamityPower>();AddPower(Enemy,calamity);Set(calamity,"_amount",20);
        CarryE(276m,"current enemy calamity reduces the current boss parameter only");Powers(Enemy).Remove(calamity);
        HistoryMap(1,1,NativeHistory(17,0));CarryE(306m,"next act inherits floor96 but no previous boss50 or calamity20");
        HistoryMap(1,1,NativeHistory(17,0),room:RoomType.Elite);CarryE(336m,"only current elite adds30 on retained progress");
        HistoryMap(1,1,NativeHistory(17,0),room:RoomType.Boss);CarryE(356m,"only current boss adds50 on retained progress");
        HistoryMap(3,2,NativeHistory(1,0,null,100));CarryE(336m,"first-only empty and null previous buckets contribute zero floor growth");
        HistoryMap(3,2,NativeHistory(8));CarryE(378m,"missing later history buckets preserve available completed progress without assuming17 floors");
        HistoryMap(2,2,null);CarryE(276m,"null native history safely contributes no prior floor progress");
        HistoryMap(2,null,NativeHistory(17,22,100),fallback:0);CarryE(492m,"ActFloor clamps to one while completed native progress remains");
        HistoryMap(0,1,NativeHistory(99,99));CarryE(150m,"current and future history buckets cannot become previous-act growth");
        EnemyMap(0,1,room:null);ResetSenses();
    }
    public static Task RunActCarryover()
    {
        bool ok=false;string? error=null;
        try
        {
            var harmony=new Harmony("hbr.enemy.act-carryover.focused");
            Patch(harmony,typeof(StringName).GetConstructor([typeof(string)])!,nameof(Skip));
            Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
            foreach(MethodInfo method in logger.GetMethods(F).Where(method=>method.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))Patch(harmony,method,nameof(Skip));
            RunState=Blank<RunState>();EmptyList(RunState,"_visitedMapCoords");EmptyList(RunState,"_currentRooms");EmptyList(RunState,"_mapPointHistory");
            Combat=Blank<CombatState>();Set(Combat,"<RunState>k__BackingField",RunState);Player=Blank<Player>();Set(Player,"_runState",RunState);
            Hero=Creature(player:Player);Set(Player,"<Creature>k__BackingField",Hero);Enemy=Creature(Blank<SmallHopper>());
            Set(RunState,"_players",new List<Player>{Player});Set(Combat,"_allies",new List<Creature>{Hero});Set(Combat,"_enemies",new List<Creature>{Enemy});
            Manual=Blank<ElectronicSoldierManual>();Set(Manual,"<IsMutable>k__BackingField",true);Set(Manual,"_owner",Player);Set(Player,"_relics",new List<RelicModel>{Manual});
            ActCarryoverChecks(checkPipeline:false);ok=true;
        }
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllText(ReportPath,JsonSerializer.Serialize(new{passed=ok,checks=Passed.Count,cases=Passed,error,actCarryover=CarryoverTraces,
            modSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeGameSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=System.Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Focused managed native RunState history/MapCoord/ActFloor/Encounter/manual/power models and real production E getters. Native history buckets contain actual entry objects and are read without new saved state. No old enemy-skills suite, native damage Hook/HP/DP commands, act-transition gameplay, save loading, rendering, game process, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" focused act-carryover checks");if(!ok)throw new Exception("Inspect "+ReportPath);return Task.CompletedTask;
    }
    static async Task RunSuiteBody()
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
        await EnemyGrowthChecks();
        if(System.Environment.GetEnvironmentVariable("HBR_SKIP_ACT_CARRYOVER")!="1")ActCarryoverChecks(checkPipeline:true);
        object enemyStats = Stats(Enemy, out bool enemyFound);
        Check(!enemyFound && Stat(enemyStats, "Strength") == 0m, "enabled enemy exposes no fabricated six-stat profile");
        object heroStats = Stats(Hero, out bool heroFound);
        Check(heroFound && Stat(heroStats, "Strength") == 100m, "real saved manual supplies level-one player stats of 100");
        Creature cannon = Creature(Blank<HotaruContestCannon>(), owner: Player);
        Check(Stats(cannon, out bool cannonFound).Equals(heroStats) && cannonFound, "cannon inherits exact owner stats");
        Creature phantom = Creature(Blank<TestSubject>(), owner: Player); AddPower(phantom, Blank<PhantomPower>());
        Check(Stats(phantom, out bool phantomFound).Equals(heroStats) && phantomFound, "main phantom retains owner base stats at level one");
        Creature minion = Creature(Blank<TestSubject>(), owner: Player); var phantomPower = Blank<PhantomPower>();
        Set(phantomPower, "<IsSummonedMinion>k__BackingField", true); Set(phantomPower, "<SummonedMinionLevel>k__BackingField", 7); AddPower(minion, phantomPower);
        object minionStats = Stats(minion, out bool minionFound);
        Check(minionFound && Stat(minionStats, "Strength") == 112m && Stat(minionStats, "Spirit") == 112m, "summoned phantom preserves explicit level bonus");
        Equal(Parameter(Enemy), 150m, "enemy E comes from independent map profile");
        foreach (string name in new[] { "Madness", "LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity" })
        {
            Sense(name, 75); Equal(Parameter(Enemy), 150m, name + " changes cannot alter enemy E");
            Check(!((bool)Helper("HbrBattleStatsHelper").GetMethod("TryGetStats", F)!.Invoke(null, [Enemy, null])!), name + " cannot synthesize enemy stats");
            ResetSenses();
        }
        Progress(true, 1000); Equal(Parameter(Enemy), 150m, "player level cannot alter enemy E"); Progress(true);
        Sense("Gentleness", 25);
        foreach (string move in new[] { "DISAPPEAR_RAY_MOVE", "WORLDS_END_SUPER_NOVA_1_MOVE", "WORLDS_END_SUPER_NOVA_2_MOVE", "UNKNOWN_MOVE" })
        {
            Move(Enemy, move); bool spirit = move != "UNKNOWN_MOVE";
            Check(Reference(Enemy) == (spirit ? "Spirit" : "Vitality"), move + " keeps explicit skill defense reference");
            decimal defense = spirit ? 350m / 3m : 400m / 3m;
            decimal expected = 1000m * (1m + (150m - defense) / 140m);
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
        decimal support = Number(Helper("HbrSkillPower"), "EvaluateEnemyBlockFromUnits", 3m, Parameter(Enemy), 1000m);
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
        SingleMultiplierChecks();
        if(System.Environment.GetEnvironmentVariable("HBR_SKIP_HOPPER_CALIBRATION")!="1")await HopperCalibration(harmony);
    }
    public static async Task Run()
    {
        bool ok=false;string? error=null;
        try{await RunSuiteBody();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        bool skipHopper=System.Environment.GetEnvironmentVariable("HBR_SKIP_HOPPER_CALIBRATION")=="1";
        bool skipCarryover=System.Environment.GetEnvironmentVariable("HBR_SKIP_ACT_CARRYOVER")=="1";
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllText(ReportPath,JsonSerializer.Serialize(new
        {
            passed=ok,checks=Passed.Count,cases=Passed,error,hopperCalibrationSkipped=skipHopper,actCarryoverSkipped=skipCarryover,
            hopperCalibration=CalibrationReport,actCarryover=CarryoverTraces,
            modSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeGameSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            boundary="Existing saved-manual actor/map/scope/single-coefficient native Hook regression. Actual calls use production StrongestShield wrapper; canonical Damage terminal is an asynchronous actual-hook driver. "
                +(skipCarryover?"New act-carryover cases explicitly excluded. ":"Act-carryover native history/getter and real CurrentActIndex setter cases included, with native incoming preview/actual/support-block checks. ")
                +(skipHopper?"Obsolete Hopper destruction/card-budget calibration explicitly excluded: this is a stable segment, not the full prior suite. ":"Original Hopper HP/DP/calibration segment attempted with unchanged existing assertions; report error identifies any pre-existing failure. ")
                +"No rendered game, complete room transition, save deserialization, replay, Godot process, package or installed-file changes."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" enemy skill native checks");if(!ok)throw new Exception("Inspect "+ReportPath);
    }
}
