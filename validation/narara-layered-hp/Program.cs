using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using Environment=System.Environment;

static class NararaHpChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(NararaShibaBossMonster).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Results=[];
    static readonly Dictionary<string,string> Sources=[];
    static readonly List<string> Patches=[];
    static readonly List<decimal> HpDeltas=[];
    static string LocalizationRoot="";
    // Captured from the frozen F757 DLL by --probe-legacy-snapshot; JSON SHA256
    // 89BC6443B1E40F7B9BDFB9AA8741887AAECE0F51443900E8480BD41CBCED2201.
    const string LegacySnapshotGolden="""{"Protocol":"HBRGO_EXACT_COMBAT","Version":1,"Creatures":[{"Index":0,"CombatIndex":0,"CombatId":null,"PlayerIndex":null,"PetOwnerIndex":null,"MonsterId":"MONSTER.SMALL_HOPPER","CurrentHp":"101","MaxHp":"101","DpPowers":[]},{"Index":1,"CombatIndex":-1,"CombatId":null,"PlayerIndex":0,"PetOwnerIndex":null,"MonsterId":null,"CurrentHp":"1000","MaxHp":"1000","DpPowers":[]}]}""";
    static RunState RunState=null!;
    static CombatState Combat=null!;
    static Player Player=null!;
    static Creature Hero=null!,Boss=null!;
    static Type HpPower=>Mod.GetType("HBRGOSakiMod.Core.Models.Powers.NararaLayeredHpPower",true)!;
    static Type Helper(string name)=>Mod.GetType("HBRGOSakiMod.Core.Helpers."+name,true)!;
    static object? Call(string helper,string method,params object?[] args)=>Helper(helper).GetMethod(method,F)!.Invoke(null,args);
    static decimal Number(string helper,string method,params object?[] args)=>Convert.ToDecimal(Call(helper,method,args),CultureInfo.InvariantCulture);
    static FieldInfo Field(Type type,string name)
    {
        for(Type? t=type;t!=null;t=t.BaseType)if(t.GetField(name,F) is {} field)return field;
        throw new MissingFieldException(type.FullName,name);
    }
    static void Set(object instance,string field,object? value)=>Field(instance.GetType(),field).SetValue(instance,value);
    static T Blank<T>(){var value=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));GC.SuppressFinalize(value!);return value;}
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Passed.Add(name);}
    static void Equal(decimal actual,decimal expected,string name)=>Check(actual==expected,$"{name}: {actual} = {expected}");
    static bool Skip()=>false;
    static bool StringNameLeaf(StringName __instance){GC.SuppressFinalize(__instance);return false;}
    static bool VisualTask(ref Task __result){__result=Task.CompletedTask;return false;}
    static bool EnergyLeaf(ref string __result){__result="energy";return false;}
    static bool LocalFalse(ref bool __result){__result=false;return false;}
    static bool Listeners(ref IEnumerable<AbstractModel> __result)
    {
        __result=Combat==null?[]:Combat.Creatures.SelectMany(c=>c.Powers.Cast<AbstractModel>().Concat(c.Monster==null?[]:new AbstractModel[]{c.Monster})).ToArray();
        return false;
    }
    static decimal Hp(Creature creature)=>Number("LargeCreatureHealthHelper","GetCurrentHp",creature);
    static decimal Max(Creature creature)=>Number("LargeCreatureHealthHelper","GetMaxHp",creature);
    static int Count(Creature creature)=>(int)Number("NararaLayeredHpHelper","GetRemainingLayers",creature);
    static bool Enabled(Creature creature)=>(bool)Call("NararaLayeredHpHelper","IsEnabled",creature)!;
    static PowerModel Power(Creature creature)=>creature.Powers.Single(p=>p.GetType()==HpPower);
    public sealed class HpDeltaObserver : PowerModel
    {
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type=>MegaCrit.Sts2.Core.Entities.Powers.PowerType.None;
        public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType=>MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Single;
        public override Task AfterCurrentHpChanged(Creature creature,decimal delta)
        {if(ReferenceEquals(creature,Boss))HpDeltas.Add(delta);return Task.CompletedTask;}
    }
    static void Install(Harmony h,Type type)
    {Check(h.CreateClassProcessor(type).Patch().Count>0,"Installs actual production "+type.Name);Patches.Add(type.FullName!);}
    static void Setup(bool old)
    {
        var h=new Harmony("hbr.narara-four-hp.native");
        Install(h,Assembly.Load("BaseLib").GetType("BaseLib.Patches.Content.PrefixIdPatch",true)!);
        h.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(StringNameLeaf),F)!));
        Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(var m in logger.GetMethods(F).Where(m=>m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))h.Patch(m,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(Skip),F)!));
        TestMode.IsOn=true;
        foreach(var type in new[]{typeof(NararaShibaBossMonster),typeof(SmallHopper),typeof(LargeHealthPower),typeof(ElectronicSoldierManual),typeof(Ironclad),typeof(HpDeltaObserver)})ModelDb.Inject(type);
        if(!old)ModelDb.Inject(HpPower);
        foreach(string name in new[]{"LargeCreatureSetMaximumPatch","LargeCreatureSetCurrentPatch","LargeCreatureLoseHpPatch","LargeCreatureCommandSetCurrentPatch","LargeCreatureHealthArithmeticPatch","LargeCreatureNativeDeathPatch"})
            Install(h,Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!);
        if(!old)foreach(var type in Mod.GetTypes().Where(t=>t.Namespace=="HBRGOSakiMod.Core.Patches.Combat"&&t.Name.StartsWith("Narara",StringComparison.Ordinal)&&t.IsDefined(typeof(HarmonyPatch),false)&&!t.Name.StartsWith("NararaContest",StringComparison.Ordinal)))
            if(type.Name.Contains("Hp",StringComparison.Ordinal)||type.Name.StartsWith("NararaForcedTurn",StringComparison.Ordinal))Install(h,type);
        if(!old)foreach(string name in new[]{"LargeCombatSnapshotCapturePatch","LargeCombatSnapshotAnonymizePatch"})Install(h,Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!);
        foreach(var type in new[]{typeof(RunState),typeof(CombatState)})h.Patch(type.GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(Listeners),F)!));
        Type local=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Context.LocalContext",true)!;
        foreach(var method in local.GetMethods(F).Where(m=>m.Name=="IsMe"&&m.ReturnType==typeof(bool)))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(LocalFalse),F)!));
        h.Patch(typeof(RunManager).GetProperty("IsSingleplayerOrFakeMultiplayer",F)!.GetMethod!,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(LocalFalse),F)!));
        foreach(var method in typeof(Cmd).GetMethods(F).Where(m=>m.Name is "Wait" or "CustomScaledWait"&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(VisualTask),F)!));
        Type energy=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(var method in energy.GetMethods(F).Where(m=>m.Name is "GetPrefix" or "GetPath"&&m.ReturnType==typeof(string)))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(EnergyLeaf),F)!));
        foreach(string name in new[]{"MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx"})
            foreach(var method in typeof(ModelDb).Assembly.GetType(name,true)!.GetMethods(F).Where(m=>m.Name=="Create"))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(Skip),F)!));
        foreach(var method in typeof(CreatureCmd).GetMethods(F).Where(m=>m.Name=="TriggerAnim"&&m.ReturnType==typeof(Task)))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(VisualTask),F)!));
        foreach(var method in typeof(SfxCmd).GetMethods(F).Where(m=>m.Name=="Play"))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(Skip),F)!));
        foreach(var method in typeof(PowerModel).GetMethods(F).Where(m=>m.Name=="Flash"))h.Patch(method,prefix:new HarmonyMethod(typeof(NararaHpChecks).GetMethod(nameof(Skip),F)!));
    }
    static void Manager(bool live)
    {
        var manager=CombatManager.Instance;
        if(typeof(CombatManager).GetField("_turnState",F) is {} field)
        {
            Type type=field.FieldType;object turn=RuntimeHelpers.GetUninitializedObject(type);
            Set(turn,"_cts",new CancellationTokenSource());Set(turn,"<State>k__BackingField",Combat);Set(turn,"<IsInProgress>k__BackingField",live);
            Set(turn,"<PlayersTakingExtraTurn>k__BackingField",new List<Player>());Set(turn,"<ReadyLock>k__BackingField",Activator.CreateInstance(typeof(System.Threading.Lock)));
            foreach(string name in new[]{"PlayersReadyToEndTurn","PlayersReadyToBeginEnemyTurn","PlayersThatCannotUnEndTurn"})Set(turn,"<"+name+">k__BackingField",new HashSet<Player>());
            Type signal=typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.EndTurnSignal",true)!;
            Set(turn,"<EndTurnSignalSource>k__BackingField",Activator.CreateInstance(typeof(TaskCompletionSource<>).MakeGenericType(signal)));
            field.SetValue(manager,turn);
            object tracker=typeof(CombatManager).GetProperty("StateTracker",F)!.GetValue(manager)!;Set(tracker,"_state",Combat);
        }
        else
        {
            Set(manager,"_state",Combat);Set(manager,"<IsInProgress>k__BackingField",live);Set(manager,"_combatCts",new CancellationTokenSource());
            Set(manager,"_playersReadyToEndTurn",new HashSet<Player>());Set(manager,"_playersTakingExtraTurn",new List<Player>());
            Set(manager,"_inPlayerTurnSetup",true);Set(manager,"_deferredEndTurnTransition",null);
        }
        Set(RunManager.Instance,"<ActionQueueSet>k__BackingField",new ActionQueueSet(new[]{Player}));
        Set(RunManager.Instance,"<ActionExecutor>k__BackingField",Blank<ActionExecutor>());
    }
    static async Task World(decimal total,bool hbr=true,bool apply=true,bool allied=false,bool ordinary=false,bool live=false)
    {
        RunState=Blank<RunState>();Set(RunState,"_visitedMapCoords",new List<MegaCrit.Sts2.Core.Map.MapCoord>());Set(RunState,"<ActFloor>k__BackingField",1);
        foreach(string name in new[]{"_currentRooms","_mapPointHistory"})Set(RunState,name,Activator.CreateInstance(Field(typeof(RunState),name).FieldType));
        Player=Blank<Player>();Set(Player,"_runState",RunState);Set(Player,"<NetId>k__BackingField",101UL);Set(Player,"_relics",new List<RelicModel>());
        Set(Player,"<Character>k__BackingField",ModelDb.Character<Ironclad>());
        Hero=new Creature(Player,1000,1000);Set(Player,"<Creature>k__BackingField",Hero);
        var playerState=Blank<PlayerCombatState>();Set(playerState,"_player",Player);Set(playerState,"_pets",new List<Creature>());Set(playerState,"<TurnNumber>k__BackingField",1);Set(playerState,"_phase",PlayerTurnPhase.Play);
        foreach(var row in new[]{("Hand",PileType.Hand),("DrawPile",PileType.Draw),("DiscardPile",PileType.Discard),("ExhaustPile",PileType.Exhaust),("PlayPile",PileType.Play)})Set(playerState,"<"+row.Item1+">k__BackingField",new CardPile(row.Item2));
        Set(Player,"<PlayerCombatState>k__BackingField",playerState);Set(Player,"<Deck>k__BackingField",new CardPile(PileType.Deck));
        Set(RunState,"_players",new List<Player>{Player});
        var manual=(ElectronicSoldierManual)ModelDb.Relic<ElectronicSoldierManual>().ToMutable();Set(manual,"_owner",Player);
        typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(manual,JsonSerializer.Serialize(new{Schema=1,Enabled=hbr,HealthScaleApplied=true,Level=1}));
        ((List<RelicModel>)Field(typeof(Player),"_relics").GetValue(Player)!).Add(manual);
        Combat=new CombatState(runState:RunState);Hero.CombatState=Combat;Combat.AddCreature(Hero);
        var observer=(PowerModel)ModelDb.GetById<PowerModel>(ModelDb.GetId(typeof(HpDeltaObserver))).ToMutable();observer.ApplyInternal(Hero,1m,silent:true);HpDeltas.Clear();
        MonsterModel monster=ordinary?ModelDb.Monster<SmallHopper>().ToMutable():ModelDb.Monster<NararaShibaBossMonster>().ToMutable();
        if(monster is NararaShibaBossMonster)typeof(NararaShibaBossMonster).GetProperty("InitialFloor",F)!.SetValue(monster,(int)Math.Min(total,100000));
        Boss=new Creature(monster,allied?CombatSide.Player:CombatSide.Enemy,null);Boss.CombatState=Combat;
        if(allied)Set(Boss,"_petOwner",Player);
        Combat.AddCreature(Boss);Manager(live);
        Boss.SetMaxHpInternal(total);Boss.SetCurrentHpInternal(total);
        if(apply)
        {
            PowerModel power=(PowerModel)ModelDb.GetById<PowerModel>(ModelDb.GetId(HpPower)).ToMutable();
            power.ApplyInternal(Boss,4m,silent:true);await power.AfterApplied(Boss,null);
        }
    }
    static void Result(string name,DamageResult result,decimal before)
    {Results.Add(new{name,before,after=Hp(Boss),maximum=Max(Boss),nativeCurrent=Boss.CurrentHp,nativeMaximum=Boss.MaxHp,loss=result.UnblockedDamage,overkill=result.OverkillDamage,killed=result.WasTargetKilled,layers=Boss.Powers.Any(p=>p.GetType()==Mod.GetType("HBRGOSakiMod.Core.Models.Powers.NararaLayeredHpPower"))?Power(Boss).Amount:(int?)null});}
    static async Task NumericCases()
    {
        foreach(decimal total in new[]{100m,101m,102m,103m,999999999m,1000000000m,18446744073709551616m})
        {
            await World(total);Equal(Max(Boss),total,"four bars preserve exact total "+total);
            Check(Boss.HasPower<LargeHealthPower>(),"AfterApplied installs authoritative ledger even at small HP "+total);Equal(Count(Boss),4,"four initial bars "+total);
            decimal lostTotal=0;
            for(int remaining=4;remaining>0;remaining--)
            {
                decimal expected=decimal.Floor(total*(remaining-1m)/4m),capacity=decimal.Floor(total*remaining/4m)-expected;
                Equal(Number("NararaLayeredHpHelper","GetActiveMaximumHp",Boss),capacity,"active integer quarter capacity "+total+"/"+remaining);
                decimal before=Hp(Boss);
                if(before-expected>999999999m)
                {
                    DamageResult sample=Boss.LoseHpInternal(999999999m,ValueProp.Unblockable|ValueProp.Unpowered);
                    Equal(sample.UnblockedDamage,999999999m,"large quarter retains native per-hit receipt limit");
                    Equal(Count(Boss),remaining,"large non-breaking hit retains current bar");
                    Boss.SetCurrentHpInternal(expected+1m);
                    Equal(Hp(Boss),expected+1m,"real direct setter prepares last exact HP of huge quarter");
                    Equal(Count(Boss),remaining,"non-breaking direct write retains huge bar");
                    before=Hp(Boss);
                }
                DamageResult result=Boss.LoseHpInternal(999999999m,ValueProp.Unblockable|ValueProp.Unpowered);
                lostTotal+=capacity;Equal(Hp(Boss),expected,"one overflow stops at current quarter "+total+"/"+remaining);
                Equal(Count(Boss),remaining-1,"one consumed bar updates authoritative Amount "+total+"/"+remaining);
                Check(result.WasTargetKilled==(remaining==1),"native result only final bar kills "+total+"/"+remaining);
                Equal(result.OverkillDamage,0,"rejected overflow never becomes overkill "+total+"/"+remaining);
                if(remaining>1)Check(Boss.IsAlive&&Boss.CurrentHp>0,"intermediate quarter retains native life "+total+"/"+remaining);
                Result("quarter "+total+"/"+remaining,result,before);
            }
            Equal(lostTotal,total,"integer quarter capacities sum to unchanged total "+total);
            Equal(Hp(Boss),0,"last quarter reaches real zero "+total);Check(Boss.IsDead,"native IsDead sees final zero "+total);
        }
    }
    static async Task HealingAndEdges()
    {
        await World(101);Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),75,"first odd quarter loses26");
        Boss.LoseHpInternal(7,ValueProp.Unpowered);Equal(Hp(Boss),68,"next independent offturn hit damages new bar");
        Boss.HealInternal(1000);Equal(Hp(Boss),75,"native healing clamps to current bar ceiling");Equal(Count(Boss),3,"healing never restores consumed bar count");
        Boss.LoseHpInternal(.9m,ValueProp.Unpowered);Equal(Hp(Boss),75,"fractional sub-one HP loss floors without breaking");
        Boss.LoseHpInternal(0,ValueProp.Unpowered);Equal(Count(Boss),3,"zero HP loss consumes no extra bar");
        Boss.SetCurrentHpInternal(0);Equal(Hp(Boss),50,"native direct HP set respects current bar boundary");Equal(Count(Boss),2,"direct HP set consumes exactly one bar");
        await World(101,hbr:false);Check(Enabled(Boss),"legacy-mode enemy Shiba uses the same new four bars");Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),75,"legacy mode blocks the same overflow");
        await World(101,allied:true);Check(!Enabled(Boss),"allied Shiba phantom does not receive enemy HP rule");Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),0,"allied phantom remains normal HP");
        await World(101,ordinary:true);Check(!Enabled(Boss),"ordinary enemy is not a four-bar Shiba");Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),0,"ordinary enemy retains native lethal damage");
        await World(101);var result=(DamageResult)Call("LargeCreatureHealthHelper","LoseHpForKill",Boss,101m,ValueProp.Unblockable|ValueProp.Unpowered)!;
        Equal(Hp(Boss),0,"explicit native death drain bypasses intermediate bars");Check(Boss.IsDead&&result.WasTargetKilled,"explicit death drain remains lethal");
        Check((bool)Call("PowerDispelRules","IsUndispellable",Power(Boss))!,"new four-bar ability remains undispellable after final drain");
        foreach(int total in new[]{1,2,3})
        {
            await World(total);int breaks=0;
            while(Boss.IsAlive)
            {
                decimal before=Hp(Boss);DamageResult hit=Boss.LoseHpInternal(999,ValueProp.Unpowered);breaks++;
                Check(Hp(Boss)<before&&breaks<=total,"tiny totals skip zero-capacity partitions without phantom breaks");
                Equal(hit.OverkillDamage,0,"tiny total discards overflow");
            }
            Equal(Max(Boss),total,"tiny four-bar total never gains HP");Equal(Count(Boss),0,"tiny total finishes without empty positive counter");
        }
    }
    static object BeginScope()=>Call("NararaHpTransitionBoundary","Begin",Combat)!;
    static void CloseScope(object scope,bool failed=false)=>Call("NararaHpTransitionBoundary","Close",scope,failed);
    static bool Blocked()=>(bool)Call("NararaHpTransitionBoundary","BlocksHpLoss",Boss)!;
    static object SessionToken(string coordinator,bool active)
    {
        Type type=Helper(coordinator);FieldInfo current=Field(type,"_current");
        if(!active){current.SetValue(null,null);if(type.GetField("_combat",F) is {} combat)combat.SetValue(null,null);return null!;}
        Type sessionType=type.GetNestedType("Session",F)!;object session=RuntimeHelpers.GetUninitializedObject(sessionType);
        Set(session,"Combat",Combat);
        if(coordinator=="PersonalImmediateTurnCoordinator"){Set(session,"Owner",Player);Field(type,"_combat").SetValue(null,Combat);}
        else Set(session,"Players",new[]{Player});
        FieldInfo phase=Field(sessionType,"Phase");phase.SetValue(session,Enum.Parse(phase.FieldType,"Playing"));current.SetValue(null,session);return session;
    }
    static Player Peer()
    {
        var peer=Blank<Player>();Set(peer,"_runState",RunState);Set(peer,"<NetId>k__BackingField",202UL);Set(peer,"_relics",new List<RelicModel>());Set(peer,"<Character>k__BackingField",ModelDb.Character<Ironclad>());
        var creature=new Creature(peer,1000,1000);Set(peer,"<Creature>k__BackingField",creature);creature.CombatState=Combat;Combat.AddCreature(creature);
        var state=Blank<PlayerCombatState>();Set(state,"_player",peer);Set(state,"_pets",new List<Creature>());Set(state,"<TurnNumber>k__BackingField",1);Set(state,"_phase",PlayerTurnPhase.Play);Set(peer,"<PlayerCombatState>k__BackingField",state);
        Set(RunState,"_players",new List<Player>{Player,peer});Manager(true);return peer;
    }
    static async Task BoundaryCases()
    {
        await World(100);object scope=BeginScope();Boss.LoseHpInternal(1000,ValueProp.Unpowered);
        Equal(Hp(Boss),75,"scope records a real first HP break");Check(Blocked(),"same action is locked after committed bar break");
        Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),75,"nested HP supplement cannot consume next bar");
        Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);Check(Blocked(),"same action marker survives side change during cleanup");
        CloseScope(scope);Check(!Blocked(),"closing offturn scope releases short lock");Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),50,"next independent offturn hit may consume next bar");
        await World(100,live:true);Boss.LoseHpInternal(1000,ValueProp.Unpowered);
        Check(CombatManager.Instance.IsPlayerReadyToEndTurn(Player),"outside-card native HP hit requests genuine forced EndTurn");
        Check(Blocked(),"ready vote does not clear current turn barrier");Boss.LoseHpInternal(1000,ValueProp.Unpowered);Equal(Hp(Boss),75,"outside-card followup cannot cross while forced end is pending");
        Player.PlayerCombatState!.IncrementTurnNumber();Check(!Blocked(),"native extra-turn number releases barrier without round increment");
        Boss.LoseHpInternal(1,ValueProp.Unpowered);Equal(Hp(Boss),74,"new native extra-turn identity can damage next bar");
        await World(100,live:true);object outer=BeginScope(),inner=BeginScope();Boss.LoseHpInternal(1000,ValueProp.Unpowered);
        CloseScope(inner,failed:true);Check(Blocked(),"caught nested failure preserves active outer cleanup protection");CloseScope(outer);
        Check(!Blocked(),"caught nested failure then successful outer close leaves no turn lock");Boss.LoseHpInternal(1,ValueProp.Unpowered);Equal(Hp(Boss),74,"no dangling turn lock after nested failure");
        await World(100,live:true);scope=BeginScope();Boss.LoseHpInternal(1000,ValueProp.Unpowered);CloseScope(scope,failed:true);
        Check(!Blocked(),"owning failed action clears its current turn lock");
        foreach(string coordinator in new[]{"PersonalImmediateTurnCoordinator","OverclockImmediateTurnCoordinator"})
        {
            await World(100,live:true);object token=SessionToken(coordinator,true);
            Check(ReferenceEquals(Call("OverclockImmediateTurnCoordinator","GetTurnToken",Player),token),"real coordinator getter exposes "+coordinator+" session identity");
            Boss.LoseHpInternal(1000,ValueProp.Unpowered);Check(Blocked(),coordinator+" retains lock during current inserted identity");SessionToken(coordinator,false);
            Check(!Blocked(),coordinator+" restoration to ordinary identity releases prior bar lock");Boss.LoseHpInternal(1,ValueProp.Unpowered);Equal(Hp(Boss),74,coordinator+" restored ordinary identity may damage new bar");
        }
        await World(100,live:true);Peer();Boss.LoseHpInternal(1000,ValueProp.Unpowered);Check(Blocked(),"multiplayer source turn initially retains bar boundary");
        Player.PlayerCombatState!.IncrementTurnNumber();
        if(typeof(CombatManager).GetField("_turnState",F) is {} turnField)Set(turnField.GetValue(CombatManager.Instance)!,"<PlayersTakingExtraTurn>k__BackingField",new List<Player>{Player});
        else Set(CombatManager.Instance,"_playersTakingExtraTurn",new List<Player>{Player});
        Check(!Blocked(),"multiplayer next native extra turn is not locked by teammate's unchanged prior turn number");
        await World(100,live:true);Player peer=Peer();Boss.LoseHpInternal(1000,ValueProp.Unpowered);peer.Creature.LoseHpInternal(1000,ValueProp.Unpowered);
        Check(Blocked(),"peer death does not release the unchanged living player's outgoing turn barrier");
    }
    static async Task FullDamageAndDp()
    {
        await World(101);Type compatibility=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;
        MethodInfo damage=compatibility.GetMethods(F).Single(m=>m.Name=="Damage"&&m.GetParameters().Length==7&&m.GetParameters()[1].ParameterType==typeof(Creature)&&m.GetParameters()[2].ParameterType==typeof(decimal));
        var result=await (Task<IEnumerable<DamageResult>>)damage.Invoke(null,[new ThrowingPlayerChoiceContext(),Boss,1000m,ValueProp.Unblockable|ValueProp.Unpowered,null,null,null])!;
        DamageResult hit=result.Single();Equal(Hp(Boss),75,"full actual native Damage command writes only current odd bar");Equal(hit.UnblockedDamage,26,"full Damage result reports actual26 HP lost");Equal(hit.OverkillDamage,0,"full Damage drops overflow receipt");Check(!hit.WasTargetKilled,"full Damage keeps intermediate bar alive");
        Check(HpDeltas.SequenceEqual(new[]{-26m}),"full native Damage hook reports actual26 HP delta");
        await CreatureCmd.SetCurrentHp(Boss,0m);Equal(Hp(Boss),50,"actual native SetCurrentHp command respects next bar floor");
        Check(HpDeltas.SequenceEqual(new[]{-26m,-25m}),"actual native SetCurrentHp hook reports clipped25 HP delta");
        Boss.LoseHpInternal(7m,ValueProp.Unpowered);Manager(true);Set(Combat,"<CurrentSide>k__BackingField",CombatSide.Enemy);
        await CreatureCmd.Heal(Boss,7m,playAnim:false);Equal(Hp(Boss),50,"actual native Heal command restores current bar's missing7 HP");
        Equal(HpDeltas.Last(),7,"native Heal hook receives actual nonzero healed HP");Equal(Count(Boss),2,"native Heal command never resurrects a consumed bar");
        await World(101);int died=0;Boss.Died+=_=>died++;
        for(int index=0;index<4;index++)
        {
            var hits=await (Task<IEnumerable<DamageResult>>)damage.Invoke(null,[new ThrowingPlayerChoiceContext(),Boss,1000m,ValueProp.Unblockable|ValueProp.Unpowered,null,null,null])!;
            Check(hits.Single().WasTargetKilled==(index==3),"full Damage only fourth bar produces final kill result");
        }
        Equal(died,1,"full native fourth-bar Damage invokes death exactly once");Equal(Hp(Boss),0,"full native fourth-bar death retains real ledger0");
        Check(Boss.IsDead&&!Combat.ContainsCreature(Boss),"full native death cleanup removes defeated boss from combat");
        Check(Boss.HasPower<LargeHealthPower>()&&!Boss.Powers.Any(p=>p.GetType()==HpPower),"native death cleanup retains ledger and removes bar ability normally");
        Check(HpDeltas.SequenceEqual(new[]{-26m,-25m,-25m,-25m}),"full native four-bar hooks sum to unchanged total101 HP");
        await World(18446744073709551616m);died=0;Boss.Died+=_=>died++;await CreatureCmd.Kill(Boss,force:true);
        Equal(Hp(Boss),0,"actual native force Kill drains entire huge ledger across all bars");Equal(died,1,"actual native force Kill executes native death event once");
        Check(!Combat.ContainsCreature(Boss),"actual native force Kill completes creature removal");Equal(HpDeltas.Single(),-18446744073709551616m,"native death drain hook reports exact huge ledger delta");
        await World(101);typeof(NararaShibaBossMonster).GetProperty("DpLayerMaximumData",F)!.SetValue(Boss.Monster,"100");Set(Boss.Monster!,"<DpLayersRemaining>k__BackingField",3);
        ModelDb.Inject(typeof(NararaLayeredDpPower));var dp=(NararaLayeredDpPower)ModelDb.Power<NararaLayeredDpPower>().ToMutable();dp.ApplyInternal(Boss,100m,silent:true);await dp.AfterApplied(Boss,null);
        int round=Combat.RoundNumber;Check(dp.TryAbsorb(100m),"first real DP absorption consumes current layer");Equal(((NararaShibaBossMonster)Boss.Monster!).DpLayersRemaining,2,"first DP layer breaks");
        Check(dp.TryAbsorb(100m),"second same-round real DP absorption is accepted");Equal(((NararaShibaBossMonster)Boss.Monster!).DpLayersRemaining,1,"same round may break a second DP layer");Equal(Combat.RoundNumber,round,"DP regression does not manufacture a new round");
    }
    static async Task MaximumGrowth()
    {
        await World(100);Boss.SetMaxHpInternal(200m);
        int beforeLayers=Count(Boss),beforeAmount=Power(Boss).Amount;
        Results.Add(new{name="maximum-growth-before-heal",current=Hp(Boss),maximum=Max(Boss),layers=beforeLayers,authoritativeAmount=beforeAmount});
        Equal(Number("NararaLayeredHpHelper","GetActiveCurrentHp",Boss),25,"maximum growth adds capacity without free current HP");
        Equal(Number("NararaLayeredHpHelper","GetActiveMaximumHp",Boss),125,"maximum growth belongs to current active bar");
        Boss.HealInternal(100m);
        Results.Add(new{name="maximum-growth-after-heal",current=Hp(Boss),maximum=Max(Boss),layers=Count(Boss),authoritativeAmount=Power(Boss).Amount});
        Equal(beforeAmount,4,"maximum growth does not consume authoritative bar count");
        Equal(beforeLayers,4,"maximum growth alone does not infer a lost HP bar");
        Equal(Hp(Boss),200,"native max-growth then native heal may restore newly gained HP");Equal(Count(Boss),4,"max-growth heal retains four unbroken bars");
        Boss.LoseHpInternal(1000m,ValueProp.Unpowered);Equal(Hp(Boss),75,"grown first bar still ends at initial reserve75");Equal(Count(Boss),3,"grown first bar consumes exactly one counter");
        Boss.HealInternal(1000m);Equal(Hp(Boss),75,"spent grown capacity is not granted again to the next bar");Equal(Number("NararaLayeredHpHelper","GetActiveMaximumHp",Boss),25,"next reserve bar retains original equal capacity");
        await World(100);Boss.SetMaxHpInternal(200m);DamageResult hit=Boss.LoseHpInternal(1m,ValueProp.Unpowered);
        Equal(Hp(Boss),99,"maximum-only growth cannot make the current boss invulnerable below a recomputed floor");
        Equal(hit.UnblockedDamage,1,"first actual damage after maximum-only growth reports real1 HP loss");
        Equal(Count(Boss),4,"actual1 HP loss after maximum growth does not consume empty inferred bars");
        Results.Add(new{name="maximum-growth-then-damage",current=Hp(Boss),maximum=Max(Boss),layers=Count(Boss),authoritativeAmount=Power(Boss).Amount});
        await World(100);Boss.LoseHpInternal(1000m,ValueProp.Unpowered);Equal(Count(Boss),3,"growth edge begins with one genuinely consumed bar");
        Boss.SetMaxHpInternal(200m);Boss.HealInternal(1000m);
        Equal(Hp(Boss),175,"growth after a spent25 HP bar adds capacity only to the active bar");Equal(Count(Boss),3,"maximum growth plus healing cannot revive spent bar count");
        Boss.LoseHpInternal(1000m,ValueProp.Unpowered);Equal(Hp(Boss),50,"grown second bar ends at original reserve50");Equal(Count(Boss),2,"grown second bar consumes one counter only");
        Equal(Number("NararaLayeredHpHelper","GetActiveMaximumHp",Boss),25,"later reserves never repeatedly inherit maximum growth");
    }
    static NetFullCombatState SnapshotShape(IEnumerable<Creature> actors)
    {
        var state=new NetFullCombatState();var rows=new List<NetFullCombatState.CreatureState>();
        object scope=Call("LargeCombatSnapshotHelper","BeginCapture",RunState)!;
        try
        {
            foreach(Creature actor in actors)
            {
                var row=new NetFullCombatState.CreatureState{monsterId=actor.Monster?.Id,playerId=actor.Player?.NetId,
                    currentHp=(int)Call("LargeCombatSnapshotHelper","CaptureCurrentHp",actor)!,maxHp=(int)Call("LargeCombatSnapshotHelper","CaptureMaxHp",actor)!,block=actor.Block,powers=[]};
                foreach(PowerModel power in actor.Powers)row.powers.Add(new NetFullCombatState.PowerState{id=power.Id,amount=(int)Call("LargeCombatSnapshotHelper","CapturePowerAmount",power)!});
                rows.Add(row);
            }
            Set(state,"<Creatures>k__BackingField",rows);Set(state,"<Players>k__BackingField",new List<NetFullCombatState.PlayerState>());
            Set(state,"<Rng>k__BackingField",new SerializableRunRngSet{Seed="fixture"});Set(state,"nextChoiceIds",new List<uint>());Set(state,"nextRewardIds",new List<int>());
            Call("LargeCombatSnapshotHelper","FinishCapture",scope,state);
        }
        finally{Call("LargeCombatSnapshotHelper","EndCapture",scope);}
        return state;
    }
    static string Frozen(NetFullCombatState state)=>(string)Call("LargeCombatSnapshotHelper","GetFrozenJson",state)!;
    static string TextHash(string text)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    static NetFullCombatState SnapshotDecode(NetFullCombatState shape,string json)
    {
        var writer=new PacketWriter();writer.WriteString(json);var reader=new PacketReader();reader.Reset(writer.Buffer);
        var target=new NetFullCombatState();Set(target,"<Creatures>k__BackingField",shape.Creatures);Call("LargeCombatSnapshotHelper","Deserialize",target,reader);
        Check(reader.BitPosition==writer.BitPosition,"snapshot extension consumes exactly its serialized string bits");return target;
    }
    static async Task LegacySnapshotOnly()
    {
        await World(101,apply:false,ordinary:true);string json=Frozen(SnapshotShape(new[]{Boss}));
        Check(!json.Contains("HpLayers",StringComparison.Ordinal),"marker-free snapshot omits the new optional member");
        Results.Add(new{name="legacy-snapshot-golden",frozenJson=json,sha256=TextHash(json),scope="Real production capture functions and freeze output on the previous F757 DLL; controlled native row factory, not full FromRun"});
    }
    static async Task SnapshotCases()
    {
        await World(101);NetFullCombatState first=SnapshotShape(new[]{Boss});string initial=Frozen(first);
        using(var doc=JsonDocument.Parse(initial))
        {
            JsonElement hp=doc.RootElement.GetProperty("Creatures")[0].GetProperty("HpLayers")[0];
            Check(hp.GetProperty("PowerId").GetString()==Power(Boss).Id.ToString(),"real snapshot capture retains native HP power identity");
            Check(hp.GetProperty("PartitionMaximumHp").GetString()=="101"&&hp.GetProperty("SpentMaximumHp").GetString()=="0","real capture reads both partition fields from the live model");
        }
        var writer=new PacketWriter();Call("LargeCombatSnapshotHelper","Serialize",first,writer);var reader=new PacketReader();reader.Reset(writer.Buffer);
        var decoded=new NetFullCombatState();Set(decoded,"<Creatures>k__BackingField",first.Creatures);Call("LargeCombatSnapshotHelper","Deserialize",decoded,reader);
        Check(Frozen(decoded)==initial&&reader.BitPosition==writer.BitPosition,"actual extension Serialize/Deserialize roundtrip retains frozen bytes");
        NetFullCombatState anonymous=first.Anonymized();Check(Frozen(anonymous)==initial,"actual native Anonymized plus production postfix preserves frozen exact fields");
        PropertyInfo partition=HpPower.GetProperty("PartitionMaximumHpData",F)!,spent=HpPower.GetProperty("SpentMaximumHpData",F)!;
        partition.SetValue(Power(Boss),"102");string changedPartition=Frozen(SnapshotShape(new[]{Boss}));
        Check(TextHash(changedPartition)!=TextHash(initial),"partition field alone changes actual captured JSON hash");Check(Frozen(first)==initial,"later live partition changes never mutate frozen snapshots");
        partition.SetValue(Power(Boss),"101");spent.SetValue(Power(Boss),"1");string changedSpent=Frozen(SnapshotShape(new[]{Boss}));
        Check(TextHash(changedSpent)!=TextHash(initial),"spent capacity field alone changes actual captured JSON hash");
        partition.SetValue(Power(Boss),"");spent.SetValue(Power(Boss),"");string fallback=Frozen(SnapshotShape(new[]{Boss}));
        Check((string)partition.GetValue(Power(Boss))! == ""&&(string)spent.GetValue(Power(Boss))! == "","snapshot raw fallback leaves both original empty fields untouched");
        using(var doc=JsonDocument.Parse(fallback))
        {JsonElement hp=doc.RootElement.GetProperty("Creatures")[0].GetProperty("HpLayers")[0];Check(hp.GetProperty("PartitionMaximumHp").GetString()=="101"&&hp.GetProperty("SpentMaximumHp").GetString()=="0","readonly raw fallback derives actual max/count deterministically");}
        foreach(bool missingPartition in new[]{false,true})
        {
            await World(1000);Boss.LoseHpInternal(10000,ValueProp.Unpowered);Boss.SetMaxHpInternal(1100);
            partition.SetValue(Power(Boss),missingPartition?"":"1000");spent.SetValue(Power(Boss),missingPartition?"250":"");
            decimal effectivePartition=missingPartition?1100m:1000m,effectiveSpent=250m;
            Equal(Number("NararaLayeredHpHelper","GetActiveMaximumHp",Boss),missingPartition?300m:350m,"live runtime budget honors independent partial-field fallbacks");
            string partial=Frozen(SnapshotShape(new[]{Boss}));
            Results.Add(new{name="partial-snapshot-budget",missingPartition,rawPartition=partition.GetValue(Power(Boss)),rawSpent=spent.GetValue(Power(Boss)),runtimeActiveMaximum=Number("NararaLayeredHpHelper","GetActiveMaximumHp",Boss),frozenJson=partial,expectedPartition=effectivePartition,expectedSpent=effectiveSpent});
            using(var doc=JsonDocument.Parse(partial))
            {
                JsonElement hp=doc.RootElement.GetProperty("Creatures")[0].GetProperty("HpLayers")[0];
                Check(hp.GetProperty("PartitionMaximumHp").GetString()==effectivePartition.ToString(CultureInfo.InvariantCulture)&&hp.GetProperty("SpentMaximumHp").GetString()==effectiveSpent.ToString(CultureInfo.InvariantCulture),"snapshot independent missing field fallback matches actual live runtime budget");
            }
            Check((string)partition.GetValue(Power(Boss))! == (missingPartition?"":"1000")&&(string)spent.GetValue(Power(Boss))! == (missingPartition?"250":""),"partial-field snapshot fallback never backfills live SavedStrings");
        }
        foreach(string invalid in new[]{"-1","01","1.5","18446744073709551617"})
        {
            var node=System.Text.Json.Nodes.JsonNode.Parse(initial)!;node["Creatures"]![0]!["HpLayers"]![0]!["SpentMaximumHp"]=invalid;
            bool rejected=false;try{SnapshotDecode(first,node.ToJsonString());}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){rejected=true;}
            Check(rejected,"snapshot rejects noncanonical/unbounded HP layer amount "+invalid);
        }
        foreach(string field in new[]{"PowerId","PowerIndex"})
        {
            var node=System.Text.Json.Nodes.JsonNode.Parse(initial)!;
            if(field=="PowerId")node["Creatures"]![0]!["HpLayers"]![0]![field]="POWER.WRONG";else node["Creatures"]![0]!["HpLayers"]![0]![field]=999;
            bool rejected=false;try{SnapshotDecode(first,node.ToJsonString());}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){rejected=true;}
            Check(rejected,"snapshot validates native HP layer "+field);
        }
        await World(100);Boss.SetMaxHpInternal(200);Boss.HealInternal(100);Boss.LoseHpInternal(1000,ValueProp.Unpowered);
        string grown=Frozen(SnapshotShape(new[]{Boss}));using(var doc=JsonDocument.Parse(grown))
        {JsonElement hp=doc.RootElement.GetProperty("Creatures")[0].GetProperty("HpLayers")[0];Check(hp.GetProperty("PartitionMaximumHp").GetString()=="100"&&hp.GetProperty("SpentMaximumHp").GetString()=="125","actual grown consumed capacity greater than initial partition survives validation");}
        await World(101);Combat.RemoveCreature(Boss,unattach:false);Set(Boss,"<Side>k__BackingField",CombatSide.Player);Set(Boss,"_petOwner",Player);
        ((List<Creature>)Field(typeof(PlayerCombatState),"_pets").GetValue(Player.PlayerCombatState!)!).Add(Boss);
        string extra=Frozen(SnapshotShape(new[]{Hero}));using(var doc=JsonDocument.Parse(extra))
        {JsonElement row=doc.RootElement.GetProperty("Creatures")[1];Check(row.GetProperty("CombatIndex").GetInt32()==-1&&row.GetProperty("HpLayers")[0].GetProperty("PartitionMaximumHp").GetString()=="101","production AddExtra capture freezes a detached pet's partition fields");}
        await World(101,ordinary:true,apply:false);NetFullCombatState legacyState=SnapshotShape(new[]{Boss});string legacy=Frozen(legacyState);
        Check(LegacySnapshotGolden.Length>0&&legacy==LegacySnapshotGolden,"no-marker snapshot JSON stays byte-identical to actual previous-version golden");
        Check(Frozen(SnapshotDecode(legacyState,LegacySnapshotGolden))==LegacySnapshotGolden,"previous snapshot without optional HP member still roundtrips byte-identically");
        Results.Add(new{name="exact-snapshot-extension",initialSha256=TextHash(initial),changedPartitionSha256=TextHash(changedPartition),changedSpentSha256=TextHash(changedSpent),legacySha256=TextHash(legacy),
            scope="Manually drive genuine production Begin/CaptureCurrent/CaptureMax/CapturePower/Finish functions with controlled native rows; genuine extension PacketWriter/Reader and native Anonymized postfix. Full native FromRun and full native snapshot packet/transport are not invoked; no live entity restoration is claimed."});
    }
    static void CacheSaved(Type type)
    {
        Assembly native=typeof(SavedProperties).Assembly;
        if(native.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedPropertiesTypeCache") is {} stable){stable.GetMethod("InjectTypeIntoCache",F)!.Invoke(null,[type]);return;}
        Type beta=native.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache",true)!;
        beta.GetMethod("CacheSavedPropertiesForTypeDebug",F)!.Invoke(null,[type]);beta.GetField("_initialized",F)!.SetValue(null,true);
    }
    static void LoadLocalization(string repo,string language)
    {
        string path=Path.Combine(LocalizationRoot,language,"powers.json");byte[] bytes=File.ReadAllBytes(path);Sources[path]=Convert.ToHexString(SHA256.HashData(bytes));
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField",language);Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        Set(manager,"_tables",new Dictionary<string,LocTable>{{"powers",new LocTable("powers",JsonSerializer.Deserialize<Dictionary<string,string>>(bytes)!,null!)}});
        typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
    }
    static async Task IdentityAndSave(string repo)
    {
        Check(ModelDb.GetId(HpPower).Entry=="NARARA_LAYERED_HP_POWER","actual BaseLib CustomID resolves the exact authored power key");
        await World(101);Boss.LoseHpInternal(1000,ValueProp.Unpowered);PowerModel power=Power(Boss);Equal(power.Amount,3,"authoritative native Amount stores remaining bars");
        foreach(string language in new[]{"zhs","eng"})
        {
            LoadLocalization(repo,language);Check(power.Title.Exists(),"real "+language+" power title key exists");Check(!string.IsNullOrWhiteSpace(power.Title.GetFormattedText()),"real "+language+" power title formats");
            foreach(PowerModel model in new[]{ModelDb.GetById<PowerModel>(ModelDb.GetId(HpPower)),power})
            {LocString text=model.Description;Check(text.Exists(),language+" actual canonical/owned Description key exists");typeof(PowerModel).GetMethod("AddDumbVariablesToDescription",F)!.Invoke(model,[text,null]);string formatted=text.GetFormattedText();Check(!string.IsNullOrWhiteSpace(formatted)&&!formatted.Contains('{'),language+" actual Description formats without raw key/placeholder");}
        }
        CacheSaved(HpPower);CacheSaved(typeof(LargeHealthPower));CacheSaved(typeof(NararaShibaBossMonster));
        var options=new JsonSerializerOptions{IncludeFields=true};
        string ledger=JsonSerializer.Serialize(SavedProperties.From(Boss.GetPower<LargeHealthPower>()!)!,options);
        LargeHealthPower copy=(LargeHealthPower)ModelDb.Power<LargeHealthPower>().ToMutable();JsonSerializer.Deserialize<SavedProperties>(ledger,options)!.Fill(copy);
        Check(copy.CurrentHpData=="75"&&copy.MaxHpData=="101","native SavedProperties JSON/Fill preserves exact ledger strings");
        // Amount belongs to native PowerState rather than SavedProperties. A
        // native mutable clone must retain it; test both mechanisms explicitly.
        PowerModel cloned=(PowerModel)power.MutableClone();Equal(cloned.Amount,3,"native MutableClone preserves authoritative remaining count");
        PropertyInfo[] partitionFields=HpPower.GetProperties(F).Where(p=>p.PropertyType==typeof(string)&&p.GetCustomAttribute<SavedPropertyAttribute>()!=null).ToArray();
        Equal(partitionFields.Length,2,"native saved properties include both initial partition and spent capacity strings");
        string powerJson=JsonSerializer.Serialize(SavedProperties.From(power),options);
        PowerModel restored=ModelDb.GetById<PowerModel>(ModelDb.GetId(HpPower)).ToMutable();
        JsonSerializer.Deserialize<SavedProperties>(powerJson,options)!.Fill(restored);
        foreach(PropertyInfo field in partitionFields)
        {
            string original=(string)field.GetValue(power)!;
            Check(field.GetValue(restored) as string==original,"native SavedProperties JSON/Fill preserves "+field.Name);
            Check(field.GetValue(cloned) as string==original,"native mutable clone preserves "+field.Name);
        }
        Check(partitionFields.Select(p=>decimal.Parse((string)p.GetValue(restored)!,CultureInfo.InvariantCulture)).Order().SequenceEqual(new[]{26m,101m}),"restored partition snapshot stores original101 and genuinely spent26 exactly");
        Check(HpPower.GetProperty("Amount",F)!.GetCustomAttribute<SavedPropertyAttribute>()==null,"native Amount is not falsely claimed as a SavedProperties field");
        Results.Add(new{name="saved-model-boundary",ledgerJson=ledger,powerJson,partitionFields=partitionFields.Select(p=>p.Name).ToArray(),clonedAmount=cloned.Amount,scope="Native SavedProperties ledger/partition fields and native clone count only; native PowerState serialization is not exercised; not a complete mid-combat save/load or network transport claim"});
    }
    public static int Run(string[] args)
    {
        string? error=null;bool old=args.Contains("--expect-old-failure"),negative=false;
        int locale=Array.IndexOf(args,"--localization-root");LocalizationRoot=locale>=0?Path.GetFullPath(args[locale+1]):Path.Combine(Path.GetFullPath(args[3]),"HBRGOSakiMod/localization");
        try
        {
            Setup(old);
            if(old)
            {
                Check(Mod.GetType("HBRGOSakiMod.Core.Models.Powers.NararaLayeredHpPower")==null,"old negative candidate genuinely predates the four-bar HP power");
                World(101,apply:false).GetAwaiter().GetResult();decimal before=Hp(Boss);DamageResult result=Boss.LoseHpInternal(1000,ValueProp.Unpowered);Result("old-overflow",result,before);
                negative=Hp(Boss)==0&&result.UnblockedDamage==101&&result.WasTargetKilled;
                Check(negative,"old native write genuinely crosses all four intended HP bars in one hit");
            }
            else if(args.Contains("--probe-max-growth"))MaximumGrowth().GetAwaiter().GetResult();
            else if(args.Contains("--probe-legacy-snapshot"))LegacySnapshotOnly().GetAwaiter().GetResult();
            else{NumericCases().GetAwaiter().GetResult();HealingAndEdges().GetAwaiter().GetResult();BoundaryCases().GetAwaiter().GetResult();FullDamageAndDp().GetAwaiter().GetResult();MaximumGrowth().GetAwaiter().GetResult();IdentityAndSave(Path.GetFullPath(args[3])).GetAwaiter().GetResult();SnapshotCases().GetAwaiter().GetResult();}
        }
        catch(Exception e){error=(e is TargetInvocationException&&e.InnerException!=null?e.InnerException:e).ToString();}
        bool ok=error==null&&(!old||negative);
        var report=new{ok,negativeControl=old,expectedOldOverflowObserved=negative,selection=args.Contains("--probe-max-growth")?"max-growth-probe":args.Contains("--probe-legacy-snapshot")?"legacy-snapshot-probe":old?"old-overflow-negative":"complete",checks=Passed.Count,passed=Passed,results=Results,error,patches=Patches,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),nativeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),localizationSources=Sources,
            validationAssemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NararaHpChecks).Assembly.Location))),
            boundary="Native model constructors/IDs/mutable clone, actual Creature constructor, real LoseHpInternal/SetCurrentHpInternal/HealInternal, full native Damage/SetCurrentHp/Heal/Kill, clipped Damage/SetCurrent delta, actual7 HP Heal hook and native death/removal cleanup. No HP/heal/death terminal leaves. Native overheal notification semantics are unchanged. StringName/logging/waits/VFX/sound/energy-icon/local-UI identity and hook actor enumeration isolated. Scene manager/queues and inserted-session objects are fixture data; real coordinator token getters run. SavedProperties ledger/partition and clone-count are model-level. Snapshot capture factory manually drives real production capture functions, genuine exact extension packet and native Anonymized; no complete FromRun/native PowerState packet/transport or live-entity restoration. No complete midcombat load, live game, victory/rewards, package or upload."};
        File.WriteAllText(args[4],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{ok,checks=Passed.Count,old,negative,error,report=args[4]}));return ok?0:1;
    }
}
