using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using Overclock=HBRGOSakiMod.Core.Models.Relics.Overclock;
using Environment=System.Environment;

static class OverchargeChecks {public static void Run(string path)=>OverclockHoverChecks.Run(path);}
static class OverclockHoverChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(Overclock).Assembly,Native=typeof(ModelDb).Assembly;
    static readonly Harmony H=new("hbr.overclock.canonical-hover.checks");
    static readonly List<string> Passed=[];
    static readonly List<object> Texts=[];
    static string Root{get{for(DirectoryInfo? d=new(AppContext.BaseDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"HBRGOSakiMod.csproj")))return d.FullName;throw new DirectoryNotFoundException();}}
    static object Blank(Type t){var v=RuntimeHelpers.GetUninitializedObject(t);GC.SuppressFinalize(v);return v;}
    static T Blank<T>()=>(T)Blank(typeof(T));
    static FieldInfo Field(Type t,string n){for(Type? x=t;x!=null;x=x.BaseType)if(x.GetField(n,F|BindingFlags.DeclaredOnly)is{} f)return f;throw new MissingFieldException(t.FullName,n);}
    static void Set(object o,string n,object? v)=>Field(o.GetType(),n).SetValue(o,v);
    static void Check(bool v,string s){if(!v)throw new Exception(s);Passed.Add(s);}
    static bool Skip()=>false;
    static bool NameCtor(StringName __instance){GC.SuppressFinalize(__instance);return false;}
    static bool PrefixColor(ref string __result){__result="colorless";return false;}
    static bool PathLeaf(ref string __result){__result="res://images/packed/energy_icons/energy_colorless.png";return false;}
    static bool IconLeaf(ref Texture2D? __result){__result=null;return false;}
    static void Patch(MethodBase target,string name)=>H.Patch(target,prefix:new HarmonyMethod(typeof(OverclockHoverChecks).GetMethod(name,F)!));
    static object? Invoke(MethodInfo m,object? o,params object?[] args){try{return m.Invoke(o,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static IDisposable Compact()=>(IDisposable)Invoke(Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrCompactText",true)!.GetMethod("Enter",F)!,null)!;
    static void Install(bool negative)
    {
        TestMode.IsOn=true;Patch(typeof(StringName).GetConstructor([typeof(string)])!,nameof(NameCtor));
        Type logger=Native.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(var m in logger.GetMethods(F).Where(m=>m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))Patch(m,nameof(Skip));
        Type energy=Native.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        Patch(energy.GetMethod("GetPrefix",F)!,nameof(PrefixColor));Patch(energy.GetMethod("GetPath",F,null,[typeof(string)],null)!,nameof(PathLeaf));
        Patch(typeof(RelicModel).GetProperty("Icon",F)!.GetMethod!,nameof(IconLeaf));
        foreach(string n in new[]{"HbrCompactTextFormatPatch","HbrCompactRelicDescriptionPatch"})Check(H.CreateClassProcessor(Mod.GetType("HBRGOSakiMod.Core.Patches."+n,true)!).Patch().Count>0,"installs actual "+n);
        Type? fix=Mod.GetType("HBRGOSakiMod.Core.Patches.OverclockDescriptionVariablesPatch");
        if(!negative)Check(fix!=null&&H.CreateClassProcessor(fix).Patch().Count==2,"installs actual OD DynamicDescription and EventDescription variable hooks");
        else Check(fix==null,"old candidate has no dedicated OD description variable patch");
        foreach(Type t in new[]{typeof(Overclock),typeof(OverclockUpgradeHbrgoSaki),typeof(Anchor)}){ModelDb.Inject(t);Set(ModelDb.GetById<AbstractModel>(ModelDb.GetId(t)),"<Id>k__BackingField",ModelDb.GetId(t));}
        var manager=Blank<LocManager>();Set(manager,"<Language>k__BackingField","zhs");Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        string runtime="D:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";AssemblyLoadContext.Default.Resolving+=(ctx,n)=>{string p=Path.Combine(runtime,n.Name+".dll");return File.Exists(p)?ctx.LoadFromAssemblyPath(p):null;};
        var tables=new Dictionary<string,LocTable>();foreach(string table in new[]{"relics","cards","card_keywords","static_hover_tips"}){var text=new Dictionary<string,string>();string p="D:/stsmod/sts/localization/zhs/"+table+".json";if(File.Exists(p))foreach(var kv in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(p))!)text[kv.Key]=kv.Value;p=Path.Combine(Root,"HBRGOSakiMod/localization/zhs",table+".json");if(File.Exists(p))foreach(var kv in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(p))!)text[kv.Key]=kv.Value;tables[table]=new LocTable(table,text,null!);}Set(manager,"_tables",tables);Invoke(typeof(LocManager).GetMethod("LoadLocFormatters",F)!,manager);
    }
    static (Player,Overclock) Owned(RunState run,int spent)
    {
        var player=Blank<Player>();Set(player,"_runState",run);var relic=(Overclock)ModelDb.Relic<Overclock>().MutableClone();Set(relic,"_owner",player);Set(relic,"<FloorUsageKey>k__BackingField","0:act");Set(relic,"_floorLevelsSpent",spent);Set(player,"_relics",new List<RelicModel>{relic});return(player,relic);
    }
    static string Format(LocString text,int expected,string label,bool compact=true)
    {
        Check(text.Variables.TryGetValue("hbrRemaining",out object? value)&&Convert.ToInt32(value)==expected,label+" registers actual remaining="+expected);
        string formatted=text.GetFormattedText();Texts.Add(new{label,expected,formatted});Check(!formatted.Contains("{hbrRemaining}")&&!formatted.Contains("ERROR",StringComparison.OrdinalIgnoreCase),label+" real native SmartFormat has no missing variable/error");if(compact)Check(formatted.Contains("[blue]"+expected+"[/blue]"),label+" compact hover renders remaining allowance");return formatted;
    }
    static string Extra(OverclockUpgradeHbrgoSaki card)
    {
        IEnumerable tips=(IEnumerable)typeof(OverclockUpgradeHbrgoSaki).GetProperty("ExtraHoverTips",F)!.GetValue(card)!;
        return ((HoverTip)tips.Cast<object>().Single()).Description;
    }
    static void Positive()
    {
        var canonical=ModelDb.Relic<Overclock>();bool protectedOwner=false;try{_ = canonical.Owner;}catch(Exception e)when(e.GetType().Name=="CanonicalModelException"){protectedOwner=true;}Check(protectedOwner,"canonical relic.Owner still throws native assertion");
        string full=Format(canonical.DynamicDescription,12,"canonical full",false);Format(canonical.DynamicEventDescription,12,"canonical event",false);
        using(Compact()){Format(canonical.DynamicDescription,12,"canonical compact");Format(canonical.DynamicEventDescription,12,"canonical compact event");}
        var run=Blank<RunState>();Set(run,"_currentActIndex",0);var (owner,relic)=Owned(run,3);var (peer,other)=Owned(run,8);Set(run,"_players",new List<Player>{owner,peer});
        Check(relic.RemainingFloorLevels==4,"existing OD policy reads maximum party spending8rather than local3");
        using(Compact()){Format(relic.DynamicDescription,4,"owned compact with both duplicate-key postfixes");Format(relic.DynamicEventDescription,4,"owned compact event");}
        Check(Format(relic.DynamicDescription,4,"owned full",false)==full,"full OD description text remains unchanged");
        Set(other,"_floorLevelsSpent",10);using(Compact()){Format(relic.DynamicDescription,2,"live party allowance after another spend");}
        var card=(OverclockUpgradeHbrgoSaki)ModelDb.Card<OverclockUpgradeHbrgoSaki>().MutableClone();Set(card,"_owner",owner);using(Compact()){string text=Extra(card);Texts.Add(new{label="owned card ExtraHoverTips",text});Check(text.Contains("[blue]2[/blue]")&&!text.Contains("hbrRemaining"),"actual card ExtraHoverTips selects its owner's live party OD relic");}
        using(Compact()){Check(Extra(ModelDb.Card<OverclockUpgradeHbrgoSaki>()).Contains("[blue]12[/blue]"),"actual canonical card ExtraHoverTips safely formats generic12");var unowned=(OverclockUpgradeHbrgoSaki)ModelDb.Card<OverclockUpgradeHbrgoSaki>().MutableClone();Check(Extra(unowned).Contains("[blue]12[/blue]"),"mutable ownerless card safely falls back to generic relic");}
        var ownerless=(Overclock)ModelDb.Relic<Overclock>().MutableClone();using(Compact()){Format(ownerless.DynamicDescription,12,"ownerless mutable relic");}
        Set(run,"_currentActIndex",1);using(Compact()){Format(relic.DynamicDescription,12,"next act uses real reset policy");}
        Check(!ModelDb.Relic<Anchor>().DynamicDescription.Variables.ContainsKey("hbrRemaining"),"unrelated vanilla relic gets no OD-specific variable");
    }
    static void Negative()
    {
        var canonical=ModelDb.Relic<Overclock>();using(Compact())
        {
            Check(!canonical.DynamicDescription.Variables.ContainsKey("hbrRemaining"),"old canonical dynamic description genuinely lacks hbrRemaining");
            string? text=null;Exception? failure=null;try{text=Extra(ModelDb.Card<OverclockUpgradeHbrgoSaki>());}catch(Exception e){failure=e;}
            Texts.Add(new{label="old actual card ExtraHoverTips compact formatting",text,failure=failure?.ToString()});
            Check(failure!=null||text==null||!text.Contains("[blue]12[/blue]"),"old actual ExtraHoverTips cannot produce a resolved remaining allowance");
        }
    }
    public static void Run(string path)
    {
        bool ok=false;string?error=null;bool negative=Environment.GetEnvironmentVariable("HBR_EXPECT_OD_HOVER_BUG")=="1";
        try{Install(negative);if(negative)Negative();else Positive();ok=true;}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
        File.WriteAllText(path,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,texts=Texts,expectedNegativeControl=negative,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),sourceNativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real Relic.DynamicDescription/EventDescription and card ExtraHoverTips, actual compact raw-text patch, duplicate native LocString.Add behavior, real native SmartFormat and party OD allowance policy. Godot StringName/icon resource loading, logger and energy image-path/color terminal leaves isolated; native canonical assertions remain. No engine/render/save/package."},new JsonSerializerOptions{WriteIndented=true}));if(!ok)throw new Exception("Inspect "+path);Console.WriteLine("PASS "+Passed.Count+" OD hover checks");
    }
}
