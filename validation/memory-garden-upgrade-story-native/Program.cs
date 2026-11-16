using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
if(args.Length!=6)throw new ArgumentException("candidate, expected SHA, existing 31O fixture, native profile, report, temporary directory");
string candidate=Path.GetFullPath(args[0]),fixture=Path.GetFullPath(args[2]),profile=Path.GetFullPath(args[3]),report=Path.GetFullPath(args[4]);
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
if(Hash(candidate)!=args[1])throw new Exception("Candidate mismatch");
using(var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(profile,"profile-manifest.json"))))
    foreach(var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        if(Hash(Path.Combine(profile,file.GetProperty("file").GetString()!))!=file.GetProperty("copySha256").GetString())throw new Exception("Native profile mismatch");
Environment.SetEnvironmentVariable("HBR_NATIVE_RUNTIME_DIRECTORY",profile);
AssemblyLoadContext.Default.Resolving+=(context,name)=>
{
    if(name.Name=="HBRGOSakiMod")return context.LoadFromAssemblyPath(candidate);
    foreach(string directory in new[]{profile,Path.GetDirectoryName(candidate)!,"C:/Users/ROG/.nuget/packages/godotsharp/4.5.1/lib/net8.0","C:/Users/ROG/.nuget/packages/system.io.hashing/9.0.0/lib/net9.0"})
    {string path=Path.Combine(directory,name.Name+".dll");if(File.Exists(path))return context.LoadFromAssemblyPath(path);}
    return null;
};
var passed=new List<string>();string? error=null;
void Check(bool value,string name){if(!value)throw new Exception(name);passed.Add(name);}
object? Call(Type type,string name,object? target,params object?[] values)
{try{return type.GetMethod(name,F)!.Invoke(target,values);}catch(TargetInvocationException e)when(e.InnerException!=null){throw e.InnerException;}}
FieldInfo Field(Type type,string name)
{for(Type? cursor=type;cursor!=null;cursor=cursor.BaseType)if(cursor.GetField(name,F)is{}field)return field;throw new MissingFieldException(type.FullName,name);}
void Set(object owner,string field,object? value)=>Field(owner.GetType(),field).SetValue(owner,value);
object Blank(Type type){object value=RuntimeHelpers.GetUninitializedObject(type);GC.SuppressFinalize(value);return value;}
string temporary=Path.GetFullPath(args[5]);Directory.CreateDirectory(temporary);
try
{
    Assembly host=AssemblyLoadContext.Default.LoadFromAssemblyPath(fixture);Type effects=host.GetType("EffectChecks",true)!;
    await (Task)effects.GetMethod("ClassSetup",F)!.Invoke(null,null)!;
    Assembly mod=Assembly.Load("HBRGOSakiMod"),native=Assembly.Load("sts2");
    Type store=mod.GetType("HBRGOSakiMod.Core.Helpers.HbrMemoryGardenUpgradeStoryLocalStore",true)!;
    Type file=mod.GetType("HBRGOSakiMod.Core.Story.HbrMemoryGardenUpgradeStoryLocalFile",true)!;
    Type config=mod.GetType("HBRGOSakiMod.Core.Config.HBRGOSakiConfig",true)!,playback=mod.GetType("HBRGOSakiMod.Core.Config.HbrMemoryGardenStoryPlayback",true)!;
    var setting=config.GetProperty("MemoryGardenUpgradeStoryPlayback",F)!;
    Check(setting.GetValue(null)!.ToString()=="All","actual persisted config property defaults to All");
    Check(Enum.GetNames(playback).SequenceEqual(new[]{"All","UnseenOnly","Off"}),"actual candidate exposes requested enum options");
    Check(setting.MetadataToken>config.GetProperty("ThirtyGProposalPresentation")!.MetadataToken&&setting.MetadataToken<config.GetProperty("HbrNumerics")!.MetadataToken,"new setting is adjacent to 31A/30G presentation controls");
    Check(setting.GetCustomAttributes().Any(a=>a.GetType().Name=="ConfigHoverTipAttribute")&&!setting.GetCustomAttributes().Any(a=>a.GetType().Name is "ConfigHideInUI" or "ConfigHideInUIAttribute"),"new enum setting remains visible with native BaseLib hover metadata");
    FieldInfo fileField=store.GetField("_file",F)!;Check(fileField.GetValue(null)==null,"local facade starts without accessing the real Godot user path");
    setting.SetValue(null,Enum.Parse(playback,"Off"));
    Check(!(bool)Call(store,"MarkStarted",null,"run-a","31o","ruka",1)!&&fileField.GetValue(null)==null,"Off rejects playback claim before creating any local/user-data file");
    string path=Path.Combine(temporary,"receipts.json");object injected=Activator.CreateInstance(file,F,null,new object?[]{path,(Action<string>)(_=>{})},null)!;fileField.SetValue(null,injected);
    setting.SetValue(null,Enum.Parse(playback,"All"));Check((bool)Call(store,"MarkStarted",null,"run-a","31O","Ruka",1)!,"opening setting later permits the previously unplayed chapter");
    Check((bool)Call(store,"HasStarted",null,"run-a","31o","ruka",1)!&&!(bool)Call(store,"MarkStarted",null,"run-a","31o","ruka",1)!,"actual facade honors durable per-run duplicate suppression");
    Check(!(bool)Call(store,"HasSeen",null,"31o","ruka",1)!,"actual facade does not infer seen from a started receipt");
    setting.SetValue(null,Enum.Parse(playback,"Off"));Check((bool)Call(store,"MarkSeen",null,"31o","ruka",1)!&& (bool)Call(store,"HasSeen",null,"31o","ruka",1)!,"a chapter already completed may be recorded even if setting is now Off");
    object afterLoad=Activator.CreateInstance(file,F,null,new object?[]{path,(Action<string>)(_=>{})},null)!;fileField.SetValue(null,afterLoad);
    setting.SetValue(null,Enum.Parse(playback,"UnseenOnly"));Check((bool)Call(store,"HasSeen",null,"31o","ruka",1)!&&(bool)Call(store,"HasStarted",null,"run-a","31o","ruka",1)!,"fresh file instance retains both seen and started while UnseenOnly is selected");

    object player=effects.GetProperty("Player",F)!.GetValue(null)!,run=effects.GetProperty("RunState",F)!.GetValue(null)!;
    Type managerType=native.GetType("MegaCrit.Sts2.Core.Runs.RunManager",true)!;object manager=managerType.GetProperty("Instance",F)!.GetValue(null)!;
    FieldInfo stateField=Field(managerType,"<State>k__BackingField"),timeField=Field(managerType,"_startTime");object? oldState=stateField.GetValue(manager);object oldTime=timeField.GetValue(manager)!;
    bool TryKey(object owner,out string key){object?[] parameters={owner,null};bool ok=(bool)store.GetMethod("TryGetRunKey",F)!.Invoke(null,parameters)!;key=(string)parameters[1]!;return ok;}
    try
    {
        stateField.SetValue(manager,run);timeField.SetValue(manager,1700000000L);Set(player,"<NetId>k__BackingField",123UL);Set(run,"<AscensionLevel>k__BackingField",7);
        Check(ReferenceEquals(managerType.GetMethod("DebugOnlyGetState",F)!.Invoke(manager,null),run),"fixture current-run field is observed through the real public native DebugOnlyGetState contract");
        object rng=run.GetType().GetProperty("Rng")!.GetValue(run)!;string seed=(string)rng.GetType().GetProperty("StringSeed")!.GetValue(rng)!;
        object niche=rng.GetType().GetProperty("Niche")!.GetValue(rng)!;
        object Counter()=>niche.GetType().GetProperty("Counter",F)?.GetValue(niche)??Field(niche.GetType(),"_counter").GetValue(niche)!;
        object counter=Counter();
        Check(TryKey(player,out string original)&&original==$"v1:1700000000:{seed.Length}:{seed}:7:123","actual RunKey uses native saved start time, original string seed, ascension and holder ID");
        Check(TryKey(player,out string repeated)&&repeated==original&&Equals(counter,Counter()),"repeated key reads are stable and consume no native RNG");
        object loadedRun=Blank(run.GetType());Set(loadedRun,"<Rng>k__BackingField",Activator.CreateInstance(rng.GetType(),seed));Set(loadedRun,"<AscensionLevel>k__BackingField",7);
        object loadedPlayer=Blank(player.GetType());Set(loadedPlayer,"_runState",loadedRun);Set(loadedPlayer,"<NetId>k__BackingField",123UL);stateField.SetValue(manager,loadedRun);
        Check(TryKey(loadedPlayer,out string loaded)&&loaded==original,"fresh run/player objects restored from identical native save identity produce the same SL key");
        Set(loadedPlayer,"<NetId>k__BackingField",124UL);Check(TryKey(loadedPlayer,out string peer)&&peer!=original,"teammate identity has an independent local run partition");Set(loadedPlayer,"<NetId>k__BackingField",123UL);
        timeField.SetValue(manager,1700000001L);Check(TryKey(loadedPlayer,out string newRun)&&newRun!=original,"a new native run start creates an independent partition");
        timeField.SetValue(manager,0L);Check(!TryKey(loadedPlayer,out string invalid)&&invalid=="","missing native saved start time returns no key instead of fabricating an ID");
        timeField.SetValue(manager,1700000000L);stateField.SetValue(manager,run);Check(!TryKey(loadedPlayer,out invalid)&&invalid=="","mismatched RunManager state never borrows another run's identity");
        stateField.SetValue(manager,loadedRun);Check(TryKey(loadedPlayer,out string recovered)&&recovered==original,"an earlier failed lookup does not poison later valid run identity");
    }
    finally{stateField.SetValue(manager,oldState);timeField.SetValue(manager,oldTime);setting.SetValue(null,Enum.Parse(playback,"All"));}
    Check(File.Exists(path)&&Path.GetFullPath(path).StartsWith(temporary,StringComparison.OrdinalIgnoreCase),"all facade writes remain inside the injected temporary test path");
}
catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
Directory.CreateDirectory(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok=error==null,checks=passed.Count,passed,error,modSha256=Hash(candidate),nativeSha256=Hash(Path.Combine(profile,"sts2.dll")),temporary,boundary="Actual candidate config/facade and native RunManager start-time field/Rng.StringSeed metadata. File backend injected with a temporary path; run reload represented by fresh native objects with saved identity fields, not a full scene load. No real user data, Godot or network writes."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+passed.Count+" native local-story identity checks");if(error!=null)Environment.ExitCode=1;
