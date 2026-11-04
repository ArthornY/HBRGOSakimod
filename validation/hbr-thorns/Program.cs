using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

// Reuse the hash-verified bootstrap without modifying the overcharge fixture.
static class OverchargeChecks { public static void Run(string reportPath)=>ThornsChecks.Run(reportPath); }

static class ThornsChecks
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    static readonly List<Command> Captured=[];
    sealed record Command(decimal Amount,ValueProp Props,Creature[] Targets,Creature? Dealer,CardModel? Source,object? Context,string Signature);
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static Player Player=>State<Player>("Player");
    static T State<T>(string name)=>(T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string name,params object?[] args)=>Invoke(typeof(Checks).GetMethod(name,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static ThornsPower Power(Creature owner,int amount)=>(ThornsPower)Invoke(typeof(Checks).GetMethod("Power",F)!.MakeGenericMethod(typeof(ThornsPower)),null,owner,amount)!;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static void Reset(bool hbr){H("Reset");H("Mode",hbr,1);Captured.Clear();}
    static bool Skip()=>false;
    static bool EnergyPrefix(ref string __result){__result="energy";return false;}
    static object? ByType(ParameterInfo[] parameters,object?[] arguments,Type type)=>arguments[Array.FindIndex(parameters,p=>p.ParameterType==type)];
    static bool DamageLeaf(MethodBase __originalMethod,object?[] __args,ref Task<IEnumerable<DamageResult>> __result)
    {
        var parameters=__originalMethod.GetParameters();
        decimal amount=(decimal)ByType(parameters,__args,typeof(decimal))!;
        ValueProp props=(ValueProp)ByType(parameters,__args,typeof(ValueProp))!;
        int targetsIndex=Array.FindIndex(parameters,p=>typeof(IEnumerable<Creature>).IsAssignableFrom(p.ParameterType));
        Creature[] targets=targetsIndex>=0?((IEnumerable<Creature>)__args[targetsIndex]!).ToArray():[(Creature)__args[Array.FindIndex(parameters,p=>p.ParameterType==typeof(Creature))]!];
        int dealerIndex=Array.FindIndex(parameters,p=>p.Name=="dealer");
        if(dealerIndex<0)dealerIndex=Array.FindLastIndex(parameters,p=>p.ParameterType==typeof(Creature));
        int sourceIndex=Array.FindIndex(parameters,p=>p.ParameterType==typeof(CardModel));
        int contextIndex=Array.FindIndex(parameters,p=>p.ParameterType==typeof(PlayerChoiceContext));
        Captured.Add(new(amount,props,targets,dealerIndex<0?null:(Creature?)__args[dealerIndex],sourceIndex<0?null:(CardModel?)__args[sourceIndex],contextIndex<0?null:__args[contextIndex],__originalMethod.ToString()!));
        __result=Task.FromResult<IEnumerable<DamageResult>>([]);return false;
    }
    static MethodInfo NativeHook=>typeof(ThornsPower).GetMethods(F).Single(m=>m.Name==nameof(PowerModel.BeforeDamageReceived)&&m.DeclaringType==typeof(ThornsPower));
    static void Receive(ThornsPower power,Creature receiver,Creature? dealer,ValueProp props,CardModel? card=null,PlayerChoiceContext? context=null)
    {
        int creatureIndex=0;
        object?[] args=NativeHook.GetParameters().Select(parameter=>
            parameter.ParameterType==typeof(Creature)?(object?)(creatureIndex++==0?receiver:dealer):
            parameter.ParameterType==typeof(decimal)?5m:
            parameter.ParameterType==typeof(ValueProp)?props:
            parameter.ParameterType==typeof(CardModel)?card:
            parameter.ParameterType==typeof(PlayerChoiceContext)?context:null).ToArray();
        ((Task)Invoke(NativeHook,power,args)!).GetAwaiter().GetResult();
    }
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.native-thorns.checks");int leaves=0;
        foreach(MethodInfo method in typeof(CreatureCmd).GetMethods(F).Where(m=>m.Name=="Damage"&&m.ReturnType==typeof(Task<IEnumerable<DamageResult>>)&&m.GetParameters().Any(p=>p.ParameterType==typeof(decimal))))
        {harmony.Patch(method,prefix:new HarmonyMethod(typeof(ThornsChecks).GetMethod(nameof(DamageLeaf),F)!));leaves++;}
        Check(leaves>0,"captures only native leaf damage commands");
        if(typeof(PowerModel).GetMethod("Flash",F) is {} flash)harmony.Patch(flash,prefix:new HarmonyMethod(typeof(ThornsChecks).GetMethod(nameof(Skip),F)!));
        foreach(MethodInfo method in typeof(SfxCmd).GetMethods(F).Where(m=>m.Name=="Play"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(ThornsChecks).GetMethod(nameof(Skip),F)!));
        Type energy=typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(MethodInfo method in energy.GetMethods(F).Where(m=>m.Name=="GetPrefix"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(ThornsChecks).GetMethod(nameof(EnergyPrefix),F)!));
        Type? damagePatch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeThornsDamagePatch");
        if(damagePatch!=null)Check(harmony.CreateClassProcessor(damagePatch).Patch().Count==1,"installs real production native Thorns async-body transpiler");
        else Passed.Add("baseline intentionally lacks Thorns damage patch");
        Type descriptionPatch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeFixedPowerDescriptionPatch",true)!;
        Check(harmony.CreateClassProcessor(descriptionPatch).Patch().Count==1,"installs real native fixed-power description postfix");
        var stateType=NativeHook.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        Console.WriteLine("Native hook: "+NativeHook+"; "+stateType.FullName);
    }
    static void RetaliationCases()
    {
        foreach(bool hbr in new[]{true,false})foreach(bool enemyOwner in new[]{false,true})foreach(int amount in new[]{2,1,int.MaxValue})
        {
            Reset(hbr);Creature defender=enemyOwner?Enemy:Hero;Creature attacker=enemyOwner?Hero:Enemy;
            var power=Power(defender,amount);var source=Blank<StrikeIronclad>();Set(source,"<IsMutable>k__BackingField",true);Set(source,"_owner",Player);
            var context=new ThrowingPlayerChoiceContext();Receive(power,defender,attacker,ValueProp.Move,source,context);
            Cases.Add(new{hbr,enemyOwner,amount,commands=Captured.Select(c=>new{c.Amount,props=c.Props.ToString(),targets=c.Targets.Select(t=>ReferenceEquals(t,attacker)?"attacker":"other"),dealerIsDefender=ReferenceEquals(c.Dealer,defender),sourceIsNull=c.Source==null,sameContext=ReferenceEquals(c.Context,context),c.Signature}).ToArray()});
            Check(Captured.Count==1,"native Thorns issues one retaliation per eligible powered attack");
            Command command=Captured.Single();
            Equal(command.Amount,amount,$"HBR{hbr} enemyOwner{enemyOwner} native Thorns retains raw skill units");
            Check(command.Targets.Length==1&&ReferenceEquals(command.Targets[0],attacker),"native retaliation keeps original attacker as sole target");
            Check(ReferenceEquals(command.Dealer,defender)&&command.Source==null&&ReferenceEquals(command.Context,context),"native retaliation keeps defender dealer, null source and original choice context");
            Check(command.Props==(ValueProp.Unpowered|ValueProp.SkipHurtAnim),"native retaliation preserves Unpowered recursion guard and SkipHurtAnim flags");
            Check(power.Amount==amount&&power.DisplayAmount==amount,"retaliation leaves native stack amount and visible counter unchanged");
        }
    }
    static void TriggerCases()
    {
        foreach(bool hbr in new[]{true,false})
        {
            Reset(hbr);var power=Power(Hero,2);
            foreach(ValueProp props in new[]{ValueProp.Unpowered,ValueProp.Move|ValueProp.Unpowered})
            {Receive(power,Hero,Enemy,props);Check(Captured.Count==0,"native nonpowered damage does not trigger Thorns");}
            Receive(power,Enemy,Enemy,ValueProp.Move);Check(Captured.Count==0,"native handler ignores damage to a different receiver");
            Receive(power,Hero,null,ValueProp.Move);Check(Captured.Count==0,"native handler ignores damage without a dealer");
            Receive(power,Hero,Enemy,ValueProp.Unpowered,Blank<Omnislice>());
            Check(Captured.Count==1,"native Omnislice exception still retaliates even when incoming damage is Unpowered");
            Equal(Captured[0].Amount,2m,"native Omnislice exception retains raw retaliation skill units");
            Check(Captured[0].Props==(ValueProp.Unpowered|ValueProp.SkipHurtAnim)&&Captured[0].Source==null&&ReferenceEquals(Captured[0].Dealer,Hero)&&ReferenceEquals(Captured[0].Targets.Single(),Enemy),"Omnislice exception retains native outgoing attribution and recursion flags");
            Equal((decimal)H("Actual",Enemy,Hero,7m,ValueProp.Unpowered)!,7m,"unknown already-resolved Unpowered damage is never globally scaled");
            Check(power.Amount==2&&power.DisplayAmount==2,"ineligible damage leaves native Thorns counter unchanged");
        }
    }
    static LocString Description(PowerModel power,int? amount=null)
    {
        var text=new LocString("powers","fixture.thorns");text.Add("Marker",17);
        Invoke(AccessTools.Method(typeof(PowerModel),"AddDumbVariablesToDescription"),power,text,amount);return text;
    }
    static void DescriptionCases()
    {
        foreach(bool hbr in new[]{true,false})foreach(bool enemyOwner in new[]{false,true})
        {
            Reset(hbr);var power=Power(enemyOwner?Enemy:Hero,2);
            foreach(int? amount in new int?[]{null,7,int.MaxValue})
            {
                LocString text=Description(power,amount);Equal(Convert.ToDecimal(text.Variables["Amount"]),amount??2,"native description Amount binding keeps stacks or override, not a resolved damage amount");
                Check(Convert.ToDecimal(text.Variables["Marker"])==17m,"description retains unrelated variables");
                Check(power.Amount==2&&power.DisplayAmount==2,"description amount override never changes stored stacks or counter");
            }
            var canonical=Blank<ThornsPower>();Equal(Convert.ToDecimal(Description(canonical,7).Variables["Amount"]),7m,"canonical immutable Thorns description keeps native override without reading Owner");
            var detached=Power(Hero,2);Set(detached,"_owner",null);Equal(Convert.ToDecimal(Description(detached,7).Variables["Amount"]),7m,"detached mutable Thorns description retains native amount");
        }
    }
    public static void Run(string reportPath)
    {
        bool ok=false;string? error=null;
        try{Install();RetaliationCases();TriggerCases();DescriptionCases();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real native ThornsPower.BeforeDamageReceived and source adapter; terminal raw skill-unit commands, attribution, native trigger gates and stack/override variables are captured. Unknown Unpowered damage is separately checked through the actual wrapper. The terminal capture bypasses the canonical command scope, so skill resolution, RNG and rendered percentage descriptions belong to the separate effect-skill fixture. Logging/StringName, Flash/audio/energy-icon prefix and HP command execution isolated. No game process, HP mutation, rendered tooltip, complete combat, saves, packaging or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native Thorns checks");if(!ok)throw new Exception("Inspect "+reportPath);
    }
}





