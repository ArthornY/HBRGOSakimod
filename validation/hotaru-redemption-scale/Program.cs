using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks { public static void Run(string reportPath)=>RedemptionChecks.Run(reportPath); }

static class RedemptionChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Assembly Mod=typeof(Millsage).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    static readonly Queue<(int[] Hp,decimal Dp)> Losses=[];
    static bool Old=>Environment.GetEnvironmentVariable("HBR_EXPECT_OLD_REDEMPTION")=="1";
    static Player Player=>State<Player>("Player");
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static CombatState Combat=>State<CombatState>("Combat");
    static Millsage Relic=null!;
    static Creature Cannon=null!,Endgame=null!;
    static int Commands;
    static HotaruTurnDamageAmplificationPower? Buff=>Hero.GetPower<HotaruTurnDamageAmplificationPower>();
    static T State<T>(string name)=>(T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static object? Call(string helper,string method,params object?[] args)=>Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers."+helper,true)!.GetMethod(method,F)!,null,args);
    static void Wait(object? task)=>((Task)task!).GetAwaiter().GetResult();
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static decimal Expected(bool hbr,decimal actual)=>hbr&&!Old?actual/5000m:actual;
    static int ContestBit=>1<<Convert.ToInt32(Enum.Parse(typeof(HotaruCannonModel).GetProperty("Kind")!.PropertyType,"Contest"));
    static void Reset(bool hbr=true,int sign=1,bool retired=true,bool endgame=true)
    {
        H("Reset");H("Mode",hbr,1);Commands=0;Losses.Clear();
        Relic=new Millsage();Set(Relic,"<IsMutable>k__BackingField",true);Set(Relic,"_owner",Player);
        Set(Relic,"_layers",sign>0?2:1);
        if(retired)Set(Relic,"_retiredCannonsMask",ContestBit);
        var relics=new List<RelicModel>{State<RelicModel>("Manual"),Relic};
        if(sign<0)
        {
            var rainbow=new HotaruIridescence();Set(rainbow,"<IsMutable>k__BackingField",true);Set(rainbow,"_owner",Player);Set(rainbow,"_layers",2);relics.Add(rainbow);
        }
        Set(Player,"_relics",relics);
        Set(Combat,"_allies",endgame?new List<Creature>{Hero,Cannon,Endgame}:new List<Creature>{Hero,Cannon});
        Set(Cannon,"_currentHp",5000);Set(Endgame,"_currentHp",5000);
        Check(Math.Sign(Relic.EndgameSign)==sign,"real Layers/Iridescence determines requested endgame sign "+sign);
    }
    // Isolate only terminal HP/DP execution. Production intent, scope binding,
    // one-use receipt, OnCannonDamage, native Apply and exact state stay real.
    static bool DamageLeaf(MethodBase __originalMethod,object?[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {
        var parameters=__originalMethod.GetParameters();
        object? Arg(Type type)=>__args[Array.FindIndex(parameters,p=>p.ParameterType==type)];
        var targets=((IEnumerable<Creature>)Arg(typeof(IEnumerable<Creature>))!).ToArray();
        __result=ExecuteDamage(targets,(decimal)Arg(typeof(decimal))!,(ValueProp)Arg(typeof(ValueProp))!,
            (Creature)Arg(typeof(Creature))!,(CardModel?)Arg(typeof(CardModel)));return false;
    }
    static async Task<IEnumerable<DamageResult>> ExecuteDamage(Creature[] targets,decimal amount,ValueProp props,Creature dealer,CardModel? card)
    {
        Commands++;object?[] args=[targets,amount,props,dealer,card,null];
        object? scope=Call("HotaruCannonStatsHelper","Begin",args);
        try
        {
            var loss=Losses.Dequeue();Creature target=targets.Single();
            decimal final=(decimal)H("Actual",target,dealer,amount,props)!;
            Call("HotaruCannonStatsHelper","RecordFinalDamage",target,dealer,card,final);
            if(loss.Dp>0)Call("HotaruCannonStatsHelper","RecordDpAbsorption",target,loss.Dp);
            var results=loss.Hp.Select(hp=>{var r=Blank<DamageResult>();Set(r,"<Receiver>k__BackingField",target);Set(r,"<UnblockedDamage>k__BackingField",hp);return r;}).ToArray();
            Call("HotaruCannonStatsHelper","BindReceipts",new object?[]{results});
            object?[] take=[results[0],null];Check((bool)Call("HotaruCannonStatsHelper","TryTakeReceipt",take)!,"production scope provides one terminal receipt");
            object receipt=take[1]!;
            decimal Number(string name)=>(decimal)receipt.GetType().GetProperty(name,F)!.GetValue(receipt)!;
            Equal(Number("ActualHpDamage"),loss.Hp.Sum(),"receipt sums terminal HP losses only");
            Equal(Number("ActualDpDamage"),loss.Dp,"receipt retains actual DP loss");
            Equal(Number("ActualDamage"),loss.Hp.Sum()+loss.Dp,"actual damage equals HP plus DP exactly once");
            foreach(var result in results)
            {object?[] repeat=[result,null];Check(!(bool)Call("HotaruCannonStatsHelper","TryTakeReceipt",repeat)!,"every bound result rejects repeated receipt consumption");}
            await (Task)Call("HotaruEndgameEffectsHelper","OnCannonDamage",null,receipt)!;
            return results;
        }
        finally{Call("HotaruCannonStatsHelper","Close",scope);}
    }
    static bool Hit(int hp,decimal dp=0m,bool split=false)
    {
        Losses.Enqueue((split?new[]{hp/2,hp-hp/2}:new[]{hp},dp));
        return ((Task<bool>)Call("HotaruCannonStatsHelper","Damage",Cannon,Enemy,9999m,null,null,null,1)!).GetAwaiter().GetResult();
    }
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.hotaru-redemption-scale.checks");
        foreach(Type type in new[]{typeof(HotaruTurnDamageAmplificationPower),typeof(HotaruDamageAmplificationPower)})
        {ModelDb.Inject(type);Set(ModelDb.GetById<PowerModel>(ModelDb.GetId(type)),"<Id>k__BackingField",ModelDb.GetId(type));}
        foreach(Type type in Mod.GetTypes().Where(type=>type.IsDefined(typeof(HarmonyPatch),false)&&
            (type.Name.StartsWith("ExactDamagePercent",StringComparison.Ordinal)||type.Name is "HighDimensionalShellStackAmountPatch" or "DamageAmplificationStackingPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count>0,"installs actual precision adapter "+type.Name);
        Cannon=(Creature)H("Actor",Blank<HotaruHappinessCannon>(),null,Player)!;
        Endgame=(Creature)H("Actor",Blank<HotaruFinalCannon>(),null,Player)!;
        var pc=Blank<PlayerCombatState>();Set(pc,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",pc);
        int patched=0;
        foreach(MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(m=>m.Name=="Damage"&&m.ReturnType==typeof(Task<IEnumerable<DamageResult>>)&&m.GetParameters().Any(p=>p.ParameterType==typeof(IEnumerable<Creature>))))
        {harmony.Patch(method,prefix:new HarmonyMethod(typeof(RedemptionChecks).GetMethod(nameof(DamageLeaf),F)!));patched++;}
        Check(patched>0,"isolates terminal native damage execution leaf");
    }
    static void ConversionCases()
    {
        foreach(bool hbr in new[]{true,false})
        foreach(var loss in new[]{(Hp:5000,Dp:0m),(Hp:82693,Dp:0m),(Hp:600,Dp:4400m),(Hp:0,Dp:5000m),(Hp:1,Dp:0m)})
        {
            Reset(hbr);Check(Hit(loss.Hp,loss.Dp,split:true),"positive terminal HP/DP command reports damage");
            decimal expected=Expected(hbr,loss.Hp+loss.Dp);
            Equal(Buff!.DamagePercent,expected,$"mode{hbr} actual{loss.Hp+loss.Dp} converts to exact redemption percentage");
            Equal(decimal.Parse(Buff.AmplificationPercentData,System.Globalization.CultureInfo.InvariantCulture),expected,"saved exact percentage retains all fractional digits");
            Cases.Add(new{hbr,hp=loss.Hp,dp=loss.Dp,actual=loss.Hp+loss.Dp,percent=Buff.DamagePercent,proxy=Buff.Amount});
        }
        Reset();Hit(2500);var original=Buff;Hit(2500);
        Check(ReferenceEquals(original,Buff),"native Apply stacks into one existing turn amplification instance");
        Equal(Buff!.DamagePercent,Expected(true,5000m),"two actual2500 awards stack without fractional loss");
    }
    static void GateCases()
    {
        Reset();Check(!Hit(0),"blocked terminal command reports no actual damage");Check(Buff==null,"blocked/zero HP and DP receipt grants no buff");
        Reset();Set(Enemy,"_currentHp",0);Check(!Hit(5000)&&Commands==0&&Buff==null,"no live target issues no command and grants no buff");Set(Enemy,"_currentHp",5000);
        Reset(sign:-1);Hit(5000);Check(Buff==null,"negative endgame form grants no positive-form turn damage");
        Reset(sign:0);Hit(5000);Check(Buff==null,"balanced sign grants no redemption turn damage");
        Reset(retired:false);Hit(5000);Check(Buff==null,"unretired Contest grants no inherited buff");
        Reset(endgame:false);Hit(5000);Check(Buff==null,"missing Endgame cannon grants no inherited buff");
    }
    static void DamageAndTurnCases()
    {
        Reset();decimal baseline=(decimal)H("Actual",Enemy,Hero,700m,ValueProp.Move)!;
        Equal(baseline,450000m,"real native Hook and HBR formula establish exact damage control");Hit(5000);
        decimal percent=Expected(true,5000m);
        for(int i=0;i<3;i++)Equal((decimal)H("Actual",Enemy,Hero,700m,ValueProp.Move)!,decimal.Floor(baseline*(1m+percent/100m)),"turn amplification participates in final damage once and remains stable "+i);
        var permanent=PowerCmd.Apply<HotaruDamageAmplificationPower>(null!,Hero,.375m,Hero,null,true).GetAwaiter().GetResult()!;
        Equal(permanent.DamagePercent,.375m,"ordinary battle amplification keeps exact independent pool");
        MethodInfo boundary=typeof(Millsage).GetMethod("BeforeSideTurnStart",F)!;
        Wait(Invoke(boundary,Relic,null,CombatSide.Enemy,new Creature[]{Enemy},Combat));
        Equal(Buff!.DamagePercent,percent,"enemy side start does not clear current owner-turn award");
        Wait(Invoke(boundary,Relic,null,CombatSide.Player,Array.Empty<Creature>(),Combat));
        Equal(Buff!.DamagePercent,percent,"owner absent from participants does not clear award");
        Wait(Invoke(boundary,Relic,null,CombatSide.Player,new Creature[]{Hero},Combat));
        Check(Buff==null,"next owner start removes prior turn amplification through native Remove");
        Check(ReferenceEquals(permanent,Hero.GetPower<HotaruDamageAmplificationPower>()),"turn clear preserves ordinary battle amplification instance");
        Equal(permanent.DamagePercent,.375m,"turn clear preserves ordinary exact amplification percent");
        Hit(2500);Equal(Buff!.DamagePercent,Expected(true,2500m),"new start receipt builds a fresh turn pool without prior-turn carryover");
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try{Install();ConversionCases();GateCases();DamageAndTurnCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,negativeControl=Old,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real DamageCore pending intent, Begin/RecordDpAbsorption/BindReceipts/TryTakeReceipt and HotaruEndgameEffectsHelper.OnCannonDamage; native PowerCmd.Apply/ModifyAmount/Remove and production exact-percent adapters; real native Hook + StrongestShield actual wrapper for following damage; actual Millsage ownership/retirement/sign/next-turn-clear predicates. Only terminal HP/DP loss execution is controlled; shared host isolates StringName/logging and enumerates fixture listeners. Other FinishAfterDamageGiven reactions are outside this focused test. No gameplay method, award formula or exact-percent state is stubbed. No game process, Godot invocation, renderer, full HP mutation, OD turn coordinator, package or installed output changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" redemption scale checks"+(Old?" (old-bug negative control)":""));if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
