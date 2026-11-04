using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

static class OverchargeChecks {public static void Run(string path)=>AnchorChecks.Run(path);}
static class AnchorChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    sealed record BlockCall(Creature Target,decimal Amount,MegaCrit.Sts2.Core.ValueProps.ValueProp Props,CardPlay? Play,bool Fast);
    static readonly List<BlockCall> Calls=[];
    static int HookCalls;
    static Player Player=>State<Player>("Player");
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static Anchor Relic=null!;
    static MethodInfo Typed=null!,Decimal=null!;
    static T State<T>(string name)=>(T)typeof(Checks).GetField(name,F)!.GetValue(null)!;
    static object? Invoke(MethodInfo method,object? owner,params object?[] args)
    {try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? H(string method,params object?[] args)=>Invoke(typeof(Checks).GetMethod(method,F)!,null,args);
    static void Set(object owner,string field,object? value)=>H("Set",owner,field,value);
    static T Blank<T>()=>(T)Invoke(typeof(Checks).GetMethod("Blank",F)!.MakeGenericMethod(typeof(T)),null)!;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Equal(decimal actual,decimal expected,string message)=>Check(actual==expected,$"{message}: {actual} = {expected}");
    static bool Skip()=>false;
    static bool Energy(ref string __result){__result="energy";return false;}
    static bool Pool(ref IPoolModel __result)
    {Type type=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool",true)!;__result=(IPoolModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);Set(__result,"<Id>k__BackingField",ModelDb.GetId(type));return false;}
    static void ObserveDecimal(object[] __args)=>Calls.Add(new((Creature)__args[0],(decimal)__args[1],(MegaCrit.Sts2.Core.ValueProps.ValueProp)__args[2],(CardPlay?)__args[3],(bool)__args[4]));
    static void ObserveHook()=>HookCalls++;
    static decimal Block(Creature owner)=>Convert.ToDecimal(owner.Block,CultureInfo.InvariantCulture);
    static void ClearBlock(Creature owner)
    {
        FieldInfo field=typeof(Creature).GetFields(F).Single(field=>field.Name is "_block" or "<Block>k__BackingField");field.SetValue(owner,Convert.ChangeType(0,field.FieldType,CultureInfo.InvariantCulture));
    }
    static T Owned<T>()where T:RelicModel,new()
    {var relic=new T();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);Set(relic,"<Id>k__BackingField",ModelDb.GetId(typeof(T)));return relic;}
    static void Reset(bool hbr=true,int talent=0)
    {H("Reset");H("Mode",hbr,1);H("Sense","Talent",talent);ClearBlock(Hero);ClearBlock(Enemy);Relic=Owned<Anchor>();Set(Player,"_relics",new List<RelicModel>{State<RelicModel>("Manual"),Relic});Calls.Clear();HookCalls=0;}
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.anchor-block-skill.checks");
        foreach(string name in new[]{"HbrNativeAnchorBlockSkillPatch","HbrNativeEffectSkillDescriptionPatch","HbrNativeEffectSkillTextPatch","HbrMerchantRelicSkillDescriptionPatch"})
            if(Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name) is {} patch)Check(harmony.CreateClassProcessor(patch).Patch().Count>0,"installs actual Anchor block/description boundary "+name);
        Typed=typeof(CreatureCmd).GetMethods(F).Single(method=>method.Name=="GainBlock"&&method.GetParameters() is var p&&p.Length==4&&p[1].ParameterType==typeof(BlockVar));
        Decimal=typeof(CreatureCmd).GetMethods(F).Single(method=>method.Name=="GainBlock"&&method.GetParameters() is var p&&p.Length==5&&p[1].ParameterType==typeof(decimal));
        Type history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
        harmony.Patch(history.GetMethod("BlockGained",F)!,prefix:new HarmonyMethod(typeof(AnchorChecks).GetMethod(nameof(Skip),F)!));
        harmony.Patch(Decimal,prefix:new HarmonyMethod(typeof(AnchorChecks).GetMethod(nameof(ObserveDecimal),F)!));
        harmony.Patch(typeof(Hook).GetMethod("ModifyBlock",F)!,prefix:new HarmonyMethod(typeof(AnchorChecks).GetMethod(nameof(ObserveHook),F)!));
        foreach(MethodInfo flash in typeof(RelicModel).GetMethods(F).Where(method=>method.Name=="Flash"))harmony.Patch(flash,prefix:new HarmonyMethod(typeof(AnchorChecks).GetMethod(nameof(Skip),F)!));
        Type energy=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(MethodInfo method in energy.GetMethods(F).Where(method=>method.Name=="GetPrefix"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(AnchorChecks).GetMethod(nameof(Energy),F)!));
        foreach(MethodInfo method in energy.GetMethods(F).Where(method=>method.Name=="GetPool"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(AnchorChecks).GetMethod(nameof(Pool),F)!));
    }
    static void Gain(Creature target,BlockVar variable,CardPlay? play=null,bool fast=false)=>((Task)Invoke(Typed,null,target,variable,play,fast)!).GetAwaiter().GetResult();
    static void Verify(string label,decimal expected,Creature target,CardPlay? play=null,bool fast=false)
    {
        Cases.Add(new{label,actualBlock=Block(target),raw=Relic.DynamicVars.Block.BaseValue,calls=Calls.Select(call=>new{call.Amount,props=call.Props.ToString(),targetOwner=ReferenceEquals(call.Target,Hero),samePlay=ReferenceEquals(call.Play,play),call.Fast}).ToArray(),HookCalls});
        Equal(Block(target),expected,label+" reaches actual native GainBlockInternal result");
        Check(Calls.Count==1&&ReferenceEquals(Calls[0].Target,target)&&Calls[0].Props==MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered&&ReferenceEquals(Calls[0].Play,play)&&Calls[0].Fast==fast,label+" preserves original decimal command target Unpowered CardPlay and fast parameters");
        Check(HookCalls==1,label+" executes native block modifier Hook exactly once");Equal(Relic.DynamicVars.Block.BaseValue,10m,label+" leaves original Anchor BlockVar raw10 unchanged");
    }
    static void NativeCases()
    {
        Reset();((Task)Invoke(typeof(Anchor).GetMethod("BeforeCombatStart",F)!,Relic)!).GetAwaiter().GetResult();Verify("real native Anchor BeforeCombatStart INT100",1000m,Hero);
        Equal(Calls.Single().Amount,1000m,"real Anchor typed consumer resolves ten units once before original Unpowered decimal command");
        Reset(talent:25);((Task)Invoke(typeof(Anchor).GetMethod("BeforeCombatStart",F)!,Relic)!).GetAwaiter().GetResult();Verify("real native Anchor BeforeCombatStart INT150",1500m,Hero);
        Reset(false);((Task)Invoke(typeof(Anchor).GetMethod("BeforeCombatStart",F)!,Relic)!).GetAwaiter().GetResult();Verify("legacy native Anchor",10m,Hero);
        Reset();var source=new DefendIronclad();Set(source,"<IsMutable>k__BackingField",true);Set(source,"_owner",Player);var play=Blank<CardPlay>();Set(play,"<Card>k__BackingField",source);Gain(Hero,Relic.DynamicVars.Block,play,true);Verify("explicit original CardPlay and fast",1000m,Hero,play,true);
        Reset();Gain(Enemy,Relic.DynamicVars.Block);Verify("Anchor variable to different recipient stays native",10m,Enemy);
        Reset();var clone=(BlockVar)Relic.DynamicVars.Block.Clone();clone.SetOwner(Relic);Gain(Hero,clone);Verify("noncanonical clone variable identity stays native",10m,Hero);
        Reset();var other=Owned<HornCleat>();decimal raw=other.DynamicVars.Block.BaseValue;Gain(Hero,other.DynamicVars.Block);Verify("other native relic BlockVar stays native",raw,Hero);
        Reset(talent:25);Gain(Hero,Relic.DynamicVars.Block);Gain(Hero,Relic.DynamicVars.Block);Equal(Block(Hero),3000m,"two actual Anchor grants add1500 each without multiplying raw storage or prior block");Equal(Relic.DynamicVars.Block.BaseValue,10m,"repeated actual grants preserve raw ten units");
    }
    static string Root
    {get{for(DirectoryInfo? folder=new(AppContext.BaseDirectory);folder!=null;folder=folder.Parent)if(File.Exists(Path.Combine(folder.FullName,"HBRGOSakiMod.csproj")))return folder.FullName;throw new DirectoryNotFoundException();}}
    static void Localization()
    {
        string runtime="D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(context,name)=>{string path=Path.Combine(runtime,name.Name+".dll");return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>();foreach(string table in new[]{"relics","static_hover_tips","card_keywords"})
        {
            var text=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("D:/stsmod/sts/localization/zhs/"+table+".json"))!;
            foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Root,"HBRGOSakiMod/localization/zhs/"+table+".json")))!)text[pair.Key]=pair.Value;
            tables[table]=new LocTable(table,text,null!);
        }
        Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
    }
    static void DescriptionCases()
    {
        Reset();Localization();string text=Relic.DynamicDescription.GetFormattedText();Check(text.Contains("200%")&&!text.Contains("166.67")&&!text.Contains('{'),"real owned Anchor description uses Block200percent denominator5 instead of damage denominator6");Equal(Relic.DynamicVars.Block.BaseValue,10m,"owned description does not rewrite raw Anchor block");
        var merchant=new Anchor();Check(!merchant.DynamicDescription.GetFormattedText().Contains("200%"),"unowned canonical Anchor remains native outside merchant scope");
        Type helper=Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrNativeEffectSkillHelper",true)!;
        using((IDisposable)Invoke(helper.GetMethod("BeginMerchantDisplay",F)!,null,merchant,Player)!)Check(merchant.DynamicDescription.GetFormattedText().Contains("200%"),"real merchant scope renders unowned Anchor200percent block skill");
        Check(!merchant.DynamicDescription.GetFormattedText().Contains("200%"),"merchant scope cleanup restores canonical native Anchor description");
        H("Mode",false,1);Check(!Relic.DynamicDescription.GetFormattedText().Contains("200%"),"legacy Anchor description remains native ten block");
    }
    public static void Run(string path)
    {
        bool ok=false;string? error=null;try{Install();NativeCases();DescriptionCases();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real native Anchor.BeforeCombatStart and typed GainBlock consumer, production identity/owner/HBR prefix, actual decimal GainBlock, native Hook.ModifyBlock and actual GainBlockInternal execute; decimal args/Hook calls passively observed, no block command or mutation replaced. Shared host isolates StringName/logging; relic Flash, CombatHistory.BlockGained log recording and energy-icon pool/prefix isolated. Native LocManager/SmartFormat/merchant binding execute. No Godot renderer, full game combat lifecycle, save/clone suite, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native Anchor block checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
