using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

static class OverchargeChecks { public static void Run(string reportPath)=>CoffinChecks.Run(reportPath); }

static class CoffinChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Assembly Mod=typeof(Millsage).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    static readonly List<decimal> Heals=[];
    static Player Player=>State<Player>("Player");
    static Creature Hero=>State<Creature>("Hero");
    static CombatState Combat=>State<CombatState>("Combat");
    static Millsage Relic=null!;
    static DeflectorProtection? Shield;
    static T State<T>(string name)=>(T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static List<PowerModel> Powers=>(List<PowerModel>)H("Powers",Hero)!;
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static bool CaptureHeal(Creature __0,decimal __1,ref Task __result)
    {Heals.Add(__1);__result=Task.CompletedTask;return false;}
    static object Coffin=>Enum.Parse(typeof(Millsage).GetMethod("HasCannonUpgrade")!.GetParameters()[0].ParameterType,"Coffin");
    static void Level(int level)=>typeof(Millsage).GetProperty("CoffinUpgradeLevel",F)!.SetValue(Relic,level);
    static bool HasTier(int tier)=>(bool)Invoke(typeof(Millsage).GetMethod("HasCannonUpgrade")!,Relic,Coffin,tier)!;
    static void Start()=>((Task)Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers.HotaruCannonSupportUpgradeHelper",true)!.GetMethod("OnCombatStart",F)!,null,Relic)!).GetAwaiter().GetResult();
    static HotaruDamageAmplificationPower? Buff=>Hero.GetPower<HotaruDamageAmplificationPower>();
    static void Reset(bool hbr,int maximum,int current=1,bool shield=true,int level=5)
    {
        H("Reset");H("Mode",hbr,1);Heals.Clear();Hero.CombatState=Combat;
        Set(Hero,"_maxHp",5000);Set(Hero,"_currentHp",1);
        Relic=new Millsage();Set(Relic,"<IsMutable>k__BackingField",true);Set(Relic,"_owner",Player);Level(level);
        var relics=new List<RelicModel>{State<RelicModel>("Manual"),Relic};Shield=null;
        if(shield)
        {
            Shield=new DeflectorProtection();Set(Shield,"<IsMutable>k__BackingField",true);Set(Shield,"_owner",Player);
            // Use real CancerMaxDp/LostMaxDp state and the real MaxDp getter.
            // Initialization is already receipted so this test cannot refill DP.
            Set(Shield,"_cancerMaxDp",maximum);Set(Shield,"_lostMaxDp",maximum==0?10000:0);
            Set(Shield,"_hasInitializedDp",true);Set(Shield,"_dp",Math.Clamp(current,0,maximum));relics.Add(Shield);
        }
        Set(Player,"_relics",relics);
    }
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.hotaru-coffin-dp-amplification.checks");
        Set(Player,"<Character>k__BackingField",Blank<HBRGOSakiMod.Core.Models.Characters.HbrgoHotaru>());
        var playerCombat=Blank<PlayerCombatState>();Set(playerCombat,"<TurnNumber>k__BackingField",1);Set(Player,"<PlayerCombatState>k__BackingField",playerCombat);
        foreach(Type type in Mod.GetTypes().Where(type=>type.IsDefined(typeof(HarmonyPatch),false)&&
            (type.Name.StartsWith("ExactDamagePercent",StringComparison.Ordinal)||type.Name is "HighDimensionalShellStackAmountPatch" or "DamageAmplificationStackingPatch")))
            Check(harmony.CreateClassProcessor(type).Patch().Count>0,"installs actual precision/stacking adapter "+type.Name);
        ModelDb.Inject(typeof(HotaruDamageAmplificationPower));
        Set(ModelDb.Power<HotaruDamageAmplificationPower>(),"<Id>k__BackingField",ModelDb.GetId(typeof(HotaruDamageAmplificationPower)));
        foreach(MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(method=>method.Name=="Heal"&&method.ReturnType==typeof(Task)))
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(CoffinChecks).GetMethod(nameof(CaptureHeal),F)!));
    }
    static void ConversionCases()
    {
        foreach(bool hbr in new[]{true,false})foreach(int maximum in new[]{6000,53301,99,100,0})
        {
            Reset(hbr,maximum,maximum>0?Math.Min(7,maximum):0);
            Check(HasTier(5),"real Coffin level5 predicate is enabled for conversion case");
            Equal(Shield!.MaxDp,maximum,"real Deflector maximum matches native saved state");int current=Shield.Dp;
            Start();decimal expected=maximum/100m;
            Cases.Add(new{hbr,maximum,current,buffPercent=Buff?.DamagePercent,proxy=Buff?.Amount,heals=Heals.ToArray(),afterDp=Shield.Dp});
            if(maximum==0)Check(Buff==null&&Powers.All(power=>power is not HotaruDamageAmplificationPower),"zero maximum DP does not attach any amplification power");
            else
            {
                Check(Buff!=null&&Powers.Count(power=>power is HotaruDamageAmplificationPower)==1,"real combat-start native Apply attaches one canonical amplification power");
                Equal(Buff!.DamagePercent,expected,$"mode{hbr} maximum{maximum} converts100 DP to one exact amplification percentage point");
                Check(Buff.AmplificationPercentData==expected.ToString(System.Globalization.CultureInfo.InvariantCulture),"exact amplification bank retains combat-start fractional award");
            }
            Equal(Shield.Dp,current,"Coffin tier5 award consumes no current DP");Equal(Shield.MaxDp,maximum,"Coffin tier5 award consumes no maximum DP");
            Check(Hero.CurrentHp==1&&Heals.Count==1,"unrelated tier3 healing is isolated at Heal leaf with real tier predicate intact");
        }
    }
    static void MaximumAndStackingCases()
    {
        foreach(bool hbr in new[]{true,false})
        {
            foreach(int current in new[]{0,1,5999,6000})
            {
                Reset(hbr,6000,current);Start();Equal(Buff!.DamagePercent,60m,"combat-start award reads maximum6000 rather than current DP"+current);
                Equal(Shield!.Dp,current,"current DP unchanged across max-based amplification award");
            }
            Reset(hbr,53301,1);
            var existing=PowerCmd.Apply<HotaruDamageAmplificationPower>(null!,Hero,.375m,Hero,null,true).GetAwaiter().GetResult()!;
            Equal(existing.DamagePercent,.375m,"existing exact amplification created through native Apply before combat-start award");
            Start();Check(ReferenceEquals(existing,Buff)&&Powers.Count(power=>power is HotaruDamageAmplificationPower)==1,"combat-start native stacking keeps existing amplification instance");
            Equal(Buff!.DamagePercent,533.385m,"combat-start533point01 stacks exactly with existing0point375 amplification");
        }
    }
    static void GateCases()
    {
        foreach(bool hbr in new[]{true,false})
        {
            Reset(hbr,6000,1,level:4);Check(!HasTier(5),"real level4 does not unlock Coffin tier5");Start();Check(Buff==null,"locked Coffin tier5 awards no amplification");
            Reset(hbr,6000,1);typeof(Millsage).GetProperty("RetiredCannonsMask",F)!.SetValue(Relic,1<<Convert.ToInt32(Coffin));
            Check(!HasTier(5),"real permanent-loss predicate disables Coffin tier5");Start();Check(Buff==null,"permanently lost Coffin awards no amplification");
            Reset(hbr,6000,1,shield:false);Start();Check(Buff==null,"missing Deflector awards no amplification");
            Reset(hbr,6000,1);Set(Hero,"_currentHp",0);Start();Check(Buff==null&&Heals.Count==0,"dead owner exits before heal or amplification");
            Reset(hbr,6000,1);Hero.CombatState=null;Start();Check(Buff==null&&Heals.Count==0,"owner outside combat exits before heal or amplification");
        }
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try{Install();ConversionCases();MaximumAndStackingCases();GateCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),
            sourceNativeSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),telemetryIsolation=System.Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real HotaruCannonSupportUpgradeHelper.OnCombatStart, real Millsage upgrade/retirement predicates and Deflector MaxDp getter/EnsureDpInitialized; actual native PowerCmd.Apply/ModifyAmount with production exact-percentage and stacking adapters. Only unrelated tier3 CreatureCmd.Heal leaf is captured; HP input leaves enough missing health to prevent overflow restoration. No tier predicate, DP getter, award amount or native power storage is replaced. Shared host isolates StringName/logging. No game process, heal HP mutation, clone/save suite, rendering, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" Coffin DP-amplification checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}
