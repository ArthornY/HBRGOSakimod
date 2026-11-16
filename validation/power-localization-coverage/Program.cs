using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using HBRGOSakiMod.Core.Models.Powers;
using Environment=System.Environment;

static class PowerLocalizationCoverage
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(DiamondDpCheckPower).Assembly;
    static readonly List<object> Ids=[],Rows=[],Failures=[],Skipped=[];
    static int Checks;
    static string Repository="";
    static string Language="";
    static Dictionary<string,string> PowerTable=[];
    static readonly Dictionary<string,string> SourceHashes=[];
    static bool Skip()=>false;
    static bool StringNameLeaf(StringName __instance){GC.SuppressFinalize(__instance);return false;}
    static bool EnergyLeaf(ref string __result){__result="energy";return false;}
    static FieldInfo Field(Type type,string name)
    {
        for(Type? t=type;t!=null;t=t.BaseType)if(t.GetField(name,F) is {} f)return f;
        throw new MissingFieldException(type.FullName,name);
    }
    static void Set(object owner,string name,object? value)=>Field(owner.GetType(),name).SetValue(owner,value);
    static void Assert(bool condition,string message){Checks++;if(!condition)throw new Exception(message);}
    static void Setup()
    {
        var h=new Harmony("hbr.power-localization.coverage");
        // This is deliberately the first ModelDb operation. IDs are then
        // assigned by the real AbstractModel constructor, never rewritten.
        var prefix=Assembly.Load("BaseLib").GetType("BaseLib.Patches.Content.PrefixIdPatch",true)!;
        Assert(h.CreateClassProcessor(prefix).Patch().Count==1,"Real BaseLib PrefixIdPatch installs before any model lookup");
        var getter=typeof(ModelDb).GetMethod("GetEntry",F)!;
        Assert(Harmony.GetPatchInfo(getter)!.Postfixes.Any(p=>p.PatchMethod.DeclaringType==prefix),"Actual native GetEntry has the production prefix-ID postfix");
        h.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(PowerLocalizationCoverage).GetMethod(nameof(StringNameLeaf),F)!));
        Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(var method in logger.GetMethods(F).Where(m=>m.Name is "GetIsRunningFromGodotEditor" or "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose"))h.Patch(method,prefix:new HarmonyMethod(typeof(PowerLocalizationCoverage).GetMethod(nameof(Skip),F)!));
        Type energy=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Helpers.EnergyIconHelper",true)!;
        foreach(var method in energy.GetMethods(F).Where(m=>m.Name is "GetPrefix" or "GetPath" && m.ReturnType==typeof(string)))h.Patch(method,prefix:new HarmonyMethod(typeof(PowerLocalizationCoverage).GetMethod(nameof(EnergyLeaf),F)!));
        TestMode.IsOn=true;
        Type dumb=Assembly.Load("BaseLib").GetType("BaseLib.Patches.Localization.PowerModelLocPatch",true)!;
        Assert(h.CreateClassProcessor(dumb).Patch().Count>0,"Real BaseLib custom power dynamic-variable bridge is installed");
    }
    static void LoadTables(string root,JsonElement plan,bool overlay)
    {
        var manager=(LocManager)RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
        Set(manager,"<Language>k__BackingField",Language);
        Set(manager,"<CultureInfo>k__BackingField",CultureInfo.InvariantCulture);
        Set(manager,"<StringComparer>k__BackingField",StringComparer.Ordinal);
        typeof(LocManager).GetField("<Instance>k__BackingField",F)!.SetValue(null,manager);
        var tables=new Dictionary<string,LocTable>();
        foreach(string table in new[]{"powers","cards","relics","monsters","card_keywords","static_hover_tips","gameplay_ui","characters"})
        {
            var values=new Dictionary<string,string>();
            foreach(string path in new[]{Path.Combine("D:/stsmod/sts/localization",Language,table+".json"),Path.Combine(root,Language,table+".json")})
                if(File.Exists(path))
                {
                    byte[] bytes=File.ReadAllBytes(path);
                    SourceHashes[Path.GetFullPath(path)]=Convert.ToHexString(SHA256.HashData(bytes));
                    foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(bytes)!)values[pair.Key]=pair.Value;
                }
            if(table=="powers")
            {
                PowerTable=values;
                if(overlay)foreach(var alias in plan.GetProperty("aliases").EnumerateArray().Where(a=>a.GetProperty("lang").GetString()==Language&&a.GetProperty("table").GetString()=="powers"))
                {
                    string bare=alias.GetProperty("baseKey").GetString()!,key=alias.GetProperty("aliasKey").GetString()!,value=alias.GetProperty("value").GetString()!;
                    Assert(values.TryGetValue(bare,out string? actual)&&actual==value,"Overlay reuses exact authored data: "+bare);
                    if(values.TryGetValue(key,out actual))Assert(actual==value,"Existing alias is never overwritten with different content: "+key);
                    values[key]=value;
                }
            }
            tables[table]=new LocTable(table,values,null!);
        }
        Set(manager,"_tables",tables);
        typeof(LocManager).GetMethod("LoadLocFormatters",F)!.Invoke(manager,null);
    }
    static Creature Owner()
    {
        var owner=(Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));GC.SuppressFinalize(owner);
        Set(owner,"_powers",new List<PowerModel>());Set(owner,"_maxHp",5000);Set(owner,"_currentHp",5000);
        Set(owner,"<Side>k__BackingField",CombatSide.Enemy);
        return owner;
    }
    static void Text(Type type,PowerModel power,string context,string property,string expectedEntry)
    {
        try
        {
            LocString text=(LocString)type.GetProperty(property,F)!.GetValue(power)!;
            string key=text.LocEntryKey;
            string bare=key.StartsWith("HBRGOSAKIMOD-",StringComparison.Ordinal)?key["HBRGOSAKIMOD-".Length..]:key;
            if(Language=="eng"&&!text.Exists()&&!PowerTable.ContainsKey(bare))
            {Skipped.Add(new{type=type.Name,context,property,key,reason="No existing authored English entry; sparse legacy English coverage preserved"});return;}
            Checks++;
            if(!text.Exists())
            {
                Failures.Add(new{type=type.Name,id=power.Id.Entry,context,property,key,kind="missing-localization-key",error=$"{type.Name}/{context}/{property} does not resolve actual key {key}"});
                return;
            }
            if(property!="Title")
            {
                var args=typeof(PowerModel).GetMethod("AddDumbVariablesToDescription",F)!;
                args.Invoke(power,args.GetParameters().Length==2?[text,null]:[text]);
            }
            string formatted=text.GetFormattedText();
            Assert(!string.IsNullOrWhiteSpace(formatted)&&formatted!=key&&!formatted.Contains("{"),$"{type.Name}/{context}/{property} formats actual content: {key}");
            Rows.Add(new{type=type.Name,id=power.Id.Entry,context,property,key,formatted});
        }
        catch(Exception e){Failures.Add(new{type=type.Name,id=power.Id.Entry,context,property,kind="context-error",error=(e is TargetInvocationException&&e.InnerException!=null?e.InnerException:e).ToString()});}
    }
    public static int Run(string[] args)
    {
        string candidate=Path.GetFullPath(args[0]),planPath=Path.GetFullPath(args[4]),report=Path.GetFullPath(args[5]);
        Repository=Path.GetFullPath(args[3]);Language=args[6];
        bool overlay=args.Contains("--overlay"),expectMissing=args.Contains("--expect-missing"),minimal=args.Contains("--minimal");
        string? fatal=null;int powerCount=0,namespaced=0;
        try
        {
            using var planDoc=JsonDocument.Parse(File.ReadAllText(planPath));var plan=planDoc.RootElement;
            Setup();LoadTables(Path.Combine(Repository,"HBRGOSakiMod/localization"),plan,overlay);
            if(expectMissing)
            {
                Assert(!overlay,"Negative control never overlays replacement data");
                string loadedPowerTable=Path.GetFullPath(Path.Combine(Repository,"HBRGOSakiMod/localization",Language,"powers.json"));
                Assert(SourceHashes[loadedPowerTable]==plan.GetProperty("tableInputs").GetProperty(Language).GetProperty("sha256").GetString(),"Negative control reads the original audited power table bytes");
            }
            ModelDb.Inject(typeof(HBRGOSakiMod.Core.Models.CardPools.HBRGOSaki31OCardPool));
            var all=Mod.GetTypes().Where(t=>!t.IsAbstract&&!t.ContainsGenericParameters&&typeof(PowerModel).IsAssignableFrom(t)).OrderBy(t=>t.FullName).ToArray();
            powerCount=all.Length;
            foreach(var type in all)
            {string id=ModelDb.GetId(type).Entry;Ids.Add(new{type=type.FullName,id});if(id.StartsWith("HBRGOSAKIMOD-",StringComparison.Ordinal))namespaced++;}
            Assert(powerCount==plan.GetProperty("concretePowerCount").GetInt32(),"Actual reflection covers every concrete power, including subclasses of native Strength and Dexterity");
            Assert(namespaced==52,"Actual BaseLib IDs include exactly 52 namespaced powers");
            foreach(var row in plan.GetProperty("namespacedPowers").EnumerateArray())
            {
                Type type=Mod.GetType(row.GetProperty("fullType").GetString()!,true)!;
                string expected=row.GetProperty("modelId").GetString()!;
                Assert(ModelDb.GetId(type).Entry==expected,"Actual native ID equals metadata audit: "+type.Name);
                if(minimal&&type!=typeof(DiamondDpCheckPower)&&type!=typeof(FaintnessStormCountdownPower))continue;
                try
                {
                    ModelDb.Inject(type);
                    PowerModel canonical=ModelDb.GetById<PowerModel>(ModelDb.GetId(type));
                    Assert(canonical.Id.Entry==expected&&canonical.GetType()==type,"Native constructor assigned canonical ID without SetId: "+type.Name);
                    Text(type,canonical,"canonical","Title",expected);
                    foreach(string getter in new[]{"Description","SmartDescription","RemoteDescription"})Text(type,canonical,"canonical",getter,expected);
                    PowerModel owned=canonical.ToMutable();
                    Set(owned,"_owner",Owner());
                    owned.SetAmount(3,silent:true);
                    Assert(owned.Id==canonical.Id,"Native mutable clone retains canonical ID: "+type.Name);
                    int amount=owned.Amount;
                    foreach(string getter in new[]{"Description","SmartDescription","RemoteDescription"})Text(type,owned,"owned",getter,expected);
                    Assert(owned.Amount==amount,"Formatting does not change power amount: "+type.Name);
                }
                catch(Exception e){Failures.Add(new{type=type.Name,context="construction",kind="construction-error",error=e.ToString()});}
            }
        }
        catch(Exception e){fatal=e.ToString();}
        bool ok=fatal==null&&Failures.Count==0;
        var failureElements=Failures.Select(row=>JsonSerializer.SerializeToElement(row)).ToArray();
        var requiredMissing=new[]{("DiamondDpCheckPower","HBRGOSAKIMOD-DIAMOND_DP_CHECK_POWER"),("FaintnessStormCountdownPower","HBRGOSAKIMOD-FAINTNESS_STORM_COUNTDOWN_POWER")}
            .SelectMany(target=>new[]{("canonical","Title",".title"),("canonical","Description",".referenceDescription"),("canonical","SmartDescription",".referenceDescription"),("canonical","RemoteDescription",".referenceDescription"),("owned","Description",".description"),("owned","SmartDescription",".description"),("owned","RemoteDescription",".description")}
                .Select(getter=>new{type=target.Item1,context=getter.Item1,property=getter.Item2,key=target.Item2+getter.Item3})).ToArray();
        bool expectedFailure=fatal==null&&failureElements.Length==requiredMissing.Length&&failureElements.All(f=>f.GetProperty("kind").GetString()=="missing-localization-key")&&
            requiredMissing.All(required=>failureElements.Count(f=>f.GetProperty("type").GetString()==required.type&&f.GetProperty("context").GetString()==required.context&&f.GetProperty("property").GetString()==required.property&&f.GetProperty("key").GetString()==required.key)==1);
        var result=new{ok,expectedFailureObserved=expectMissing&&expectedFailure,checks=Checks,powerCount,namespaced,language=Language,overlay,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(candidate))),nativeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),
            nativeSourceSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),ids=Ids,rows=Rows,failures=Failures,skipped=Skipped,fatal,
            aliasPlanSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(planPath))),localizationSources=SourceHashes,
            boundary="Real BaseLib PrefixIdPatch before lookup; native constructors and mutable clone IDs; actual LocString/SmartFormat. Only native StringName/logging/energy paths and owner scene data controlled. No SetId, Godot, installed config/save edits or fabricated translations."};
        File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{ok,checks=Checks,powerCount,namespaced,failures=Failures.Count,skipped=Skipped.Count,fatal,report}));
        return expectMissing?(expectedFailure?0:1):(ok?0:1);
    }
}
