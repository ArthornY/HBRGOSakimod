using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

static class OverchargeChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    sealed record BlockCall(Creature Target,decimal Amount,ValueProp Props,CardPlay? Play,bool Fast);
    static readonly List<BlockCall> Calls=[];
    static int HookCalls,MutationCalls;
    static Player Player=>State<Player>("Player");
    static Creature Hero=>State<Creature>("Hero");
    static Creature Enemy=>State<Creature>("Enemy");
    static BlockPotion Potion=null!;
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
    {Type type=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool",true)!;__result=(IPoolModel)RuntimeHelpers.GetUninitializedObject(type);Set(__result,"<Id>k__BackingField",ModelDb.GetId(type));return false;}
    static void ObserveDecimal(object[] __args)=>Calls.Add(new((Creature)__args[0],(decimal)__args[1],(ValueProp)__args[2],(CardPlay?)__args[3],(bool)__args[4]));
    static void ObserveHook()=>HookCalls++;
    static void ObserveMutation()=>MutationCalls++;
    static decimal Block(Creature owner)=>Convert.ToDecimal(owner.Block,CultureInfo.InvariantCulture);
    static void ClearBlock(Creature owner)
    {FieldInfo field=typeof(Creature).GetFields(F).Single(field=>field.Name is "_block" or "<Block>k__BackingField");field.SetValue(owner,Convert.ChangeType(0,field.FieldType,CultureInfo.InvariantCulture));}
    static T Owned<T>()where T:PotionModel,new()
    {var potion=new T();Set(potion,"<IsMutable>k__BackingField",true);Set(potion,"_owner",Player);Set(potion,"<Id>k__BackingField",ModelDb.GetId(typeof(T)));return potion;}
    static void Reset(bool hbr=true,int talent=0)
    {H("Reset");H("Mode",hbr,1);H("Sense","Talent",talent);ClearBlock(Hero);ClearBlock(Enemy);Potion=Owned<BlockPotion>();Calls.Clear();HookCalls=0;MutationCalls=0;}
    static void Install()
    {
        H("Setup");var harmony=new Harmony("hbr.block-potion-scale.checks");
        foreach(string name in new[]{"HbrNativeAnchorBlockSkillPatch","HbrNativeEffectSkillDescriptionPatch","HbrNativeEffectSkillTextPatch"})
        {Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat."+name,true)!;Check(harmony.CreateClassProcessor(patch).Patch().Count>0,"installs actual potion block/description boundary "+name);}
        Typed=typeof(CreatureCmd).GetMethods(F).Single(method=>method.Name=="GainBlock"&&method.GetParameters() is var p&&p.Length==4&&p[1].ParameterType==typeof(BlockVar));
        Decimal=typeof(CreatureCmd).GetMethods(F).Single(method=>method.Name=="GainBlock"&&method.GetParameters() is var p&&p.Length==5&&p[1].ParameterType==typeof(decimal));
        Type history=typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Combat.History.CombatHistory",true)!;
        harmony.Patch(history.GetMethod("BlockGained",F)!,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(Skip),F)!));
        harmony.Patch(Decimal,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(ObserveDecimal),F)!));
        harmony.Patch(typeof(Hook).GetMethod("ModifyBlock",F)!,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(ObserveHook),F)!));
        harmony.Patch(AccessTools.Method(typeof(Creature),"GainBlockInternal"),prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(ObserveMutation),F)!));
        Type energy=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(MethodInfo method in energy.GetMethods(F).Where(method=>method.Name=="GetPrefix"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(Energy),F)!));
        foreach(MethodInfo method in energy.GetMethods(F).Where(method=>method.Name=="GetPool"))harmony.Patch(method,prefix:new HarmonyMethod(typeof(OverchargeChecks).GetMethod(nameof(Pool),F)!));
    }
    static void Use()=>((Task)Invoke(typeof(BlockPotion).GetMethod("OnUse",F)!,Potion,null,Hero)!).GetAwaiter().GetResult();
    static void Gain(Creature target,BlockVar variable,CardPlay? play=null,bool fast=false)=>((Task)Invoke(Typed,null,target,variable,play,fast)!).GetAwaiter().GetResult();
    static void Verify(string label,decimal expected,Creature target,CardPlay? play=null,bool fast=false)
    {
        Cases.Add(new{label,actualBlock=Block(target),raw=Potion.DynamicVars.Block.BaseValue,calls=Calls.Select(call=>new{call.Amount,props=call.Props.ToString(),targetOwner=ReferenceEquals(call.Target,Hero),samePlay=ReferenceEquals(call.Play,play),call.Fast}).ToArray(),HookCalls,MutationCalls});
        Equal(Block(target),expected,label+" reaches actual native GainBlockInternal result");
        Check(Calls.Count==1&&ReferenceEquals(Calls[0].Target,target)&&Calls[0].Props==ValueProp.Unpowered&&ReferenceEquals(Calls[0].Play,play)&&Calls[0].Fast==fast,label+" preserves original decimal command target Unpowered CardPlay and fast parameters");
        Check(HookCalls==1&&MutationCalls==1,label+" executes native block modifier Hook and actual GainBlockInternal exactly once");
        Equal(Potion.DynamicVars.Block.BaseValue,12m,label+" leaves original potion BlockVar raw12 unchanged");
    }
    static void NativeCases()
    {
        Reset();Use();Verify("real native BlockPotion.OnUse INT100",1200m,Hero);
        Equal(Calls.Single().Amount,1200m,"actual typed potion consumer resolves twelve units once before original Unpowered decimal command");
        Reset(talent:25);Use();Verify("real native BlockPotion.OnUse INT150",1800m,Hero);
        Reset(false);Use();Verify("legacy native BlockPotion.OnUse",12m,Hero);
        Reset();var source=new DefendIronclad();Set(source,"<IsMutable>k__BackingField",true);Set(source,"_owner",Player);var play=Blank<CardPlay>();Set(play,"<Card>k__BackingField",source);Gain(Hero,Potion.DynamicVars.Block,play,true);Verify("explicit original CardPlay and fast",1200m,Hero,play,true);
        Reset();Gain(Enemy,Potion.DynamicVars.Block);Verify("potion variable to different recipient stays native",12m,Enemy);
        Reset();var clone=(BlockVar)Potion.DynamicVars.Block.Clone();clone.SetOwner(Potion);Gain(Hero,clone);Verify("noncanonical cloned potion variable stays native",12m,Hero);
        Reset();var other=new HornCleat();Set(other,"<IsMutable>k__BackingField",true);Set(other,"_owner",Player);Gain(Hero,other.DynamicVars.Block);Verify("unlisted native relic BlockVar stays native",other.DynamicVars.Block.BaseValue,Hero);
        Reset();((Task)Invoke(Decimal,null,Hero,12m,ValueProp.Unpowered,null,false)!).GetAwaiter().GetResult();Verify("already-resolved decimal path has no blanket scaling",12m,Hero);
        foreach(RelicModel relic in new RelicModel[]{new Anchor(),new RippleBasin()})
        {
            Reset();Set(relic,"<IsMutable>k__BackingField",true);Set(relic,"_owner",Player);
            decimal raw=relic.DynamicVars.Block.BaseValue;Gain(Hero,relic.DynamicVars.Block);
            Verify("existing audited relic "+relic.GetType().Name,raw*100m,Hero);
            Equal(relic.DynamicVars.Block.BaseValue,raw,"existing relic raw block remains unchanged");
        }
        Reset(talent:25);Use();Use();Equal(Block(Hero),3600m,"repeated actual potion grants add1800 each without scaling previous block");
        Check(Calls.Count==2&&Calls.All(call=>call.Amount==1800m)&&HookCalls==2&&MutationCalls==2,"two native uses each execute one original decimal command, hook and mutation");
        Equal(Potion.DynamicVars.Block.BaseValue,12m,"repeated native uses keep original raw twelve units");
    }
    static string Root
    {get{for(DirectoryInfo? folder=new(AppContext.BaseDirectory);folder!=null;folder=folder.Parent)if(File.Exists(Path.Combine(folder.FullName,"HBRGOSakiMod.csproj")))return folder.FullName;throw new DirectoryNotFoundException();}}
    static void Localization()
    {
        string runtime="D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(context,name)=>{string path=Path.Combine(runtime,name.Name+".dll");return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>();foreach(string table in new[]{"potions","static_hover_tips","card_keywords"})
        {
            var text=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("D:/stsmod/sts/localization/zhs/"+table+".json"))!;
            string custom=Path.Combine(Root,"HBRGOSakiMod/localization/zhs/"+table+".json");
            if(File.Exists(custom))foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(custom))!)text[pair.Key]=pair.Value;
            tables[table]=new LocTable(table,text,null!);
        }
        Set(manager,"_tables",tables);typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
    }
    static void DescriptionCases()
    {
        Localization();
        foreach(var row in new[]{(hbr:true,talent:0,expected:1200m),(hbr:true,talent:25,expected:1800m),(hbr:false,talent:25,expected:12m)})
        {
            Reset(row.hbr,row.talent);string text=Potion.DynamicDescription.GetFormattedText();
            Check(text.Contains(row.expected.ToString("0",CultureInfo.InvariantCulture))&&!text.Contains('{'),$"real owned potion description displays actual {row.expected} at HBR{row.hbr} Talent{row.talent}");
            Use();Equal(Block(Hero),row.expected,"real description amount matches actual OnUse block");
            Equal(Potion.DynamicVars.Block.BaseValue,12m,"description formatting preserves raw potion twelve");
            Cases.Add(new{label="description",row.hbr,row.talent,text,actualBlock=Block(Hero),raw=Potion.DynamicVars.Block.BaseValue});
        }
        Reset();var canonical=new BlockPotion();string canonicalText=canonical.DynamicDescription.GetFormattedText();
        Check(canonicalText.Contains("12")&&!canonicalText.Contains("1200")&&!canonicalText.Contains('{'),"unowned canonical potion description remains native twelve");
    }
    public static void Run(string path)
    {
        bool ok=false;string? error=null;try{Install();NativeCases();DescriptionCases();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        MethodInfo use=typeof(BlockPotion).GetMethod("OnUse",F)!;MethodInfo body=use.GetCustomAttribute<AsyncStateMachineAttribute>() is {} state?AccessTools.Method(state.StateMachineType,"MoveNext"):use;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            nativeOnUseIl=PatchProcessor.GetOriginalInstructions(body).Select(i=>i.ToString()).ToArray(),modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Creature).Assembly.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),
            boundary="Real native BlockPotion.OnUse -> typed GainBlock -> production owner/identity/enabled resolver -> actual decimal GainBlock -> native Hook.ModifyBlock -> actual GainBlockInternal. Only passive prefix counters inspect command/hook/mutation; no calculation, command or mutation replacement. LocManager/SmartFormat and real PotionModel.DynamicDescription execute. Shared host isolates StringName/logging/listener enumeration; CombatHistory.BlockGained recording and energy-icon pool/prefix presentation are isolated. No Godot renderer, user-save loading, complete potion-consumption lifecycle, packaging or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native BlockPotion scale checks");if(!ok)throw new Exception("Inspect "+path);
    }
}
