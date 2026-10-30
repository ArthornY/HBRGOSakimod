using System.Reflection;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Capabilities;
using STS2RitsuLib.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
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
    static RunState RunState = null!;
    static CombatState Combat = null!;
    static Player Player = null!;
    static Creature Hero = null!, Enemy = null!;
    static ElectronicSoldierManual Manual = null!;
    static Type Helper(string name) => Mod.GetType("HBRGOSakiMod.Core.Helpers." + name, true)!;
    static object? Call(string helper, string name, params object?[] args) => Helper(helper).GetMethod(name, F)!.Invoke(null, args);
    static decimal Number(string helper, string name, params object?[] args) => System.Convert.ToDecimal(Call(helper, name, args),CultureInfo.InvariantCulture);
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
    static decimal NativeDamage(Creature target, Creature dealer, decimal raw, ValueProp props,
        CardModel? card, ModifyDamageHookType hookType)
    {
        Type compatibility=Mod.GetType("HBRGOSakiMod.Core.Compatibility.HookCompatibility",true)!;
        object?[] args=[RunState,Combat,target,dealer,raw,props,card,hookType,CardPreviewMode.None,null,null];
        return (decimal)compatibility.GetMethod("ModifyDamage",F)!.Invoke(null,args)!;
    }
    static decimal Preview(Creature target, Creature dealer, decimal raw = 6m, ValueProp props = ValueProp.Move, CardModel? card = null) =>
        NativeDamage(target,dealer,raw,props,card,ModifyDamageHookType.All);
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
    static bool Skip() => false;
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    { __result = Actors.SelectMany(c => c.Powers).Cast<AbstractModel>().ToArray(); return false; }
    static void Setup()
    {
        TestMode.IsOn = true; var h = new Harmony("hbr.fragile.native.checks");
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
        Set(Player,"<Character>k__BackingField",Blank<HBRGOSakiMod.Core.Models.Characters.HbrgoSaki>());
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
        foreach (string name in new[] { "HbrgoSakiAdditiveDamagePatch", "HbrStrengthAttributePatch", "HbrDexterityAttributePatch" })
        {
            Type type = Mod.GetType("HBRGOSakiMod.Core.Patches.Combat." + name, true)!;
            Check(h.CreateClassProcessor(type).Patch().Count > 0, "installs production " + name);
        }
        ModelDb.Inject(typeof(FragilePower));Set(ModelDb.Power<FragilePower>(),"<Id>k__BackingField",ModelDb.GetId(typeof(FragilePower)));
        var state=Blank<PlayerCombatState>();Set(state,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",state);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})
            Set(state,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(Player,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        InstallInstancePatches(h);
        string runtime=System.Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY")??"D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving+=(context,name)=>{string path=Path.Combine(runtime,name.Name+".dll");return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        h.Patch(typeof(MegaCrit.Sts2.Core.Commands.Cmd).GetMethod("CustomScaledWait")!,prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(ObserveWait),F)!));
        h.Patch(typeof(PowerModel).GetMethod("AfterRemoved")!,postfix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(ObserveRemoval),F)!));
        Reset();
    }
    static void InstallInstancePatches(Harmony h)
    {
        foreach(Type type in Mod.GetTypes().Where(t=>t.Namespace=="HBRGOSakiMod.Core.Patches.Combat"&&
            t.Name.StartsWith("HbrFragile",StringComparison.Ordinal)&&t.IsDefined(typeof(HarmonyPatch),false)))
            Check(h.CreateClassProcessor(type).Patch().Count>0,"installs production "+type.Name);
        Type scars=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HotaruUnfadingScarsDurationPatch",true)!;
        Check(h.CreateClassProcessor(scars).Patch().Count>0,"installs original Scars duration protection");
    }
    static decimal Effect(decimal raw,decimal intelligence,decimal luck,decimal enemy) =>
        Number("HbrFragilePowerHelper","EvaluatePercent",raw,intelligence,luck,enemy);
    static decimal ExpectedBase(int raw)
    {
        if(raw<=0)return 0m;
        decimal result=30m;for(int i=1;i<raw;i++)result*=1.25m;return result;
    }
    static decimal Expected(int raw,decimal intelligence,decimal luck,decimal enemy) =>
        ExpectedBase(raw)*(1m+Math.Max(0m,(intelligence+luck*2m)/3m-enemy)*.001m);
    static void NativePowerSignatureCases()
    {
        static bool Matches(Type type,string name,params Type[] parameters)=>
            type.GetMethod(name,BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static,null,parameters,null)!=null;
        Type context=typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),power=typeof(PowerModel),
            creature=typeof(Creature),card=typeof(CardModel),number=typeof(decimal),combat=typeof(ICombatState);
        Check(Matches(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd),"Apply",context,power,creature,number,creature,card,typeof(bool)),"runtime preserves exact seven-argument non-generic Fragile Apply boundary");
        Check(Matches(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd),"ModifyAmount",context,power,number,creature,card,typeof(bool)),"runtime preserves exact six-argument Fragile copy boundary");
        Check(Matches(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd),"FindExistingInstanceForStacking",power,creature,creature),"runtime preserves independent-instance lookup signature");
        Check(Matches(typeof(PowerModel),"BeforeApplied",creature,number,creature,card)&&Matches(typeof(PowerModel),"AfterApplied",creature,card)&&Matches(typeof(PowerModel),"ApplyInternal",creature,number,typeof(bool)),"runtime preserves all native direct Fragile lifecycle signatures");
        Check(Matches(typeof(Hook),"BeforePowerAmountChanged",combat,power,number,creature,creature,card)&&Matches(typeof(Hook),"AfterPowerAmountChanged",combat,context,power,number,creature,card),"runtime preserves before and after power change hook signatures");
        Type modifiers=typeof(IEnumerable<AbstractModel>).MakeByRefType();
        Check(Matches(typeof(Hook),"ModifyPowerAmountGiven",combat,power,creature,number,creature,card,modifiers)&&Matches(typeof(Hook),"ModifyPowerAmountReceived",combat,power,creature,number,creature,modifiers),"runtime preserves giver and Artifact receiver hook signatures");
        Type history=typeof(CombatManager).GetProperty("History",F)!.PropertyType;
        Check(Matches(history,"PowerReceived",combat,power,number,creature),"runtime preserves exact native PowerReceived history signature");
    }
    static void FormulaCases()
    {
        foreach(int raw in new[]{0,1,2,3,6,12})
        foreach(decimal intelligence in new[]{0m,100m,225m,10000m})
        foreach(decimal luck in new[]{0m,100m,300m,999m})
        foreach(decimal enemy in new[]{0m,150m,333m,1000m})
            Equal(Effect(raw,intelligence,luck,enemy),Expected(raw,intelligence,luck,enemy),
                $"one application strength{raw} INT{intelligence} LUCK{luck} E{enemy}");
        Equal(Effect(1,100,100,150),30,"below E retains base30 percent");
        Equal(Effect(2,100,100,150),37.5m,"strength2 base is37point5");
        Equal(Effect(3,100,100,150),46.875m,"strength3 base is46point875");
        Equal(Effect(6,100,100,150),91.552734375m,"combined strength6 is calculated once");
        Equal(Effect(2,250,250,150),41.25m,"strength2 converts before attribute coefficient");
        Equal(Effect(1,300,0,150),30,"INT weight is one third");
        Equal(Effect(1,0,300,150),31.5m,"LUCK weight is two thirds");
        Equal(Effect(1,10000,10000,150),325.5m,"no upper skill endpoint or hard upper cap");
        Equal(Effect(-5,100,100,150),0,"nonpositive raw strength creates no effect");
    }
    static FragilePower? Apply(int raw,Creature? source=null,Creature? target=null,CardModel? card=null,bool direct=false,bool silent=true)
    {
        target??=Enemy;
        if(!direct)return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<FragilePower>(null!,target,raw,source,card,silent).GetAwaiter().GetResult();
        var power=(FragilePower)ModelDb.Power<FragilePower>().ToMutable();
        MegaCrit.Sts2.Core.Commands.PowerCmd.Apply(null!,power,target,raw,source,card,silent).GetAwaiter().GetResult();return power;
    }
    static FragilePower[] Active(Creature? target=null)=>(target??Enemy).Powers.OfType<FragilePower>().Where(p=>p.Amount>0).ToArray();
    static decimal Percent(FragilePower power)=>(decimal)typeof(FragilePower).GetProperty("HbrEffectPercent",F)!.GetValue(power)!;
    static decimal Multiplier(FragilePower power,Creature? dealer=null,Creature? target=null,decimal damage=10) =>
        power.ModifyDamageMultiplicative(target??power.Owner,damage,ValueProp.Move,dealer??Hero,null!);
    static void Tick(FragilePower power) => MegaCrit.Sts2.Core.Commands.PowerCmd.TickDownDuration(power).GetAwaiter().GetResult();
    static void Turn(FragilePower power,CombatSide side,IEnumerable<Creature> participants)=>power.AfterSideTurnEnd(null!,side,participants).GetAwaiter().GetResult();
    static void InstanceCases()
    {
        Reset();var first=Apply(2,Hero)!;
        Check(ReferenceEquals(first.Owner,Enemy)&&first.Amount==2,"real native Apply uses fixed two-turn duration");
        Equal(Percent(first),37.5m,"first application snapshots converted strength2");
        Check(first.InstanceType==MegaCrit.Sts2.Core.Entities.Powers.PowerInstanceType.Instanced,"owned HBR Fragile is genuinely instanced");
        var second=Apply(3,Hero)!;
        Check(Active().Length==2&&!ReferenceEquals(first,second)&&first.Amount==2&&second.Amount==2,"second real Apply makes independent second instance");
        Equal(Percent(second),46.875m,"second application has independent stronger effect");
        Power<BreakPower>(Enemy,1);
        Equal(Preview(Enemy,Hero),decimal.Floor(Outgoing(100,100,150)*(1+.375m+.46875m)),"both independent effects add in target debuff bucket");
        Tick(first);Check(first.Amount==1&&second.Amount==2,"tick affects only selected instance duration");
        Equal(Percent(first),37.5m,"effect does not decay when duration decrements");
        Apply(1,Hero);
        Check(Active().Length==2&&Powers(Enemy).Contains(first)&&Powers(Enemy).Contains(second)&&first.Amount==1,"weaker third application is discarded without duration reset");
        var third=Apply(2,Hero)!;
        Check(Active().Length==2&&!Powers(Enemy).Contains(first)&&Powers(Enemy).Contains(second)&&Powers(Enemy).Contains(third),"equal third replaces weakest existing instance");
        Check(third.Amount==2&&second.Amount==2,"replacement resets only new instance to two turns");
        var strong=Apply(6,Hero)!;
        Check(Active().Length==2&&!Powers(Enemy).Contains(third)&&Powers(Enemy).Contains(second)&&Powers(Enemy).Contains(strong),"stronger third replaces lowest effect rather than strongest");
        Equal(Percent(strong),91.552734375m,"strong replacement preserves full precision strength6");
        Tick(second);Tick(second);
        Check(!Powers(Enemy).Contains(second)&&strong.Amount==2&&Active().Length==1,"independent expiry removes only expired instance");
        Turn(strong,CombatSide.Player,new[]{Enemy});Check(strong.Amount==2,"other-side end cannot tick instance");
        Turn(strong,CombatSide.Enemy,new[]{Hero});Check(strong.Amount==2,"nonparticipant cannot tick instance");
        Turn(strong,CombatSide.Enemy,new[]{Enemy});Check(strong.Amount==1,"real owner turn end decrements duration once");
        Turn(strong,CombatSide.Enemy,new[]{Enemy});Check(Active().Length==0,"second owner turn end expires instance through real native removal");
        Reset();var a=Apply(2,Hero)!;Tick(a);var b=Apply(2,Hero)!;var c=Apply(2,Hero)!;
        Check(!Powers(Enemy).Contains(a)&&Powers(Enemy).Contains(b)&&Powers(Enemy).Contains(c),"equal effect tie replaces shortest remaining duration");
        Reset();Apply(1,Hero);Apply(1,Hero);Power<BreakPower>(Enemy,1);
        Check(Active().Length==2,"two separate strength1 applications remain separate instances");
        Equal(Preview(Enemy,Hero),decimal.Floor(Outgoing(100,100,150)*1.6m),"two strength1 effects sum60 rather than recompute strength2 as37point5");
        Reset();var explicitPower=Apply(6,Hero,direct:true)!;
        Check(Powers(Enemy).Contains(explicitPower)&&explicitPower.Amount==2,"non-generic native Apply uses same instance conversion");
        Equal(Percent(explicitPower),91.552734375m,"non-generic native Apply snapshots one combined raw6");Reset();
    }
    static void SourceCases()
    {
        Reset();Sense("Talent",75);Sense("Simplicity",75);
        var power=Apply(2,Hero)!;Equal(Percent(power),41.25m,"application snapshots actual INT250 LUCK250 E150");
        Sense("Talent",0);Sense("Simplicity",0);Act(2);Equal(Percent(power),41.25m,"source stats and encounter changes do not rescale existing instance");Act(0);
        var enemy=Apply(2,Enemy)!;Equal(Percent(enemy),37.5m,"enemy source uses base without fabricated six stats");
        Reset();var absent=Apply(3)!;Equal(Percent(absent),46.875m,"absent source retains converted base effect");
        Reset();Sense("Talent",75);Sense("Simplicity",75);
        var card=new DefendIronclad();Set(card,"<IsMutable>k__BackingField",true);Set(card,"_owner",Player);
        Equal(Percent(Apply(2,card:card)!),41.25m,"null applier uses actual card owner attributes");
        Equal(Percent(Apply(2,Enemy,card:card)!),37.5m,"explicit enemy applier takes precedence over high-stat card owner");
        Reset();Sense("Talent",75);Sense("Simplicity",75);var cannon=Actor(Blank<HotaruContestCannon>(),owner:Player);
        Equal(Percent(Apply(2,cannon)!),41.25m,"cannon application reads owner current stats exactly once");
        var phantom=Actor(Blank<SmallHopper>(),owner:Player);var p=Power<PhantomPower>(phantom,1);
        Set(p,"<IsSummonedMinion>k__BackingField",true);Set(p,"<SummonedMinionLevel>k__BackingField",7);Power<DexterityPower>(phantom,3);
        Equal(Percent(Apply(1,phantom)!),33.39m,"phantom application uses own level and intelligence buff");Reset();
    }
    sealed class TargetDefenseDown : PowerModel
    {
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Counter;
#if HBR_BETA_NATIVE
        public override decimal ModifyDamageMultiplicative(Creature? target,decimal damage,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)=>
#else
        public override decimal ModifyDamageMultiplicative(Creature? target,decimal damage,ValueProp props,Creature? dealer,CardModel? card)=>
#endif
            target==Owner?1.4m:1;
    }
    static void PipelineCases()
    {
        Reset();var first=Apply(2,Hero)!;var second=Apply(3,Hero)!;Power<BreakPower>(Enemy,1);
        var ally=TestPower<FractionalMultiplier>(Hero);ally.Factor=1.5m;TestPower<TargetDefenseDown>(Enemy);
        decimal factor=1+.375m+.46875m+.4m;
        decimal expected=decimal.Floor(Outgoing(100,100,150)*1.5m*factor);
        Equal(Preview(Enemy,Hero),expected,"both Fragile instances add with other defense-down outside allied buff");
        Equal(Actual(Enemy,Hero),expected,"actual and preview share independent-instance target debuff bucket");
        string[] saved=SnapshotSave(first);
        for(int i=0;i<12;i++)
        {
            Equal(Preview(Enemy,Hero),expected,"repeated native preview stable"+i);
            Check(first.Amount==2&&second.Amount==2&&saved.SequenceEqual(SnapshotSave(first)),"preview changes no durations or saved effects"+i);
        }
        var raw=new List<DamageResult>();Call("ItsMygoIncomingDamageCapture","Begin",raw);
        try
        {
            Equal(Actual(Enemy,Hero),expected,"raw capture retains actual result");
            object stack=Helper("ItsMygoIncomingDamageCapture").GetField("_scopes",F)!.GetValue(null)!;
            object scope=stack.GetType().GetMethod("Peek")!.Invoke(stack,null)!;object capture=scope.GetType().GetProperty("Captured",F)!.GetValue(scope)!;
            Equal((decimal)capture.GetType().GetProperty("Amount",F)!.GetValue(capture)!,expected,"raw ledger records final floored damage");
        }
        finally{Call("ItsMygoIncomingDamageCapture","End");}
        ally.Factor=20.5m;expected=decimal.Floor(Outgoing(100,100,150)*20.5m*factor);
        Equal(Preview(Enemy,Hero),expected,"precision retained until final floor across both instances");Equal(Actual(Enemy,Hero),expected,"actual floors only after all multipliers");
        Check(expected!=decimal.Floor(decimal.Floor(Outgoing(100,100,150))*20.5m*factor),"selected fractional example distinguishes premature floor");
        TestPower<FractionalCap>(Enemy);Equal(Preview(Enemy,Hero),29,"cap precedes final floor");Equal(Actual(Enemy,Hero),29,"actual cap agrees with preview");Reset();
    }
    static PropertyInfo[] SavedProperties()=>typeof(FragilePower).GetProperties(F).Where(p=>p.DeclaringType==typeof(FragilePower)&&p.GetCustomAttributes().Any(a=>a.GetType().Name=="SavedPropertyAttribute")).OrderBy(p=>p.Name).ToArray();
    static string[] SnapshotSave(FragilePower power)=>SavedProperties().Select(p=>p.Name+":"+JsonSerializer.Serialize(p.GetValue(power))).ToArray();
    static void SaveCases()
    {
        Reset();Sense("Talent",75);Sense("Simplicity",75);var power=Apply(3,Hero)!;Tick(power);
        var props=SavedProperties();Check(props.Length>0,"instance effect has explicit saved properties");
        var cloned=(FragilePower)power.ClonePreservingMutability();
        Check(cloned.IsMutable&&cloned.Owner==null,"real native mutable clone clears ownership");
        Check(SnapshotSave(cloned).SequenceEqual(SnapshotSave(power)),"real clone preserves primitive exact effect and authored strength fields");
        typeof(FragilePower).GetProperty("HbrEffectData",F)!.SetValue(cloned,"5");
        typeof(FragilePower).GetProperty("HbrModifierCount",F)!.SetValue(cloned,99);
        Equal(Percent(power),51.5625m,"changing cloned effect never changes original stored snapshot");
        Check(power.HbrModifierCount==3&&power.Amount==1,"changing cloned authored strength leaves original strength and duration untouched");
        var restored=Power<FragilePower>(Enemy,1);foreach(var prop in props)prop.SetValue(restored,prop.GetValue(power));
        Equal(Percent(restored),51.5625m,"saved percent restores exact pre-rounded effect independently of duration");
        Check(restored.Amount==1,"restored native amount is remaining duration");Sense("Talent",0);Sense("Simplicity",0);
        Equal(Percent(restored),51.5625m,"restored effect independent of later source attributes");
        Check(SnapshotSave(power).SequenceEqual(SnapshotSave(restored)),"saved property roundtrip retains exact data");
        Power<BreakPower>(Enemy,1);Equal(Multiplier(restored),1.515625m,"restored single instance resolves exact percentage");
        Set(restored,"_amount",0);Equal(Multiplier(restored),1,"zero duration contributes no effect");
        Set(restored,"_amount",-1);Equal(Multiplier(restored),1,"negative duration contributes no effect");Reset();
    }
    static void ScarsAndBlockedCases()
    {
        Reset();var first=Apply(2,Hero)!;var second=Apply(3,Hero)!;Power<BreakPower>(Enemy,1);
        var scars=Power<HotaruUnfadingScarsPower>(Hero,1);typeof(HotaruUnfadingScarsPower).GetProperty("EffectPercent",F)!.SetValue(scars,12);
        Equal(Multiplier(first),1.42m,"existing Scars multiplies first instance effect once");Equal(Multiplier(second),1.525m,"existing Scars multiplies second instance effect once");
        Turn(first,CombatSide.Enemy,new[]{Enemy});Turn(second,CombatSide.Enemy,new[]{Enemy});
        Check(first.Amount==2&&second.Amount==2,"real native duration route respects existing Scars decay protection per instance");
        Equal(Preview(Enemy,Hero),decimal.Floor(Outgoing(100,100,150)*(1+(.375m+.46875m)*1.12m)),"Scars and both instance offsets contribute exactly once");
        Reset();var artifact=Power<ArtifactPower>(Enemy,1);Apply(6,Hero);
        Check(Active().Length==0,"real Artifact blocks new Fragile application");
        Check(artifact.Amount==0&&!Powers(Enemy).Contains(artifact),"real received hook consumes Artifact exactly once");
        Reset();var incoming=Apply(2,Enemy,Hero)!;
        Check(incoming.Amount==2&&!incoming.SkipNextDurationTick,"HBR player duration starts at two without legacy enemy-end skip");
        incoming.SkipNextDurationTick=true;
        Turn(incoming,CombatSide.Player,new[]{Hero});Check(incoming.Amount==1&&!incoming.SkipNextDurationTick,"first owner end clears legacy skip and consumes one duration");
        Turn(incoming,CombatSide.Player,new[]{Hero});Check(Active(Hero).Length==0,"player instance expires independently at the second actual owner end");Reset();
    }
    static void LegacyCases()
    {
        Reset();Mode(false);var first=Apply(2,Hero)!;var second=Apply(3,Hero)!;
        Check(ReferenceEquals(first,second)&&first.Amount==5&&Active().Length==1,"legacy native applications preserve stacking and authored raw amount");
        Check(ReferenceEquals(MegaCrit.Sts2.Core.Commands.PowerCmd.FindExistingInstanceForStacking(ModelDb.Power<FragilePower>(),Enemy,Hero),first),"legacy native lookup finds original instance for stacking");
        Power<BreakPower>(Enemy,1);Equal(Multiplier(first),1.5m,"legacy fixed50 percent remains independent of amount");Equal(Preview(Enemy,Hero),9,"legacy damage6 still resolves9");
        Turn(first,CombatSide.Enemy,new[]{Enemy});Check(first.Amount==4,"legacy duration decrements original raw amount");Reset();
    }
    static void CopyAndCompatibilityCases()
    {
        Reset();Sense("Talent",75);Sense("Simplicity",75);var first=Apply(3,Hero)!;Tick(first);
        Equal(Number("HbrFragilePowerHelper","GetModifierAmount",Enemy),3,"synergy count is authored strength3 even when duration is1");
        Sense("Talent",0);Sense("Simplicity",0);
        MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(null!,first,100,Enemy,null!,true).GetAwaiter().GetResult();
        var copies=Active();Check(copies.Length==2&&first.Amount==1,"positive native ModifyAmount copies effect instead of adding100 duration");
        var copied=copies.Single(p=>!ReferenceEquals(p,first));Check(copied.Amount==2,"copied instance starts independent fixed two-turn duration");
        Equal(Percent(copied),51.5625m,"copy preserves original high-stat snapshot despite different low-stat applier");
        Equal(Number("HbrFragilePowerHelper","GetModifierAmount",Enemy),6,"synergy count sums original and copied authored strengths rather than duration3");
        MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(null!,copied,1,Enemy,null!,true).GetAwaiter().GetResult();
        Check(Active().Length==2&&!Powers(Enemy).Contains(first)&&Powers(Enemy).Contains(copied),"third copy follows equal-effect shortest-duration replacement");
        Equal(Number("HbrFragilePowerHelper","GetModifierAmount",Enemy),6,"replacing one copy does not inflate compatibility strength count");
        Reset();var weak=Apply(2,Hero)!;var strong=Apply(3,Hero)!;var artifact=Power<ArtifactPower>(Enemy,1);
        Apply(6,Hero);
        Check(Active().Length==2&&Powers(Enemy).Contains(weak)&&Powers(Enemy).Contains(strong),"Artifact blocks before either occupied slot is removed");
        Check(weak.Amount==2&&strong.Amount==2&&artifact.Amount==0,"blocked stronger application neither refreshes old durations nor consumes extra Artifact");
        Reset();Apply(0,Hero);Apply(-1,Hero);Check(Active().Length==0,"zero or negative raw strength creates no duration instance");
        Equal(Number("HbrFragilePowerHelper","BasePercent",(decimal)int.MaxValue),decimal.MaxValue,"huge authored strength saturates with bounded exponentiation");
        Equal(Effect(int.MaxValue,10000,10000,150),decimal.MaxValue,"huge strength and attribute factor saturate without decimal overflow");
        Equal(Number("HbrFragilePowerHelper","GetModifierAmount",(object?)null),0,"absent target has zero compatibility count");
        Mode(false);Apply(7,Hero);Equal(Number("HbrFragilePowerHelper","GetModifierAmount",Enemy),7,"legacy synergy count retains raw duration7");
        Reset();
    }
    static T OwnedCard<T>() where T:CardModel,new()
    {
        var card=new T();Set(card,"<IsMutable>k__BackingField",true);Set(card,"_owner",Player);
        card.UpgradePreviewType=CardUpgradePreviewType.Combat;return card;
    }
    static void Play(CardModel card,Creature? target=null)
    {
        var play=new CardPlay{Card=card,Target=target,ResultPile=PileType.Discard,
#if HBR_BETA_NATIVE
            Player=Player,
#endif
            Resources=new ResourceInfo{EnergySpent=card.EnergyCost.Canonical,EnergyValue=card.EnergyCost.Canonical,StarsSpent=0,StarValue=0},
            IsAutoPlay=false,PlayIndex=0,PlayCount=1};
        ((Task)card.GetType().GetMethod("OnPlay",F)!.Invoke(card,new object?[]{null,play})!).GetAwaiter().GetResult();
    }
    static void Transform(FragilePower power,bool duration=true,CardModel? source=null)
        =>((Task)Call("HbrFragileApplication","DoubleExisting",null,power,Percent(power),power.Amount,
            Hero,source,duration,true)!).GetAwaiter().GetResult();
    static bool SkipTransformAttack(AttackCommand __instance,ref Task<AttackCommand> __result)
    {__result=Task.FromResult(__instance);return false;}
    static void DoubleExistingCases()
    {
        Reset();var first=Apply(2,Enemy)!;var second=Apply(3,Hero)!;Tick(first);
        var originalApplier=first.Applier;var card=OwnedCard<DoublePowersHbrgoSaki>();Play(card);
        Check(Active().Length==2&&Powers(Enemy).Contains(first)&&Powers(Enemy).Contains(second),"actual Double preserves both existing Fragile slots and identities");
        Check(first.Amount==2&&second.Amount==4,"actual Double doubles each independent remaining duration rather than resetting to two");
        Equal(Percent(first),75m,"actual Double doubles first37point5 effect to75");
        Equal(Percent(second),93.75m,"actual Double doubles second46point875 effect to93point75");
        Check(first.HbrModifierCount==2&&second.HbrModifierCount==3&&ReferenceEquals(first.Applier,originalApplier),"actual Double preserves authored modifier strengths and original applier");
        Equal(Number("HbrFragilePowerHelper","GetModifierAmount",Enemy),5,"Double never reinterprets new durations as authored modifier strength");
        Turn(first,CombatSide.Enemy,new[]{Enemy});Turn(second,CombatSide.Enemy,new[]{Enemy});
        Check(first.Amount==1&&second.Amount==3,"transformed four-turn instance ticks to three without legacy two-turn reclamp");
        Play(card);Check(first.Amount==2&&second.Amount==6&&Active().Length==2,"repeat Double extends existing remaining durations without occupying more slots");
        Equal(Percent(first),150,"repeat Double retains exact doubled first effect");Equal(Percent(second),187.5m,"repeat Double retains exact doubled second effect");
        Reset();Sense("Talent",75);Sense("Simplicity",75);first=Apply(3,Hero)!;Sense("Talent",0);Sense("Simplicity",0);
        Transform(first,false);Equal(Percent(first),103.125m,"effect-only transform doubles original high-stat snapshot without recalculating low current attributes");
        Check(first.Amount==2&&first.HbrModifierCount==3,"effect-only transform preserves remaining duration and authored strength");
        Reset();first=Apply(2,Hero)!;Tick(first);var artifact=Power<ArtifactPower>(Enemy,1);string[] unchanged=SnapshotSave(first);
        Transform(first);Check(first.Amount==1&&unchanged.SequenceEqual(SnapshotSave(first)),"Artifact rejects Double atomically without percent or duration change");
        Check(artifact.Amount==0&&!Powers(Enemy).Contains(artifact),"Double preserves actual native Artifact receiving and consumption hooks");
        Reset();first=Apply(3,Hero)!;artifact=Power<ArtifactPower>(Enemy,1);Transform(first,false);
        Check(first.Amount==2&&Percent(first)==46.875m&&!Powers(Enemy).Contains(artifact),"Artifact also blocks effect-only transform without changing duration");
        Reset();first=Apply(2,Hero)!;Tick(first);typeof(FragilePower).GetProperty("HbrEffectData",F)!.SetValue(first,decimal.MaxValue.ToString(CultureInfo.InvariantCulture));
        Transform(first);Equal(Percent(first),decimal.MaxValue,"huge transformed percentage saturates rather than overflowing");
        Reset();Mode(false);first=Apply(2,Hero)!;Play(OwnedCard<DoublePowersHbrgoSaki>());
        Check(first.Amount==4&&Active().Length==1,"legacy Double preserves native single-instance amount doubling");
        Power<BreakPower>(Enemy,1);Equal(Multiplier(first),1.5m,"legacy Double retains original fixed Fragile effect");Reset();

        var h=new Harmony("hbr.fragile.real.weakness.transform");
        h.Patch(AccessTools.Method(typeof(AttackCommand),"Execute"),prefix:new HarmonyMethod(typeof(Checks).GetMethod(nameof(SkipTransformAttack),F)!));
        try
        {
            first=Apply(2,Hero)!;second=Apply(3,Hero)!;Tick(first);var other=Actor(Blank<SmallHopper>());
            Set(Combat,"_enemies",new List<Creature>{Enemy,other});var untouched=Apply(6,Hero,other)!;
            Play(OwnedCard<FragileDoubleStrikeHbrgoSaki>(),Enemy);
            Check(Active().Length==2&&Powers(Enemy).Contains(first)&&Powers(Enemy).Contains(second),"actual Weakness Strike transforms each target instance without a new application");
            Equal(Percent(first),75,"actual Weakness Strike doubles first target effect");Equal(Percent(second),93.75m,"actual Weakness Strike doubles second target effect");
            Check(first.Amount==1&&second.Amount==2&&first.HbrModifierCount==2&&second.HbrModifierCount==3,"actual Weakness Strike preserves each independent duration and authored strength");
            Equal(Percent(untouched),91.552734375m,"actual Weakness Strike never aggregates or modifies other enemies' Fragile");
            Check(untouched.Amount==2,"other enemy duration is unchanged by Weakness Strike");
            Reset();Play(OwnedCard<FragileDoubleStrikeHbrgoSaki>(),Enemy);Check(Active().Length==0,"Weakness Strike cannot invent a Fragile instance on a clean target");
        }
        finally{h.UnpatchAll(h.Id);Set(Combat,"_enemies",new List<Creature>{Enemy});Reset();}
    }
    static void OldSaveCases()
    {
        Reset();Sense("Talent",75);Sense("Simplicity",75);var old=Power<FragilePower>(Enemy,6);Set(old,"_applier",Hero);
        Equal(Percent(old),100.7080078125m,"old raw-only save falls back from authored strength6 and original applier");
        string[] before=SnapshotSave(old);Power<BreakPower>(Enemy,1);
        for(int i=0;i<10;i++)Preview(Enemy,Hero);
        Check(old.Amount==6&&before.SequenceEqual(SnapshotSave(old)),"old-save preview never changes duration or materializes stored effect");
        Turn(old,CombatSide.Enemy,new[]{Enemy});
        Check(old.Amount==1&&old.HbrModifierCount==6,"first lifecycle tick migrates old raw6 into fixed2 duration then decrements once");
        Equal(Percent(old),100.7080078125m,"old-save lifecycle migration snapshots effect exactly");
        Sense("Talent",0);Sense("Simplicity",0);Equal(Percent(old),100.7080078125m,"migrated effect stays independent of later attribute changes");
        Reset();
    }
    static bool TrackingWaits;
    static int WaitCalls,RemovalCallbacks;
    static bool ObserveWait(ref Task __result)
    {
        if(!TrackingWaits)return true;
        WaitCalls++;__result=Task.CompletedTask;return false;
    }
    static void ObserveRemoval(PowerModel __instance,Creature oldOwner)
    {
        if(TrackingWaits&&__instance is FragilePower)RemovalCallbacks++;
    }
    static FragileHbrgoSaki TestCard()
    {
        var card=new FragileHbrgoSaki();Set(card,"<IsMutable>k__BackingField",true);Set(card,"_owner",Player);
        typeof(CardModel).GetProperty("UpgradePreviewType",F)!.SetValue(card,CardUpgradePreviewType.Combat);return card;
    }
    static void NararaScopeCases()
    {
        Reset();var boss=Actor(Blank<NararaShibaBossMonster>());Set(Combat,"_enemies",new List<Creature>{boss});
        try
        {
            var nullification=Power<NararaNullificationPower>(boss,1);var resistance=Power<SixSenseResistancePower>(boss,1);
            typeof(SixSenseResistancePower).GetProperty("Resistances",F)!.SetValue(resistance,new[]{10,10,10,10,10,10});
            Check((bool)typeof(NararaNullificationPower).GetProperty("IsActive",F)!.GetValue(nullification)!,"real Narara boss nullification is active");
            var card=TestCard();ModelCapabilities.EnsureInitialized();ModelCapabilityRegistry.Register<SixSenseCardCapability>("fixture.fragile.six-sense");
            var capability=ModelCapabilityRegistry.Create<SixSenseCardCapability>();Set(capability,"_attribute",SixSenseAttribute.Simplicity);
            card.Capabilities().Add(capability,false,false);
            Check(ReferenceEquals(card.Capability<SixSenseCardCapability>(),capability),"real Ritsu capability is attached to source card");
            var weak=Apply(2,Hero,boss,card)!;var strong=Apply(3,Hero,boss,card)!;
            Check(Active(boss).Length==2,"status application outside a real card scope retains normal behavior despite preview nullification");
            var scope=Call("NararaNullificationHelper","Begin",card)!;
            try
            {
                var targets=(HashSet<Creature>)scope.GetType().GetField("Targets",F)!.GetValue(scope)!;
                Check(targets.Contains(boss),"real Begin CaptureTargets records active boss from real SixSense capability and resistance");
                Check((bool)Call("NararaNullificationHelper","IsCardNullified",boss,card)!,"actual active scope nullifies its captured card target");
                var artifact=Power<ArtifactPower>(boss,1);
                Transform(weak,true,card);
                Check(weak.Amount==2&&Percent(weak)==37.5m&&artifact.Amount==1,"real Narara scope blocks existing-effect Double before either value changes or Artifact is consumed");
                Transform(strong,false,card);
                Check(strong.Amount==2&&Percent(strong)==46.875m&&artifact.Amount==1,"real Narara scope also blocks effect-only transform");
                Apply(6,Hero,boss,card);
                Check(Active(boss).Length==2&&Powers(boss).Contains(weak)&&Powers(boss).Contains(strong),"blocked strong application leaves both previous instances intact");
                Check(weak.Amount==2&&strong.Amount==2&&artifact.Amount==1,"nullification blocks before durations reset or Artifact is consumed");
                Apply(6,Hero,boss);
                Check(Active(boss).Length==2&&Powers(boss).Contains(weak)&&Powers(boss).Contains(strong),"source-less command in active card scope is also nullified");
                Powers(boss).Remove(artifact);
                var unrelated=TestCard();Apply(6,Hero,boss,unrelated);
                Check(Active(boss).Length==2&&!Powers(boss).Contains(weak)&&Powers(boss).Contains(strong),"unrelated card source does not inherit active outer card nullification");
            }
            finally{Call("NararaNullificationHelper","Close",scope);}
            Apply(6,Hero,boss,card);
            Check(Active(boss).Length==2&&!Powers(boss).Contains(strong),"closing actual scope restores originating card status application");
        }
        finally{Set(Combat,"_enemies",new List<Creature>{Enemy});Reset();}
    }
    static void AutoPlayScopeCases()
    {
        Reset();var card=TestCard();var unrelated=TestCard();var increases=new List<(PowerModel power,bool silent)>();
        void Increased(PowerModel power,int amount,bool silent){if(power is FragilePower)increases.Add((power,silent));}
        Enemy.PowerIncreased+=Increased;TrackingWaits=true;WaitCalls=0;RemovalCallbacks=0;
        try
        {
            using((IDisposable)Call("FragileAutoPlayAnimationHelper","Suppress",card)!)
            {
                Check((bool)Call("FragileAutoPlayAnimationHelper","IsSuppressed",card)!,"real autoplay suppression scope is active for originating card");
                var first=Apply(2,Hero,card:card,silent:false)!;
                Check(WaitCalls==0&&increases.Single(row=>ReferenceEquals(row.power,first)).silent,"new scoped application skips animation wait and emits silent native increase");
                var second=Apply(3,Hero,card:card,silent:false)!;
                Check(WaitCalls==0&&first.Amount==2&&second.Amount==2,"second scoped application adds second full instance without wait");
                int removedEvents=0;first.Removed+=()=>removedEvents++;
                var third=Apply(6,Hero,card:card,silent:false)!;
                Check(WaitCalls==0&&!Powers(Enemy).Contains(first)&&Powers(Enemy).Contains(second)&&Powers(Enemy).Contains(third),"scoped replacement skips both remove and apply waits while replacing correct instance");
                Check(removedEvents==1&&RemovalCallbacks==1,"fast replacement retains native Removed event and AfterRemoved callback exactly once");
                Check(increases.Where(row=>ReferenceEquals(row.power,third)).Single().silent,"scoped replacement also emits silent native increase");
                var ordinary=Apply(6,Hero,card:unrelated,silent:false)!;
                Check(WaitCalls==2&&Powers(Enemy).Contains(ordinary),"unrelated source within scope keeps one native removal wait and one apply wait");
                Check(!increases.Single(row=>ReferenceEquals(row.power,ordinary)).silent,"unrelated source retains authored non-silent native increase");
                Check((bool)Call("FragileAutoPlayAnimationHelper","IsSuppressed",card)!&&!(bool)Call("FragileAutoPlayAnimationHelper","IsSuppressed",unrelated)!,"unrelated source preserves and does not expand original suppression scope");
            }
            Check(!(bool)Call("FragileAutoPlayAnimationHelper","IsSuppressed",card)!,"disposed scope no longer suppresses originating card");
            int waits=WaitCalls;var outside=Apply(6,Hero,card:card,silent:false)!;
            Check(WaitCalls==waits+2&&!increases.Single(row=>ReferenceEquals(row.power,outside)).silent,"originating card after scope again uses normal waits and non-silent native mutation");
        }
        finally{Enemy.PowerIncreased-=Increased;TrackingWaits=false;Reset();}
    }
    static void DescriptionCases()
    {
        Reset();string runtime=System.Environment.GetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY")??"D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving+=(context,name)=>{string path=Path.Combine(runtime,name.Name+".dll");return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var table=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"HBRGOSakiMod/localization/zhs/powers.json")))!;
        Set(manager,"_tables",new Dictionary<string,LocTable>{["powers"]=new LocTable("powers",table,null!)});typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
        foreach(var row in new[]{(2,"37.5"),(3,"46.9"),(6,"91.6")})
        {
            Reset();var power=Apply(row.Item1,Hero)!;var text=power.Description;
            Check((string)text.Variables["FragilePercent"]==row.Item2,"description rounds displayed effect to one decimal for strength"+row.Item1);
            Check(!text.GetFormattedText().Contains('{')&&text.GetFormattedText().Contains(row.Item2),"native SmartFormat renders actual per-instance percentage"+row.Item1);
            Equal(Percent(power),ExpectedBase(row.Item1),"display rounding never changes stored effect"+row.Item1);
            Equal((decimal)text.Variables["ModifierCount"],row.Item1,"description binds original modifier strength independently of duration"+row.Item1);
            Equal((decimal)text.Variables["Turns"],2,"new instance description binds two remaining turns"+row.Item1);
            Tick(power);LocString ticked=power.Description;
            Equal((decimal)ticked.Variables["ModifierCount"],row.Item1,"duration tick never rewrites described modifier strength"+row.Item1);
            Equal((decimal)ticked.Variables["Turns"],1,"description dynamically tracks one remaining turn"+row.Item1);
            Check(!ticked.GetFormattedText().Contains('{'),"all current description placeholders render after duration tick"+row.Item1);
        }
        Reset();Mode(false);var legacy=Apply(2,Hero)!;Check(legacy.Description.LocEntryKey==legacy.Id.Entry+".description","legacy description keeps original entry");
        Check(!legacy.Description.GetFormattedText().Contains('{'),"legacy description still renders");Mode(true);
        var canonical=ModelDb.Power<FragilePower>();Check(canonical.Description.LocEntryKey==canonical.Id.Entry+".hbrReferenceDescription","canonical reference exposes HBR rule");
        Check(!canonical.Description.GetFormattedText().Contains('{'),"canonical reference contains no unresolved placeholders");
        var unowned=(FragilePower)canonical.ToMutable();
        Check(unowned.Description.LocEntryKey==canonical.Id.Entry+".hbrReferenceDescription"&&!unowned.Description.GetFormattedText().Contains('{'),"new unowned mutable power renders reference without unresolved placeholders");Reset();
    }
    public static Task Run()
    {
        bool ok=false;string? error=null;
        try{Setup();NativePowerSignatureCases();FormulaCases();InstanceCases();SourceCases();PipelineCases();SaveCases();ScarsAndBlockedCases();LegacyCases();CopyAndCompatibilityCases();DoubleExistingCases();ScarsChecks.Run();OldSaveCases();DescriptionCases();NararaScopeCases();CapAndDoomChecks.Run();AutoPlayScopeCases();ok=true;}
        catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"report.json"),JsonSerializer.Serialize(new{
            ok,checks=Passed.Count,passed=Passed,error,
            modSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),
            nativeSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            localizationSha256=System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../HBRGOSakiMod/localization/zhs/powers.json"))))),
            runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE")??"stable",
            boundary="Exact candidate managed fixture: real native PowerCmd generic/non-generic Apply, independent duration/tick/remove, Artifact received hooks, production Fragile application boundaries, full native Hook and actual StrongestShield wrapper/raw ledger, real attributes and native LocManager/LocTable/SmartFormat. Real Narara Begin/Close scopes use an attached SixSense capability and active resistance. Actual autoplay Suppress scopes test wait call counts and native silence/Removed events; Cmd.CustomScaledWait is a controlled counted leaf only during those cases. StringName/logging and hook enumeration are isolated leaves. Property roundtrip covers stored effect, not full save-file loading. No game process, rendered UI, package or upload."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" HBR fragile checks");if(!ok)throw new Exception("HBR fragile checks failed; inspect report.json");return Task.CompletedTask;
    }
}
