using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;

static class Checks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const decimal Epsilon = 0.00000000000000000001m;
    static readonly Assembly Mod = typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed = [];
    static readonly List<Creature> Actors = [];
    static readonly List<RelicModel> ExtraListeners = [];
    static RunState RunState = null!;
    static CombatState Combat = null!;
    static Player Player = null!;
    static Creature Hero = null!, Enemy = null!;
    static ElectronicSoldierManual Manual = null!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static string RepositoryRoot
    {
        get
        {
            for(DirectoryInfo? directory=new(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
                if(File.Exists(Path.Combine(directory.FullName,"HBRGOSakiMod.csproj")))return directory.FullName;
            throw new DirectoryNotFoundException("Fixture must remain below its checkout");
        }
    }
    static object? Call(string helper, string name, params object?[] args) => Helper(helper).GetMethod(name, F)!.Invoke(null, args);
    static decimal Number(string helper, string name, params object?[] args) => (decimal)Call(helper, name, args)!;
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
    static void EqualFinal(decimal actual,decimal expected,string name)=>Equal(actual,decimal.Floor(expected),name);
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
    static void Mode(bool enabled, int level = 1) => typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData", F)!
        .SetValue(Manual, JsonSerializer.Serialize(new { Schema = 1, Enabled = enabled, HealthScaleApplied = true, Level = level }));
    static void Sense(string name, int value) => typeof(ElectronicSoldierManual).GetProperty(name, F)!.SetValue(Manual, value);
    static void Reset()
    {
        foreach (Creature actor in Actors) Powers(actor).Clear();
        foreach (string name in new[] { "Madness", "LeadershipCharm", "Talent", "Gentleness", "MentalStrength", "Simplicity" }) Sense(name, 0);
        Mode(true); Act(0);
    }
    static void Act(int index) => Set(RunState, "_currentActIndex", index);
    static object Stats(Creature creature)
    {
        object?[] args = [creature, null];
        Check((bool)Call("HbrBattleStatsHelper", "TryGetStats", args)!, "real stats exist for " + (creature.Player != null ? "hero" : creature.Monster?.GetType().Name));
        return args[1]!;
    }
    static object RawStats(params decimal[] values) => Activator.CreateInstance(Helper("HbrBattleStats"), values.Cast<object>().ToArray())!;
    static decimal Stat(object stats, string name) => (decimal)stats.GetType().GetProperty(name, F)!.GetValue(stats)!;
    static void StatsEqual(object actual, decimal[] expected, string label)
    {
        string[] names = ["Strength", "Dexterity", "Intelligence", "Vitality", "Spirit", "Luck"];
        foreach (var row in names.Zip(expected)) Equal(Stat(actual, row.First), row.Second, label + " " + row.First);
    }
    static decimal Convert(Creature target, Creature dealer, decimal raw = 6m, ValueProp props = ValueProp.Move) =>
        Number("HbrDamageSkillAdapter", "ConvertBaseDamage", target, dealer, raw, props, null);
    static decimal Preview(Creature target, Creature dealer, decimal raw = 6m, ValueProp props = ValueProp.Move, CardModel? card = null) =>
        (decimal)Mod.GetType("HBRGOSakiMod.Core.Compatibility.HookCompatibility",true)!.GetMethod("ModifyDamage",F)!.Invoke(null,
            [RunState,Combat,target,dealer,raw,props,card,ModifyDamageHookType.All,CardPreviewMode.None,null,null])!;
    static decimal Block(Creature target, decimal raw = 5m, ValueProp props = ValueProp.Move, CardModel? card = null) =>
        Hook.ModifyBlock(Combat, target, raw, props, card, null, out _);
    static decimal NativeAdditive(PowerModel power, string name, params object?[] args)
    {
        MethodInfo method=power.GetType().GetMethods(F).Where(method=>method.Name==name)
            .OrderByDescending(method=>method.DeclaringType==power.GetType())
            .ThenByDescending(method=>method.GetParameters().Length).First();
        if(method.GetParameters().Length==args.Length+1)args=[..args,null];
        return (decimal)method.Invoke(power,args)!;
    }
    static decimal Actual(Creature target,Creature dealer,decimal raw=6m,ValueProp props=ValueProp.Move)
    {
        Type wrapper=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.StrongestShieldDamagePatch",true)!;
        MethodInfo method=wrapper.GetMethods(F).Where(m=>m.Name=="ModifyDamageWithStrongestShield").OrderBy(m=>m.GetParameters().Length).First();
        object?[] args=[RunState,Combat,target,dealer,raw,props,null,ModifyDamageHookType.All,CardPreviewMode.None,null];
        return (decimal)method.Invoke(null,args)!;
    }
    sealed class FractionalMultiplier : PowerModel
    {
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
        internal decimal Factor;
#if HBR_BETA_NATIVE
        public override decimal ModifyDamageMultiplicative(Creature? target,decimal damage,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)=>
#else
        public override decimal ModifyDamageMultiplicative(Creature? target,decimal damage,ValueProp props,Creature? dealer,CardModel? card)=>
#endif
            dealer==Owner?Factor:1m;
    }
    sealed class FractionalCap : PowerModel
    {
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
#if HBR_BETA_NATIVE
        public override decimal ModifyDamageCap(Creature? target,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)=>
#else
        public override decimal ModifyDamageCap(Creature? target,ValueProp props,Creature? dealer,CardModel? card)=>
#endif
            target==Owner?29.95m:decimal.MaxValue;
    }
    static T TestPower<T>(Creature owner) where T:PowerModel
    {
        var power=Blank<T>(); Set(power,"<IsMutable>k__BackingField",true); Set(power,"_owner",owner); Powers(owner).Add(power); return power;
    }
    static decimal Outgoing(decimal str, decimal dex, decimal enemy, bool dp = false, decimal raw = 6m)
    {
        decimal parameter = dp ? (str + dex * 2m) / 3m : (str * 2m + dex) / 3m;
        return decimal.Floor(Math.Max(1m, raw * 1000m * Math.Max(0m, 1m + (parameter - enemy) / 140m)) * 100m) / 100m;
    }
    static decimal Incoming(decimal enemy, decimal vit, decimal spirit, bool magic = false, decimal raw = 10m)
    {
        decimal defense = magic ? (vit + spirit * 2m) / 3m : (vit * 2m + spirit) / 3m;
        decimal delta = enemy - defense;
        if (delta >= 140m) return raw * 200m;
        if (delta <= -140m) return 1m;
        return Math.Max(1m, raw * 100m * (1m + delta / 140m));
    }
    static bool Skip() => false;
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    { __result = Actors.SelectMany(c => c.Powers).Cast<AbstractModel>().Concat(ExtraListeners).ToArray(); return false; }
    static void Setup()
    {
        TestMode.IsOn = true; var h = new Harmony("hbr.stat.powers.native.checks");
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
        Manual = Blank<ElectronicSoldierManual>(); Set(Manual, "<IsMutable>k__BackingField", true); Set(Manual, "_owner", Player);
        Set(Player, "_relics", new List<RelicModel> { Manual }); Set(RunState, "_players", new List<Player> { Player });
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
        Set(CombatManager.Instance,"_state",Combat); Set(CombatManager.Instance,"<IsInProgress>k__BackingField",true);
#endif
        foreach (var row in new[] { ("_enemyDamagePercent", 100), ("_enemyDamageGrowthPerActPercent", 0) })
            typeof(HBRGOSakiConfig).GetField(row.Item1, F)!.SetValue(null, row.Item2);
        // Install the real production power boundaries as well as the full
        // native hook adapters. No Strength/Dexterity return is mocked here.
        foreach (string name in new[] { "HbrgoSakiAdditiveDamagePatch", "HbrNumericBlockPatch", "HotaruCannonHappinessStrengthPatch",
            "HbrStrengthAttributePatch", "HbrDexterityAttributePatch", "HbrAbsoluteBaseDamageHookPatch" })
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            Check(h.CreateClassProcessor(type).Patch().Count > 0, "installs production " + name);
        }
        Reset();
    }
    static void AlliedCases()
    {
        Reset(); string saved = Manual.HbrBattleProgressData;
        var strength = Power<StrengthPower>(Hero, 1); var dexterity = Power<DexterityPower>(Hero, 1);
        StatsEqual(Stats(Hero), [101m, 101m, 101m, 100m, 100m, 100m], "one layer adds only the authored stats");
        Equal(NativeAdditive(strength,"ModifyDamageAdditive",Enemy, 6m, ValueProp.Move, Hero, null), 0m, "real native Strength additive is disabled in HBR");
        Equal(NativeAdditive(dexterity,"ModifyBlockAdditive",Hero, 5m, ValueProp.Move, null, null), 0m, "real native Dexterity additive is disabled in HBR");
        Equal(Convert(Enemy, Hero), Outgoing(101m, 101m, 150m), "adapter uses Strength stats once");
        EqualFinal(Preview(Enemy, Hero), Outgoing(101m, 101m, 150m), "full native Hook adds no extra1000 flat Strength");
        Equal(Block(Hero), 505m, "native block converts INT101 rather than raw5 plus Dex1");
        Equal(Block(Hero, props: ValueProp.Unpowered), 5m, "unpowered block ignores INT and Dexterity");
        Equal(Preview(Enemy, Hero, props: ValueProp.Unpowered), 6m, "unpowered damage ignores Strength stats");
        for (int i = 0; i < 10; i++)
        {
            EqualFinal(Preview(Enemy, Hero), Outgoing(101m, 101m, 150m), "repeated preview is stable " + i);
            Check(Manual.HbrBattleProgressData == saved && strength.Amount == 1 && dexterity.Amount == 1 && Manual.Madness == 0 && Manual.Talent == 0,
                "preview does not persist stats or mutate raw buff/manual " + i);
        }
        Set(strength, "_amount", -3); Set(dexterity, "_amount", -7);
        StatsEqual(Stats(Hero), [97m, 97m, 93m, 100m, 100m, 100m], "negative buffs reduce only authored stats");
        EqualFinal(Preview(Enemy, Hero), Outgoing(97m, 97m, 150m), "negative Strength has no extra native subtraction");
        Equal(Block(Hero), 465m, "negative Dexterity changes intelligence before block");
        Set(strength, "_amount", int.MinValue); Set(dexterity, "_amount", int.MinValue);
        StatsEqual(Stats(Hero), [0m, 0m, 0m, 100m, 100m, 100m], "negative layers clamp each effective stat at zero");
        Equal(Block(Hero), 0m, "zero intelligence has zero block");
        Powers(Hero).Clear(); StatsEqual(Stats(Hero), [100m,100m,100m,100m,100m,100m], "removing buffs restores pristine saved stats");
        Check(Manual.HbrBattleProgressData == saved, "buff add/change/remove never rewrites saved progression");
        Power<StrengthPower>(Hero,3); Power<FlexPotionPower>(Hero,3);
        Power<DexterityPower>(Hero,5); Power<SpeedPotionPower>(Hero,5);
        StatsEqual(Stats(Hero),[103m,103m,105m,100m,100m,100m],"temporary duration markers do not double already-applied native Strength Dexterity");
        EqualFinal(Preview(Enemy,Hero),Outgoing(103m,103m,150m),"temporary Strength marker adds no second stat or damage bonus");
        Equal(Block(Hero),525m,"temporary Dexterity marker adds no second intelligence or block bonus");
        Powers(Hero).Clear();
        var vigor = Power<VigorPower>(Hero, 2); EqualFinal(Preview(Enemy, Hero), Outgoing(100m,100m,150m)+2000m, "unrelated Vigor retains two fixed outgoing units");
        Check(vigor.Amount == 2, "damage preview does not consume Vigor");
        Powers(Hero).Clear(); Power<VigorPower>(Enemy, 2);
        EqualFinal(Preview(Hero,Enemy,10m),Incoming(150m,100m,100m)+200m,"enemy Vigor retains two fixed incoming units");
        Powers(Enemy).Clear();
        Powers(Hero).Clear();
    }
    static void NonHundredBase()
    {
        Sense("Madness",25); Sense("LeadershipCharm",40); Sense("Talent",60);
        Sense("Gentleness",15); Sense("MentalStrength",20); Sense("Simplicity",30);
    }
    static void PercentageCases()
    {
        Reset(); NonHundredBase(); string saved=Manual.HbrBattleProgressData;
        object original=Stats(Hero);
        StatsEqual(original,[150m,180m,220m,130m,140m,160m],"non100 saved profile before percentage buffs");
        var strength=Power<StrengthPower>(Hero,1);var dexterity=Power<DexterityPower>(Hero,1);
        StatsEqual(Stats(Hero),[151.5m,181.8m,222.2m,130m,140m,160m],"one layer scales each non100 base by1percent without truncating stats");
        EqualFinal(Preview(Enemy,Hero),Outgoing(151.5m,181.8m,150m),"real damage hook uses fractional percentage attributes exactly once");
        Equal(Block(Hero),1111m,"real block hook uses INT222point2 rather than adding one raw point");
        Set(strength,"_amount",20);Set(dexterity,"_amount",25);
        StatsEqual(Stats(Hero),[180m,216m,275m,130m,140m,160m],"20Strength and25Dexterity layers add percentages rather than compound each layer");
        var calamity=Power<HbrCalamityPower>(Hero,10);
        StatsEqual(Stats(Hero),[170m,206m,265m,120m,130m,150m],"fixed Calamity subtracts10 after percentage bonuses");
        EqualFinal(Preview(Enemy,Hero),Outgoing(170m,206m,150m),"post-percentage Calamity reaches actual damage preview");
        Equal(Block(Hero),1325m,"post-percentage fixed Calamity reaches actual block preview");
        Powers(Hero).Remove(calamity);Set(strength,"_amount",-50);Set(dexterity,"_amount",-25);
        StatsEqual(Stats(Hero),[75m,90m,165m,130m,140m,160m],"negative signed layers scale each original non100 attribute");
        foreach(int amount in new[]{-100,-101,int.MinValue})
        {
            Set(strength,"_amount",amount);Set(dexterity,"_amount",amount);
            StatsEqual(Stats(Hero),[0m,0m,0m,130m,140m,160m],"effective percentage floors at zero for signed amount"+amount);
        }
        Set(strength,"_amount",20);Set(dexterity,"_amount",25);
        for(int index=0;index<3;index++)
        {
            StatsEqual(Stats(Hero),[180m,216m,275m,130m,140m,160m],"repeated non100 percentage stat view does not compound"+index);
            Check(Manual.HbrBattleProgressData==saved&&Manual.Madness==25&&Manual.LeadershipCharm==40&&Manual.Talent==60,
                "percentage stat read never changes progression or allocated senses"+index);
        }
        StatsEqual(original,[150m,180m,220m,130m,140m,160m],"previous stat snapshot remains immutable after buff changes");
        Powers(Hero).Remove(strength);Powers(Hero).Remove(dexterity);
        StatsEqual(Stats(Hero),[150m,180m,220m,130m,140m,160m],"live buff removal immediately restores unmodified non100 bases");
        Check(Manual.HbrBattleProgressData==saved,"percentage add/change/remove never persists temporary attributes");
        Reset();
    }
    static void PercentageOwnershipCases()
    {
        Reset();NonHundredBase();string saved=Manual.HbrBattleProgressData;
        var ownerStrength=Power<StrengthPower>(Hero,20);Power<DexterityPower>(Hero,25);
        var phantom=Actor(Blank<TestSubject>(),owner:Player);Power<PhantomPower>(phantom,1);
        StatsEqual(Stats(phantom),[150m,180m,220m,130m,140m,160m],"non100 phantom does not inherit owner percentage buffs");
        Power<StrengthPower>(phantom,10);Power<DexterityPower>(phantom,40);
        StatsEqual(Stats(phantom),[165m,198m,308m,130m,140m,160m],"non100 phantom uses only its own percentage layers");
        EqualFinal(Preview(Enemy,phantom),Outgoing(165m,198m,150m),"phantom native damage hook applies its own percent once");
        Equal(Block(phantom),1540m,"phantom native block hook applies its own40percent INT once");
        var millsage=new Millsage();Set(millsage,"<IsMutable>k__BackingField",true);Set(millsage,"_owner",Player);
        Set(Player,"_relics",new List<RelicModel>{Manual,millsage});
        var happy=Actor(Blank<HotaruHappinessCannon>(),owner:Player);var contest=Actor(Blank<HotaruContestCannon>(),owner:Player);
        Set(Combat,"_allies",new List<Creature>{Hero,phantom,happy,contest});
        Power<StrengthPower>(contest,999);Power<DexterityPower>(contest,999);
        StatsEqual(Stats(contest),[180m,216m,275m,130m,140m,160m],"non100 cannon uses owner percentage buffs once and ignores local native counters");
        Equal(Block(contest),1375m,"non100 cannon block reads owner INT275 once");
        EqualFinal(Cannon(contest,()=>Preview(Enemy,Hero)),Outgoing(180m,216m,150m)*.5m,"non100 cannon damage uses owner percentage attributes once before soul factor");
        typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(millsage,3);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(210m,252m,150m),"HappinessLv3 adds a second20percent rather than compounding1point2 twice");
        StatsEqual(Stats(Hero),[180m,216m,275m,130m,140m,160m],"Happiness percentage copy remains local to its attack");
        var ownerCalamity=Power<HbrCalamityPower>(Hero,10);Power<HbrCalamityPower>(happy,3);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(197m,239m,150m),"Happiness extra percentage resolves before owner10 and local3 fixed Calamity");
        Set(ownerStrength,"_amount",-20);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(107m,131m,150m),"Happiness does not double negative percentage Strength before fixed Calamity");
        Set(ownerStrength,"_amount",20);Powers(Hero).Remove(ownerCalamity);Powers(happy).Clear();Power<DexterityPower>(Enemy,3);Act(1);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(204m,246m,210m),"enemy flat per-act ignore remains after local percentage Happiness bonus");
        Act(0);Powers(Enemy).Clear();
        var state=Blank<PlayerCombatState>();Set(state,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",state);
        typeof(Millsage).GetProperty("LongingUpgradeLevel",F)!.SetValue(millsage,2);
        var again=Power<HotaruSeeYouAgainPower>(Hero,2);
        typeof(HotaruSeeYouAgainPower).GetProperty("OrdinaryCopies",F)!.SetValue(again,2);
        typeof(HotaruSeeYouAgainPower).GetProperty("OrdinaryTurnNumber",F)!.SetValue(again,1);
        Equal(Number("HotaruSeeYouAgainHelper","GetExtraDexterity",Player),6m,"real active copies supply six virtual Dexterity layers");
        StatsEqual(Stats(Hero),[180m,216m,288.2m,130m,140m,160m],"virtual six layers add to native25percent before non100 INT multiplication");
        Equal(Block(Hero),1441m,"native block uses220 times1point31 intelligence");
        Equal(Block(contest),1441m,"cannon virtual Dexterity reads owner percentage once");
        StatsEqual(Stats(phantom),[165m,198m,308m,130m,140m,160m],"phantom percentage stats remain independent of owner virtual Dexterity");
        Set(state,"<TurnNumber>k__BackingField",2);Equal(Block(Hero),1375m,"virtual Dexterity expiry immediately removes only its six percent");
        Check(Manual.HbrBattleProgressData==saved,"ownership and virtual percentage effects never rewrite progression");
        Set(Player,"_relics",new List<RelicModel>{Manual});Set(Combat,"_allies",new List<Creature>{Hero});Reset();
    }
    static void EnemyCases()
    {
        Reset(); Sense("Gentleness", 5); Sense("MentalStrength", 10);
        var strength = Power<StrengthPower>(Enemy, 3); var dex = Power<DexterityPower>(Enemy, 4);
        object defense = RawStats(100m,110m,120m,11m,21m,150m);
        object attack = RawStats(310m,450m,200m,130m,170m,190m);
        foreach (int index in new[] { -2,0,1,2,3,4,5,9 })
        {
            Act(index); decimal per = index <= 0 ? 1m : index == 1 ? 2m : index == 2 ? 3m : index == 3 ? 4m : 8m;
            Equal(Number("HbrStatPowerHelper", "GetEnemyIgnorePerLayer", Enemy), per, "enemy ignore per layer act index " + index);
            Equal(Number("HbrStatPowerHelper", "GetEnemyStrengthIgnorePerLayer", Enemy), per, "Strength UI wrapper equals common coefficient " + index);
            object reduced = Call("HbrStatPowerHelper", "GetIncomingDefense", Enemy, defense)!;
            StatsEqual(reduced, [100m,110m,120m,Math.Max(0m,11m-3m*per),Math.Max(0m,21m-3m*per),150m], "enemy Strength defense local act " + index);
            object outgoing = Call("HbrStatPowerHelper", "GetOutgoingAttack", Enemy, Hero, attack, null, ValueProp.Move)!;
            StatsEqual(outgoing, [310m-4m*per,450m-4m*per,200m,130m,170m,190m], "enemy Dexterity attack local act " + index);
            StatsEqual(defense, [100m,110m,120m,11m,21m,150m], "incoming original defense stays immutable " + index);
            StatsEqual(attack, [310m,450m,200m,130m,170m,190m], "outgoing original attacker stays immutable " + index);
            decimal e = 150m + Math.Max(0,index)*60m;
            Equal(Convert(Hero,Enemy,10m), Incoming(e,Math.Max(0m,110m-3m*per),Math.Max(0m,120m-3m*per)), "incoming adapter honors Strength stat ignore " + index);
            EqualFinal(Preview(Hero,Enemy,10m), Convert(Hero,Enemy,10m), "native pipeline adds no fixed enemy Strength damage " + index);
            Equal(NativeAdditive(dex,"ModifyBlockAdditive",Enemy,3m,ValueProp.Move,null,null),0m,"native enemy Dexterity contributes no flat block " + index);
            Equal(NativeAdditive(strength,"ModifyDamageAdditive",Hero,10m,ValueProp.Move,Enemy,null),0m,"native enemy Strength contributes no flat damage " + index);
        }
        Act(4); Set(strength,"_amount",-3); Set(dex,"_amount",-4);
        StatsEqual(Call("HbrStatPowerHelper","GetIncomingDefense",Enemy,defense)!,[100m,110m,120m,35m,45m,150m],"negative enemy Strength raises temporary defense");
        StatsEqual(Call("HbrStatPowerHelper","GetOutgoingAttack",Enemy,Hero,attack,null,ValueProp.Move)!,[342m,482m,200m,130m,170m,190m],"negative enemy Dexterity raises temporary attacker stats");
        Set(dex,"_amount",10000);
        StatsEqual(Call("HbrStatPowerHelper","GetOutgoingAttack",Enemy,Hero,attack,null,ValueProp.Move)!,[0m,0m,200m,130m,170m,190m],"enemy Dexterity clamps STR DEX individually at zero");
        Powers(Enemy).Clear(); Reset();
        var magic = Actor(Blank<RedCrimsonBossMonster>());
        Set(magic.Monster!,"<NextMove>k__BackingField",new MoveState("DISAPPEAR_RAY_MOVE",_=>Task.CompletedTask));
        Power<StrengthPower>(magic,5); Sense("Gentleness",25);
        Equal(Convert(Hero,magic,10m),Incoming(150m,145m,95m,true),"spirit-biased native enemy skill weights apply after both stat ignores");
        EqualFinal(Preview(Hero,magic,10m),Convert(Hero,magic,10m),"spirit-biased intent uses same pipeline and zero native additive");
        Reset(); Sense("Madness",75); Sense("LeadershipCharm",125); Power<DexterityPower>(Enemy,6);
        foreach (int index in new[] {0,1,2,3,4,8})
        {
            Act(index); decimal per = index == 0 ? 1m : index == 1 ? 2m : index == 2 ? 3m : index == 3 ? 4m : 8m;
            Equal(Convert(Enemy,Hero),Outgoing(250m-6m*per,350m-6m*per,150m+60m*index),"HP weights consume local dex-ignore stats " + index);
            var shell=Power<HighDimensionalShellPower>(Enemy,1);
            typeof(HighDimensionalShellPower).GetProperty("CurrentDpData",F)!.SetValue(shell,"100");
            Equal(Convert(Enemy,Hero),Outgoing(250m-6m*per,350m-6m*per,150m+60m*index,true),"DP weights consume local dex-ignore stats " + index);
            Powers(Enemy).Remove(shell);
        }
        Reset();
    }
    static object? Async(Type type,string name) { object local=type.GetField(name,F)!.GetValue(null)!; return local.GetType().GetProperty("Value")!.GetValue(local); }
    static void Async(Type type,string name,object? value) { object local=type.GetField(name,F)!.GetValue(null)!; local.GetType().GetProperty("Value")!.SetValue(local,value); }
    static decimal Cannon(Creature cannon,Func<decimal> calculate)
    {
        Type helper=Helper("HotaruCannonStatsHelper"); object? prior=Async(helper,"Pending");
        object intent=Activator.CreateInstance(helper.GetNestedType("Intent",F)!,true)!;
        Set(intent,"Cannon",cannon); Set(intent,"Target",Enemy); Set(intent,"Dealer",Hero); Set(intent,"Amount",6m); Set(intent,"Props",ValueProp.Move);
        Async(helper,"Pending",intent); object?[] args=[new[]{Enemy},6m,ValueProp.Move,Hero,null,null];
        object scope=helper.GetMethod("Begin",F)!.Invoke(null,args)!;
        try { return calculate(); } finally { Call("HotaruCannonStatsHelper","Close",scope); Async(helper,"Pending",prior); }
    }
    static void OwnershipCases()
    {
        Reset(); var phantom=Actor(Blank<TestSubject>(),owner:Player); Power<PhantomPower>(phantom,1);
        var ownerStr=Power<StrengthPower>(Hero,10); Power<DexterityPower>(Hero,7);
        StatsEqual(Stats(phantom),[100m,100m,100m,100m,100m,100m],"phantom does not inherit summoner native buffs");
        Power<StrengthPower>(phantom,3); Power<DexterityPower>(phantom,4);
        StatsEqual(Stats(phantom),[103m,103m,104m,100m,100m,100m],"phantom only uses its own Strength Dexterity");
        EqualFinal(Preview(Enemy,phantom),Outgoing(103m,103m,150m),"phantom full damage uses its own strength once");
        Equal(Block(phantom),520m,"phantom full block uses its own Dexterity INT");
        var millsage=new Millsage(); Set(millsage,"<IsMutable>k__BackingField",true); Set(millsage,"_owner",Player);
        Set(Player,"_relics",new List<RelicModel>{Manual,millsage});
        var happy=Actor(Blank<HotaruHappinessCannon>(),owner:Player);
        var contest=Actor(Blank<HotaruContestCannon>(),owner:Player);
        Set(Combat,"_allies",new List<Creature>{Hero,phantom,happy,contest});
        Power<StrengthPower>(contest,999); Power<DexterityPower>(contest,999);
        StatsEqual(Stats(contest),[110m,110m,107m,100m,100m,100m],"cannon stat view uses owner buffs once instead of extra pet counters");
        Equal(Block(contest),535m,"cannon block uses owner's current INT107 exactly once");
        EqualFinal(Cannon(contest,()=>Preview(Enemy,Hero)),Outgoing(110m,110m,150m)*0.5m,"ordinary owner-sourced cannon uses hero stats once then soul factor");
        typeof(Millsage).GetProperty("HappinessUpgradeLevel",F)!.SetValue(millsage,3);
        object stats=Stats(Hero);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(120m,120m,150m),"happy Lv3 doubles positive Strength stat contribution only locally");
        Check(stats.Equals(Stats(Hero))&&ownerStr.Amount==10,"happy local upgrade does not persist its doubled stat contribution");
        Set(ownerStr,"_amount",-10);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(90m,90m,150m),"happy Lv3 does not double negative Strength stat contribution");
        Set(ownerStr,"_amount",10); Power<DexterityPower>(Enemy,3); Act(1);
        Equal(Cannon(happy,()=>Convert(Enemy,Hero)),Outgoing(114m,114m,210m),"happy extra Strength and enemy Dexterity share one local attack snapshot");
        Act(0); Powers(Enemy).Clear();
        // Build a real active ordinary-turn copy bucket. GetExtraDexterity and
        // ActiveCopies still run production logic; their return is never mocked.
        var playerCombat=Blank<PlayerCombatState>(); Set(playerCombat,"<TurnNumber>k__BackingField",1);
        Set(Player,"<PlayerCombatState>k__BackingField",playerCombat);
        typeof(Millsage).GetProperty("LongingUpgradeLevel",F)!.SetValue(millsage,2);
        var again=Power<HotaruSeeYouAgainPower>(Hero,2);
        typeof(HotaruSeeYouAgainPower).GetProperty("OrdinaryCopies",F)!.SetValue(again,2);
        typeof(HotaruSeeYouAgainPower).GetProperty("OrdinaryTurnNumber",F)!.SetValue(again,1);
        Equal(Number("HotaruSeeYouAgainHelper","GetExtraDexterity",Player),6m,"two real active longing copies expose six virtual Dexterity");
        StatsEqual(Stats(Hero),[110m,110m,113m,100m,100m,100m],"virtual Dexterity enters current hero intelligence once");
        Equal(NativeAdditive(again,"ModifyBlockAdditive",Hero,5m,ValueProp.Move,null!,null!),0m,"HBR virtual Dexterity no longer adds raw block");
        Equal(Block(Hero),565m,"real active longing block uses INT113 without additive rescale");
        var defend=new DefendIronclad(); Set(defend,"<IsMutable>k__BackingField",true); Set(defend,"_owner",Player);
        Equal(Block(Hero,card:defend),565m,"card-sourced native Defend uses current INT113 once");
        Equal(Block(contest),565m,"cannon receives owner's virtual intelligence once");
        StatsEqual(Stats(phantom),[103m,103m,104m,100m,100m,100m],"phantom remains independent of owner's virtual Dexterity");
        Set(playerCombat,"<TurnNumber>k__BackingField",2);
        Equal(Number("HotaruSeeYouAgainHelper","GetExtraDexterity",Player),0m,"expired ordinary-turn virtual Dexterity reads zero");
        Equal(Block(Hero),535m,"turn expiry immediately restores only native Dexterity intelligence");
        Set(playerCombat,"<TurnNumber>k__BackingField",1); Mode(false);
        Equal(NativeAdditive(again,"ModifyBlockAdditive",Hero,5m,ValueProp.Move,null!,null!),6m,"legacy virtual Dexterity keeps native raw additive six");
        Equal(Block(Hero),18m,"legacy block stacks raw5 nativeDex7 and virtualDex6 normally");
        Mode(true);
        Set(Player,"_relics",new List<RelicModel>{Manual}); Reset();
    }
    static void LegacyCases()
    {
        Reset(); var s=Power<StrengthPower>(Hero,2); var d=Power<DexterityPower>(Hero,3);
        var es=Power<StrengthPower>(Enemy,4); var ed=Power<DexterityPower>(Enemy,5); Mode(false);
        Equal(NativeAdditive(s,"ModifyDamageAdditive",Enemy,6m,ValueProp.Move,Hero,null),2m,"legacy native player Strength additive remains two");
        Equal(NativeAdditive(d,"ModifyBlockAdditive",Hero,5m,ValueProp.Move,null,null),3m,"legacy native player Dexterity additive remains three");
        Equal(NativeAdditive(es,"ModifyDamageAdditive",Hero,6m,ValueProp.Move,Enemy,null),4m,"legacy native enemy Strength additive remains four");
        Equal(NativeAdditive(ed,"ModifyBlockAdditive",Enemy,5m,ValueProp.Move,null,null),5m,"legacy native enemy Dexterity additive remains five");
        Equal(Preview(Enemy,Hero),8m,"disabled HBR full player pipeline retains raw6 plus Strength2");
        Equal(Preview(Hero,Enemy),10m,"disabled HBR full enemy pipeline retains raw6 plus Strength4");
        Equal(Block(Hero),8m,"disabled HBR block retains raw5 plus Dexterity3");
        Equal(Block(Enemy),10m,"disabled HBR enemy block retains raw5 plus Dexterity5");
        object raw=RawStats(1m,2m,3m,4m,5m,6m);
        Check(Call("HbrStatPowerHelper","Apply",Hero,raw)!.Equals(raw),"disabled HBR Apply preserves all incoming base stats");
        Check(Call("HbrStatPowerHelper","GetIncomingDefense",Enemy,raw)!.Equals(raw),"disabled HBR ignores no defense stats");
        Check(Call("HbrStatPowerHelper","GetOutgoingAttack",Enemy,Hero,raw,null,ValueProp.Move)!.Equals(raw),"disabled HBR ignores no attack stats");
        Reset();
        Check(Call("HbrStatPowerHelper","Apply",null,raw)!.Equals(raw),"ownerless stats remain unchanged");
        Check(Call("HbrStatPowerHelper","GetIncomingDefense",null,raw)!.Equals(raw),"missing dealer ignores no defense stats");
        Check(Call("HbrStatPowerHelper","GetOutgoingAttack",null,Hero,raw,null,ValueProp.Move)!.Equals(raw),"missing target ignores no attack stats");
    }
    static void DescriptionCases()
    {
        Reset();
        string runtime=System.Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY")??
            "D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving+=(context,name)=>{
            string path=Path.Combine(runtime,name.Name+".dll"); return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        var manager=Blank<LocManager>(); Set(manager,"<Language>k__BackingField","zhs");
        Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture); Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        string localization=Path.Combine(RepositoryRoot,"HBRGOSakiMod/localization/zhs/powers.json");
        var table=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(localization))!;
        Set(manager,"_tables",new Dictionary<string,LocTable>{["powers"]=new LocTable("powers",table,null!)});
        typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrStatPowerDescriptionPatch",true)!;
        LocString Rewrite(PowerModel power,LocString text)=>(LocString)patch.GetMethod("ForPower",F)!.Invoke(null,[power,text])!;
        var original=new LocString("powers","NATIVE_ORIGINAL.description"); original.Add("TestMarker",77);
        PowerModel[] powers=[Power<StrengthPower>(Hero,3),Power<DexterityPower>(Hero,4),Power<StrengthPower>(Enemy,5),Power<DexterityPower>(Enemy,6)];
        string[] keys=["HBR_STAT_STRENGTH_ALLY.description","HBR_STAT_DEXTERITY_ALLY.description","HBR_STAT_STRENGTH_ENEMY.description","HBR_STAT_DEXTERITY_ENEMY.description"];
        for(int i=0;i<powers.Length;i++)
        {
            LocString text=Rewrite(powers[i],original);
            Check(text.LocTable=="powers"&&text.LocEntryKey==keys[i],"mutable owned power selects exact HBR description "+keys[i]);
            Check((decimal)text.Variables["TestMarker"]==77m,"description preserves original bound variables "+keys[i]);
            Check(!text.GetFormattedText().Contains('{')&&!text.GetFormattedText().Contains("每次攻击造成"),"real SmartFormat renders complete new stat rule "+keys[i]);
        }
        Check(Rewrite(powers[0],original).GetFormattedText().Contains("[gold]力[/gold]与[gold]器用[/gold]"),"allied Strength description names both stats");
        Check(Rewrite(powers[1],original).GetFormattedText().Contains("[gold]知性[/gold]"),"allied Dexterity description names intelligence");
        foreach(PowerModel power in powers.Take(2))
        {
            string text=Rewrite(power,original).GetFormattedText();
            Check(text.Contains("1%")&&text.Contains("同类层数相加"),"allied percentage description renders one percent and additive layers "+power.GetType().Name);
        }
        foreach(int index in new[]{0,1,2,3,4,5,20})
        {
            Act(index);decimal expected=index==0?1m:index==1?2m:index==2?3m:index==3?4m:8m;
            foreach(PowerModel power in powers.Skip(2))
            {
                LocString text=Rewrite(power,original);
                Check((decimal)text.Variables["IgnorePerLayer"]==expected,"description binds current per-act ignore "+power.GetType().Name+index);
                Check(text.GetFormattedText().Contains("[blue]"+expected+"[/blue]点"),"real enemy description renders bound ignore "+power.GetType().Name+index);
            }
        }
        Mode(false);
        foreach(PowerModel power in powers)Check(ReferenceEquals(Rewrite(power,original),original),"legacy description returns original LocString "+power.GetType().Name+power.Owner.Side);
        Mode(true);
        var canonical=Blank<StrengthPower>();
        Check(ReferenceEquals(Rewrite(canonical,original),original),"canonical ownerless power retains original LocString");
        var detached=Power<DexterityPower>(Hero,3); Set(detached,"_owner",null);
        Check(ReferenceEquals(Rewrite(detached,original),original),"mutable ownerless power retains original LocString");
        Check(ReferenceEquals(Rewrite(Power<VigorPower>(Hero,2),original),original),"unrelated Vigor description retains original LocString");
        Check(original.LocEntryKey=="NATIVE_ORIGINAL.description"&&original.Variables.Count==1,"description wrapper never mutates original LocString");
        Reset();
    }
    static void StrikeDummyCases()
    {
        Reset();
        StrikeDummy Dummy(Player owner)
        {
            var relic = new StrikeDummy(); Set(relic,"<IsMutable>k__BackingField",true); Set(relic,"_owner",owner);
            ((List<RelicModel>)Field(typeof(Player),"_relics").GetValue(owner)!).Add(relic);
            ExtraListeners.Add(relic); return relic;
        }
        var strike = new StrikeIronclad(); Set(strike,"<IsMutable>k__BackingField",true); Set(strike,"_owner",Player);
        EqualFinal(Preview(Enemy,Hero,card:strike),Outgoing(100m,100m,150m),"Strike without Dummy retains100percent skill coefficient");
        var dummy = Dummy(Player);
        EqualFinal(Preview(Enemy,Hero,card:strike),Outgoing(100m,100m,150m,raw:9m),"owned Dummy changes100percent Strike to150percent before attribute curve");
        EqualFinal(Preview(Enemy,Hero,raw:9m,card:strike),Outgoing(100m,100m,150m,raw:12m),"Dummy adds50points to150percent upgraded Strike rather than multiplying by1point5");
        EqualFinal(Preview(Enemy,Hero,raw:0m,card:strike),Outgoing(100m,100m,150m,raw:3m),"zero base Strike still receives Dummy50percent skill coefficient");
        using((IDisposable)Call("HbrAbsoluteBaseDamageContext","Begin",Hero,strike,Enemy,6m)!)
            Equal(Preview(Enemy,Hero,card:strike),6m,"absolute world-space Strike skips coefficient and second native flat three");
        using((IDisposable)Call("PrecalculatedDamageContext","Begin",strike,Enemy,6m)!)
            Equal(Preview(Enemy,Hero,card:strike),6m,"precalculated Strike skips coefficient and second native flat three");
        for(int i=0;i<3;i++) EqualFinal(Preview(Enemy,Hero,card:strike),Outgoing(100m,100m,150m,raw:9m),"repeated native preview applies exactly one owned Dummy "+i);
        var strength=Power<StrengthPower>(Hero,20);
        EqualFinal(Preview(Enemy,Hero,card:strike),Outgoing(120m,120m,150m,raw:9m),"Dummy coefficient receives current Strength attribute curve");
        Powers(Hero).Remove(strength);
        var defend = new DefendIronclad(); Set(defend,"<IsMutable>k__BackingField",true); Set(defend,"_owner",Player);
        EqualFinal(Preview(Enemy,Hero,card:defend),Outgoing(100m,100m,150m),"card without Strike tag receives no Dummy coefficient");
        EqualFinal(Preview(Enemy,Hero),Outgoing(100m,100m,150m),"cardless attack receives no Dummy coefficient");
        Equal(Preview(Enemy,Hero,props:ValueProp.Unpowered,card:strike),6m,"unpowered Strike source receives no Dummy conversion or flat bonus");
        Mode(false);
        Equal(Preview(Enemy,Hero,card:strike),9m,"legacy native Strike Dummy still adds three flat damage");
        Mode(true);
        var phantom=Actor(Blank<SmallHopper>(),owner:Player); Power<PhantomPower>(phantom,1);
        EqualFinal(Preview(Enemy,phantom,card:strike),Outgoing(100m,100m,150m),"phantom does not inherit excluded summoner Dummy coefficient or flat damage");
        var other=Blank<Player>(); Set(other,"_runState",RunState); var otherHero=Actor(player:other); Set(other,"<Creature>k__BackingField",otherHero);
        var otherManual=Blank<ElectronicSoldierManual>(); Set(otherManual,"<IsMutable>k__BackingField",true); Set(otherManual,"_owner",other);
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(otherManual,Manual.HbrBattleProgressData);
        Set(other,"_relics",new List<RelicModel>{otherManual}); Dummy(other); Set(strike,"_owner",other);
        EqualFinal(Preview(Enemy,Hero,card:strike),Outgoing(100m,100m,150m,raw:12m),"borrowed Strike retains distinct dealer and card owner Dummy bonuses once each");
        Set(Player,"_relics",new List<RelicModel>{Manual}); ExtraListeners.Remove(dummy);
        EqualFinal(Preview(Enemy,Hero,card:strike),Outgoing(100m,100m,150m,raw:9m),"borrowed Strike card owner Dummy qualifies without dealer Dummy");

        var manager=LocManager.Instance;
        var tables=(Dictionary<string,LocTable>)Field(typeof(LocManager),"_tables").GetValue(manager)!;
        var table=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(RepositoryRoot,"HBRGOSakiMod/localization/zhs/relics.json")))!;
        tables["relics"]=new LocTable("relics",table,null!);
        Type descriptionPatch=Mod.GetType("HBRGOSakiMod.Core.Patches.HotaruRelicDescriptionPatch",true)!;
        LocString Rewrite(RelicModel relic,LocString original)
        {
            object?[] args=[relic,original]; descriptionPatch.GetMethod("Postfix",F)!.Invoke(null,args); return (LocString)args[1]!;
        }
        var original=new LocString("relics","NATIVE_ORIGINAL.description"); original.Add("ExtraDamage",3m);
        LocString rendered=Rewrite(dummy,original);
        Check(rendered.LocEntryKey=="STRIKE_DUMMY.hbrDescription","owned HBR Dummy selects coefficient description");
        Check((decimal)rendered.Variables["ExtraDamage"]==3m,"Dummy description retains bound native variables");
        Check(rendered.GetFormattedText().Contains("50%")&&!rendered.GetFormattedText().Contains('{'),"real native SmartFormat renders complete50percent Dummy rule");
        Mode(false); Check(ReferenceEquals(Rewrite(dummy,original),original),"legacy Dummy retains original description"); Mode(true);
        Check(ReferenceEquals(Rewrite(new StrikeDummy(),original),original),"canonical ownerless Dummy retains original description");
        var detached=new StrikeDummy(); Set(detached,"<IsMutable>k__BackingField",true);
        Check(ReferenceEquals(Rewrite(detached,original),original),"mutable detached Dummy retains original description");
        Check(original.LocEntryKey=="NATIVE_ORIGINAL.description"&&original.Variables.Count==1,"Dummy wrapper never mutates original description");
        ExtraListeners.Clear(); Set(Player,"_relics",new List<RelicModel>{Manual}); Reset();
    }
    static void FinalFloorCases()
    {
        Reset(); var multiplier=TestPower<FractionalMultiplier>(Hero); multiplier.Factor=20.5m;
        decimal baseAmount=Outgoing(100m,100m,150m);
        Check(baseAmount==3857.14m,"attribute adapter retains hundredth precision before final output");
        decimal expected=79071m;
        Equal(Preview(Enemy,Hero),expected,"allied fractional attribute result floors only after20point5 multiplier");
        Check(expected!=decimal.Floor(decimal.Floor(baseAmount)*20.5m),"final-only floor example distinguishes premature integer truncation");
        Equal(Actual(Enemy,Hero),expected,"production actual wrapper matches integer preview after all multipliers");
        var results=new List<DamageResult>(); Call("ItsMygoIncomingDamageCapture","Begin",results);
        try
        {
            Equal(Actual(Enemy,Hero),expected,"captured actual pipeline remains the same floored damage");
            object stack=Helper("ItsMygoIncomingDamageCapture").GetField("_scopes",F)!.GetValue(null)!;
            object scope=stack.GetType().GetMethod("Peek")!.Invoke(stack,null)!;
            object capture=scope.GetType().GetProperty("Captured",F)!.GetValue(scope)!;
            Equal((decimal)capture.GetType().GetProperty("Amount",F)!.GetValue(capture)!,expected,"raw-damage capture publishes floored final amount");
        }
        finally { Call("ItsMygoIncomingDamageCapture","End"); }
        Check((int)Helper("ItsMygoIncomingDamageCapture").GetProperty("ScopeDepth",F)!.GetValue(null)! == 0,"actual raw capture scope restores after floor test");
        var cap=TestPower<FractionalCap>(Enemy);
        Equal(Preview(Enemy,Hero),29m,"fractional target cap is applied before final integer floor");
        Equal(Actual(Enemy,Hero),29m,"actual fractional target cap matches floored preview");
        Powers(Enemy).Remove(cap);
        var millsage=new Millsage(); Set(millsage,"<IsMutable>k__BackingField",true); Set(millsage,"_owner",Player);
        Set(Player,"_relics",new List<RelicModel>{Manual,millsage});
        var cannon=Actor(Blank<HotaruContestCannon>(),owner:Player);
        Equal(Cannon(cannon,()=>Preview(Enemy,Hero)),39535m,"cannon Soul Power applies before final floor of79071point37 times0point5");
        Equal(Cannon(cannon,()=>Actual(Enemy,Hero)),39535m,"owner-sourced cannon actual matches final-floor preview");
        Set(Player,"_relics",new List<RelicModel>{Manual}); Reset();
        Equal(Preview(Hero,Enemy,4m),542m,"incoming final damage is floored after enemy defensive formula");
        Equal(Actual(Hero,Enemy,4m),542m,"incoming actual matches floored intent");
        Equal(Preview(Enemy,Hero,-0.5m,ValueProp.Unpowered),0m,"HBR nonpositive resolved damage floors with nonnegative boundary");
        Equal(Preview(Enemy,Hero,0.99m,ValueProp.Unpowered),0m,"HBR resolved fractional damage is floored at final boundary");
        Reset();
    }
    public static Task Run()
    {
        bool ok=false; string? error=null;
        try { Setup(); AlliedCases(); PercentageCases(); EnemyCases(); OwnershipCases(); PercentageOwnershipCases(); LegacyCases(); DescriptionCases(); StrikeDummyCases(); FinalFloorCases(); ok=true; }
        catch(Exception failure) { error=failure.ToString(); Console.Error.WriteLine(failure); }
        string report=System.Environment.GetEnvironmentVariable("HBR_STAT_REPORT_PATH")??Path.Combine(AppContext.BaseDirectory,"report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report,JsonSerializer.Serialize(new {
            ok, checks=Passed.Count, passed=Passed,error,
            modSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            localizationSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepositoryRoot,"HBRGOSakiMod/localization/zhs/powers.json")))),
            relicLocalizationSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(RepositoryRoot,"HBRGOSakiMod/localization/zhs/relics.json")))),
            runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            boundary="Real native Strength/Dexterity/Vigor methods, production Harmony power and damage/block adapters, full native Hook.ModifyDamage and Hook.ModifyBlock pipelines, current saved manual stats, local attack/defense snapshots, real Millsage state, active ordinary-turn longing copies, production cannon Begin/Close, actual StrongestShield wrapper and raw capture, and description wrapper with native LocManager/LocTable/SmartFormat. StringName/logging and hook-listener enumeration are isolated leaves. Final rounding uses explicit fixture multiplier/cap input powers. No Godot nodes, game process, rendered UI, save loading, complete card/cannon/temporary-power command lifecycle, package or upload."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" stat power checks");
        if(!ok) throw new Exception("Stat power checks failed; inspect report.json");
        return Task.CompletedTask;
    }
}
