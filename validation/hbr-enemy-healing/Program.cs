using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

static class EnemyHealingChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    sealed record HealRequest(decimal Requested,int Before,int After,int Maximum,bool PlayAnim);
    static readonly List<HealRequest> Heals=[];
    static readonly List<object> Cases=[];
    static Type Host=>typeof(Checks);
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static Player Player=>State<Player>("Player");
    static CombatState Combat=>State<CombatState>("Combat");
    static RunState CurrentRun=>State<RunState>("RunState");
    static Creature Other=null!,Giant=null!;
    static MethodInfo RegenHook=null!;
    static MethodInfo[] Targets=[];
    static int Decrements,SteamApplications,Ascension;
    static T State<T>(string field)=>(T)Host.GetField(field,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(Host.GetMethod(method,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(Host.GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static void Check(bool value,string label){if(!value)throw new Exception(label);Passed.Add(label);}
    static void Equal(decimal actual,decimal expected,string label)=>Check(actual==expected,$"{label}: {actual} = {expected}");
    static void Wait(object? task)=>((Task)task!).GetAwaiter().GetResult();
    static bool Skip()=>false;
    static bool TaskLeaf(ref Task __result){__result=Task.CompletedTask;return false;}
    static bool DecrementLeaf(ref Task __result){Decrements++;__result=Task.CompletedTask;return false;}
    static bool SteamLeaf(PowerModel power,ref Task __result)
    {if(power is not SteamEruptionPower)return true;SteamApplications++;__result=Task.CompletedTask;return false;}
    static bool AscensionLeaf(AscensionLevel level,int ascensionValue,int fallbackValue,ref int __result)
    {__result=Ascension>=(int)level?ascensionValue:fallbackValue;return false;}
    static bool HealLeaf(Creature creature,decimal amount,bool playAnim,ref Task __result)
    {
        int before=creature.CurrentHp;
        creature.HealInternal(amount);
        Heals.Add(new(amount,before,creature.CurrentHp,creature.MaxHp,playAnim));
        __result=Task.CompletedTask;return false;
    }
    static void Prefix(Harmony h,MethodBase method,string leaf)=>h.Patch(method,prefix:new HarmonyMethod(typeof(EnemyHealingChecks).GetMethod(leaf,F)!));
    static void Reset(bool enabled)
    {
        H("Reset");H("Mode",enabled,1);Heals.Clear();Decrements=0;SteamApplications=0;Ascension=0;
        Set(CurrentRun,"_players",new List<Player>{Player});
        Set(Combat,"_allies",new List<Creature>{Hero});
        foreach(Creature creature in new[]{Enemy,Other,Giant})
        {Set(creature,"_maxHp",5000);Set(creature,"_currentHp",100);}
        Set(Combat,"_enemies",new List<Creature>{Enemy,Other,Giant});
    }
    static RegenPower Regen(Creature owner,int amount)=>
        (RegenPower)Invoke(Host.GetMethod("Power",F)!.MakeGenericMethod(typeof(RegenPower)),null,owner,amount)!;
    static void TriggerRegen(RegenPower power,CombatSide side,Creature[]? participants=null)=>Wait(Invoke(RegenHook,power,
        new ThrowingPlayerChoiceContext(),side,participants??new[]{Enemy,Other,Giant}));
    static MethodInfo Body(MethodInfo method)=>method.GetCustomAttribute<AsyncStateMachineAttribute>() is { } state
        ? AccessTools.Method(state.StateMachineType,"MoveNext"):method;
    static void Install()
    {
        H("Setup");var h=new Harmony("hbr.enemy.healing.native.checks");
        Other=(Creature)H("Actor",Blank<SmallHopper>(),null,null)!;
        Giant=(Creature)H("Actor",Blank<WaterfallGiant>(),null,null)!;
        Prefix(h,AccessTools.Method(typeof(CreatureCmd),nameof(CreatureCmd.Heal)),nameof(HealLeaf));
        Prefix(h,AccessTools.Method(typeof(CreatureCmd),nameof(CreatureCmd.TriggerAnim)),nameof(TaskLeaf));
        Prefix(h,AccessTools.Method(typeof(PowerCmd),nameof(PowerCmd.Decrement)),nameof(DecrementLeaf));
        Prefix(h,AccessTools.Method(typeof(PowerModel),"Flash"),nameof(Skip));
        ModelDb.Inject(typeof(SteamEruptionPower));
        Set(ModelDb.Power<SteamEruptionPower>(),"<Id>k__BackingField",ModelDb.GetId(typeof(SteamEruptionPower)));
        MethodInfo steamApply=typeof(PowerCmd).GetMethods(F).Single(method=>method.Name==nameof(PowerCmd.Apply)&&
            !method.IsGenericMethod&&method.GetParameters()[1].ParameterType==typeof(PowerModel));
        Prefix(h,steamApply,nameof(SteamLeaf));
        Prefix(h,typeof(AscensionHelper).GetMethod(nameof(AscensionHelper.GetValueIfAscension),
            [typeof(AscensionLevel),typeof(int),typeof(int)])!,nameof(AscensionLeaf));
        RegenHook=typeof(RegenPower).GetMethods(F|BindingFlags.DeclaredOnly).Single(method=>
            method.Name is "AfterSideTurnEnd" or "BeforeSideTurnEndEarly" &&
            method.GetCustomAttribute<AsyncStateMachineAttribute>()!=null);
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeFixedResourceCommandPatch",true)!;
        Targets=((IEnumerable<MethodBase>)Invoke(patch.GetMethod("TargetMethods",F)!,null)!).Cast<MethodInfo>().ToArray();
        Check(h.CreateClassProcessor(patch).Patch().Count>0,"production native fixed-resource class installs against this runtime");
    }
    static void RegenCases()
    {
        foreach(bool enabled in new[]{true,false})
        {
            Reset(enabled);var first=Regen(Enemy,10);var second=Regen(Other,10);
            Set(Other,"_currentHp",4500);
            TriggerRegen(first,CombatSide.Enemy);TriggerRegen(second,CombatSide.Enemy);
            decimal expected=enabled?1000m:10m;
            Cases.Add(new{kind="regen-two-owners",enabled,hook=RegenHook.Name,requests=Heals.ToArray()});
            Check(Heals.Count==2,"one native regeneration command per owner");
            Equal(Heals[0].Requested,expected,"native regeneration10 receives exactly one fixed conversion");
            Equal(Heals[1].Requested,expected,"second owner receives the same once-converted regeneration");
            Equal(Heals[0].After-Heals[0].Before,expected,"real native HealInternal applies the requested recovery");
            Equal(Heals[1].After,enabled?5000m:4510m,"real native HealInternal caps overheal at maximum");
            Equal(first.Amount,10,"fixed read does not rewrite raw regeneration stacks");
            Check(Decrements==2,"both native hooks still dispatch their ordinary decrement leaf");
            Heals.Clear();Set(Other,"_currentHp",5000);TriggerRegen(second,CombatSide.Enemy);
            Check(Heals.Count==1&&Heals[0].After==5000,"full-health native regeneration remains capped");
            Heals.Clear();TriggerRegen(first,CombatSide.Enemy,[]);
            Check(Heals.Count==0,"callback without its owner in participants retains native no-heal condition");
            Set(Enemy,"_currentHp",0);TriggerRegen(first,CombatSide.Enemy);
            Check(Heals.Count==0,"dead-owner callback retains native no-heal condition");
        }
        Check(Targets.Contains(Body(RegenHook)),"production patch targets the declared runtime regeneration state machine");
    }
    static void WaterfallCases()
    {
        MethodInfo move=AccessTools.Method(typeof(WaterfallGiant),"SiphonMove");
        foreach(bool enabled in new[]{true,false})foreach(int ascension in new[]{0,10})foreach(int players in new[]{1,2})
        {
            Reset(enabled);Ascension=ascension;
            var participants=new List<Player>{Player};
            if(players==2)
            {
                var teammate=Blank<Player>();Set(teammate,"_runState",CurrentRun);
                Set(teammate,"_relics",new List<RelicModel>());participants.Add(teammate);
                Creature actor=(Creature)H("Actor",null,teammate,null)!;
                Set(teammate,"<Creature>k__BackingField",actor);Set(Combat,"_allies",new List<Creature>{Hero,actor});
            }
            Set(CurrentRun,"_players",participants);
            Check(Combat.Players.Count==players,"native combat player roster matches the requested player count");
            Wait(Invoke(move,Giant.Monster,(object)new[]{Hero}));
            decimal raw=(ascension>=8?15m:10m)*players;
            decimal expected=raw*(enabled?100m:1m);
            Cases.Add(new{kind="waterfall-fixed-siphon",enabled,ascension,players,requests=Heals.ToArray(),steamApplications=SteamApplications});
            Check(Heals.Count==1,"native Siphon emits one self-heal");
            Equal(Heals[0].Requested,expected,"native fixed Siphon heal scales once after ascension and player count");
            Equal(Heals[0].After-Heals[0].Before,expected,"Siphon recovery reaches real native HP mutation");
        }
        Check(Targets.Contains(Body(move)),"production patch targets native Waterfall Siphon state machine");
    }
    static void GuardCases()
    {
        foreach(bool enabled in new[]{true,false})
        {
            Reset(enabled);
            CreatureCmd.Heal(Enemy,10m,true).GetAwaiter().GetResult();
            Equal(Heals.Single().Requested,10m,"unmapped direct Heal command is not globally converted");
            Equal(Enemy.CurrentHp,110,"unmapped direct actual recovery stays10");
            var tail=Blank<LizardTail>();Set(tail,"<IsMutable>k__BackingField",true);Set(tail,"_owner",Player);
            var helper=Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrNativeFixedResourceHelper",true)!;
            Equal((decimal)Invoke(helper.GetMethod("ReadBase",F)!,null,tail.DynamicVars.Heal)!,50m,
                "LizardTail percentage recovery is excluded from fixed-resource mapping");
            Equal((decimal)H("Actual",Enemy,Hero,6m,ValueProp.Unpowered)!,6m,
                "unpowered damage is not changed by fixed healing mapping");
        }
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try{Install();RegenCases();WaterfallCases();GuardCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,heals=Heals,
            regenerationHook=RegenHook?.Name,patchTargets=Targets.Select(method=>method.DeclaringType?.FullName+"::"+method.Name),
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Actual native Regen declared turn hook and WaterfallGiant.SiphonMove, production Harmony fixed-resource transpiler, raw amount requests and real Creature.HealInternal HP/clamp execute. Native Heal command orchestration, decrement, animation/Flash, SteamEruption application and ascension input are controlled leaves. Linked stat host installs production damage adapters for an Unpowered guard. No game/Godot process, rendering, full command lifecycle, saves, multiplayer transport, package or upload."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native enemy healing checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
