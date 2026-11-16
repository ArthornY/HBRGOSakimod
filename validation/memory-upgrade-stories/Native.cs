using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.DevConsole;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Events;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Godot;
using Environment = System.Environment;

static class ReaderChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly Type P=Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrMemoryUpgradeStoryPresentation",true)!;
    static readonly Type C=Mod.GetType("HBRGOSakiMod.Core.Story.HbrMemoryUpgradeStoryCatalog",true)!;
    static readonly Type Result=Mod.GetType("HBRGOSakiMod.Core.Helpers.HbrMemoryUpgradeStoryPlaybackResult",true)!;
    static readonly List<string> Passed=[];
    static readonly Dictionary<string,string> Files=[];
    static Player Owner=default!;
    static bool Ready=true,Current=true,Visible=true,AnchorValid=true,ThrowDetach;
    static int Reads,Loads,Attaches,Detaches,FocusRestores,NetworkCalls,Availability;
    static Task<bool>? LoadGate;
    static object? Session;
    static NLibraryEvent? Reader;
    static readonly FakeScreen Underlay=new(),Covering=new();
    static IScreenContext NativeScreen=Underlay;
    sealed class FakeScreen:IScreenContext {public Control? DefaultFocusedControl=>null;}
    static object? Invoke(MethodInfo method,object? target,params object?[] args)
    {try{return method.Invoke(target,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
    static object? Call(Type type,string name,params object?[] args)=>Invoke(type.GetMethod(name,F)!,null,args);
    static void Set(object target,string field,object? value)=>typeof(Checks).GetMethod("Set",F)!.Invoke(null,new[]{target,field,value});
    static T Blank<T>(){T result=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));GC.SuppressFinalize(result!);return result;}
    static void Check(bool value,string message){if(!value)throw new Exception(message);Passed.Add(message);}
    static void Eq(int actual,int expected,string message)=>Check(actual==expected,message+": "+actual+"="+expected);
    static bool Text(string path,ref string? __result){Reads++;Files.TryGetValue(path,out __result);return false;}
    static bool Capture(Player owner,object?[] __args,ref bool __result)
    {
        __result=Ready&&ReferenceEquals(owner,Owner);__args[1]=null;
        if(__result){object anchor=Activator.CreateInstance(P.GetNestedType("Anchor",F)!,true)!;Set(anchor,"Underlay",Underlay);__args[1]=anchor;}return false;
    }
    static bool IsCurrent(object session,ref bool __result){__result=Current;return false;}
    static bool Anchor(object session,ref bool __result){__result=AnchorValid;return false;}
    static bool Load(HbrMemoryUpgradeStorySegment segment,ref Task<bool> __result){Loads++;__result=LoadGate??Task.FromResult(true);return false;}
    static bool Attach(object session)
    {
        Session=session;Reader=Blank<NLibraryEvent>();Visible=true;Attaches++;Set(session,"Reader",Reader);Set(session,"Attached",true);
        var callback=typeof(ReaderChecks).GetMethod(nameof(Completed),F)!.MakeGenericMethod(Result);
        var action=Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(Result),callback);
        Invoke(typeof(NLibraryEvent).GetMethod("InitializeUpgradeMemory",F)!,Reader,Owner,session.GetType().GetField("Segment",F)!.GetValue(session),action);
        return false;
    }
    static void Completed<T>(T result)=>Call(P,"Finish",Session,result);
    static bool Detach(object session){Detaches++;Visible=false;if(ThrowDetach)throw new InvalidOperationException("controlled visual cleanup failure");return false;}
    static bool Visibility(object session,ref bool __result){__result=Visible;return false;}
    static bool Coverage(object session,bool visible){Visible=visible;return false;}
    static bool Screen(ref IScreenContext __result){__result=NativeScreen;return false;}
    static bool Focus(){FocusRestores++;return false;}
    static void Available()=>Availability++;
    static bool Forbidden(MethodBase __originalMethod){NetworkCalls++;throw new Exception("Unexpected synchronized/UI transition: "+__originalMethod);}
    static string Payload(int level=1)=>JsonSerializer.Serialize(new
    {
        schemaVersion=1,characterId="ruka",
        presentation=new{background="res://images/fixture/background.png",dialogueStyle="original"},
        segments=new[]{new{level,perspectives=new[]{"31o"},lines=new[]{new{kind="narration",speaker="",text="Only a managed temporary fixture."}}}}
    });
    static void Reset()
    {
        Call(P,"CancelAll");Files.Clear();Reads=Loads=Attaches=Detaches=FocusRestores=NetworkCalls=Availability=0;
        Ready=Current=Visible=AnchorValid=true;ThrowDetach=false;LoadGate=null;Session=null;Reader=null;NativeScreen=Underlay;
        object entries=C.GetField("Entries",F)!.GetValue(null)!;entries.GetType().GetMethod("Clear")!.Invoke(entries,null);
        Files["res://story/memory_upgrades/ruka.json"]=Payload();
    }
    static void Install()
    {
        typeof(Checks).GetMethod("Setup",F)!.Invoke(null,null);Owner=(Player)typeof(Checks).GetField("Player",F)!.GetValue(null)!;
        var h=new Harmony("hbr.memory-upgrade-reader.native-checks");
        foreach(var row in new[]{("ReadText",nameof(Text))})h.Patch(C.GetMethod(row.Item1,F)!,prefix:new HarmonyMethod(typeof(ReaderChecks).GetMethod(row.Item2,F)!));
        foreach(var row in new[]{("TryCaptureUi",nameof(Capture)),("IsCurrent",nameof(IsCurrent)),("AnchorIsUsable",nameof(Anchor)),("LoadAssets",nameof(Load)),("AttachReader",nameof(Attach)),("DetachReader",nameof(Detach)),("ReaderIsVisible",nameof(Visibility)),("UpdateReaderCoverage",nameof(Coverage))})
            h.Patch(P.GetMethod(row.Item1,F)!,prefix:new HarmonyMethod(typeof(ReaderChecks).GetMethod(row.Item2,F)!));
        h.Patch(typeof(ActiveScreenContext).GetMethod("GetCurrentScreen")!,prefix:new HarmonyMethod(typeof(ReaderChecks).GetMethod(nameof(Screen),F)!));
        h.Patch(typeof(ActiveScreenContext).GetMethod("FocusOnDefaultControl")!,prefix:new HarmonyMethod(typeof(ReaderChecks).GetMethod(nameof(Focus),F)!));
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.HbrMemoryUpgradeStoryScreenContextPatch",true)!;
        Check(h.CreateClassProcessor(patch).Patch().Count==1,"actual native screen-context bridge installs exactly one target");
        P.GetEvent("AvailabilityChanged",F)!.GetAddMethod(true)!.Invoke(null,new object[]{(Action)Available});
        foreach(string prop in new[]{"EventSynchronizer","InputSynchronizer"})
        {
            Type type=typeof(RunManager).GetProperty(prop,F)!.PropertyType;
            foreach(var method in type.GetMethods(F).Where(m=>m.Name is "BeginEvent" or "ChooseLocalOption" or "SyncLocalScreen"))
                h.Patch(method,prefix:new HarmonyMethod(typeof(ReaderChecks).GetMethod(nameof(Forbidden),F)!));
        }
        Reset();
    }
    static bool Find(string actor,int level,HbrMemoryUpgradeStoryPerspective p=HbrMemoryUpgradeStoryPerspective.ThirtyOneO)
    {object?[] args={actor,p,level,null};return (bool)C.GetMethod("TryFind",F)!.Invoke(null,args)!;}
    static Task Play(Player? player=null)=> (Task)Call(P,"PlayLocalAsync",player??Owner,HbrMemoryUpgradeStoryPerspective.ThirtyOneO,"ruka",1)!;
    static string GetResult(Task task)=>task.GetType().GetProperty("Result")!.GetValue(task)!.ToString()!;
    static void Close(string result)=>Invoke(typeof(NLibraryEvent).GetMethod("CompleteUpgradeMemory",F)!,Reader,Enum.Parse(Result,result));
    static void CatalogCases()
    {
        Reset();Check(Find("ruka",1),"actual cached asset loader resolves authored content");Eq(Reads,1,"first actor read occurs once");Check(Find("ruka",1),"second lookup resolves same cached content");Eq(Reads,1,"cache hit performs no file access");
        Check(!Find("ruka",2)&&!Find("ruka",1,HbrMemoryUpgradeStoryPerspective.Hotaru),"missing level and viewpoint are no-op");Check(!Find("yuki",1)&&!Find("yuki",1),"missing actor file is silent no-op");Eq(Reads,2,"missing actor file is cached once");
        int count=Reads;Check(!Find("../ruka",1)&&!Find("unknown",1)&&!Find("ruka",0),"untrusted actor path and acquisition0 rejected before IO");Eq(Reads,count,"invalid key cannot reach file reader");
        Reset();Files["res://story/memory_upgrades/ruka.json"]="{invalid";Check(!Find("ruka",1)&&!Find("ruka",1),"invalid future authored data fails closed and is cached");Eq(Reads,1,"malformed data is not retried repeatedly");
        Reset();Check(!new HbrMemoryUpgradeStoryPreviewConsoleCmd().IsNetworked,"actual preview console command is strictly non-networked");
    }
    static async Task LifecycleCases()
    {
        foreach(string result in new[]{"Completed","Skipped","Abandoned"})
        {
            Reset();Task task=Play();Check(!task.IsCompleted&&Attaches==1,"real reader session stays local while reading: "+result);
            Check(ReferenceEquals(ActiveScreenContext.Instance.GetCurrentScreen(),Reader),"actual patched native screen getter selects local reader");
            Check(!(bool)Call(P,"CanPlayLocal",Owner)!,"another story/preview cannot overlap the current session");
            if(result=="Completed")Invoke(typeof(NLibraryEvent).GetMethod("ShowChoices",F)!,Reader);
            else Close(result);
            await task;Check(GetResult(task)==result,"actual local node completion/callback returns "+result);Eq(Detaches,1,"session detaches visual once");
            Close("Abandoned");Eq(Detaches,1,"later tree-exit cannot overwrite completed result or detach twice");
            Check(ReferenceEquals(ActiveScreenContext.Instance.GetCurrentScreen(),Underlay),"closing returns exact original screen object");Eq(FocusRestores,1,"actual focus restoration reaches only original screen context");Eq(NetworkCalls,0,"reading and closing sends no synchronized story/screen messages");
        }
        Reset();Task pending=Play();NativeScreen=Covering;Check(ReferenceEquals(ActiveScreenContext.Instance.GetCurrentScreen(),Covering),"native pause/inspect context takes priority over reader");
        Check((bool)Call(P,"ObserveReader",Reader)!&&!Visible,"foreign screen pauses and hides local reader");NativeScreen=Underlay;Call(P,"ObserveReader",Reader);Check(Visible&&ReferenceEquals(ActiveScreenContext.Instance.GetCurrentScreen(),Reader),"reader resumes when original underlay becomes current");
        Close("Completed");await pending;
        Reset();pending=Play();Current=false;AnchorValid=false;Call(P,"ObserveReader",Reader);await pending;Check(GetResult(pending)=="Abandoned","room/run or original-screen invalidation abandons reading");Eq(FocusRestores,0,"stale UI is not resurrected or focused");
        Reset();ThrowDetach=true;pending=Play();Close("Skipped");await pending;Check(GetResult(pending)=="Skipped"&&Detaches==1,"visual cleanup failure still settles explicit skipped result");
        Reset();var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);LoadGate=gate.Task;pending=Play();Check(!pending.IsCompleted&&Attaches==0,"loading awaits only local assets before attaching");
        Task other=Play();await other;Check(GetResult(other)=="Abandoned"&&Loads==1,"parallel preview cannot duplicate asset loading or claim reader");
        Call(P,"CancelAll");await pending;Check(GetResult(pending)=="Abandoned"&&Attaches==0,"run exit cancels local preload without waiting for asset completion");gate.TrySetResult(true);Eq(NetworkCalls,0,"cancellation sends no event/screen synchronization");
        Reset();Ready=false;Task unavailable=Play();await unavailable;Check(GetResult(unavailable)=="Abandoned"&&Loads==0,"UI readiness guard prevents premature start");
    }
    static void CompiledContract()
    {
        using var module=ModuleDefinition.ReadModule(Mod.Location);
        var names=new[]{"HBRGOSakiMod.Core.Helpers.HbrMemoryUpgradeStoryPresentation","HBRGOSakiMod.Core.Nodes.Events.NLibraryEvent","HBRGOSakiMod.Core.DevConsole.HbrMemoryUpgradeStoryPreviewConsoleCmd"};
        IEnumerable<TypeDefinition> All(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(All));
        var source=names.Select(module.GetType).Where(t=>t!=null).SelectMany(All).ToArray();
        var direct=source.Where(t=>t.FullName!=names[1]).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
        Check(!direct.Any(m=>m.DeclaringType.Name.Contains("EventSynchronizer")||m.Name=="SyncLocalScreen"||m.Name=="SetCurrentRoom"||m.Name=="Resume"),"compiled local bridge contains no synchronized event/room replacement calls");
        Check(!direct.Any(m=>m.DeclaringType.Name=="NOverlayStack"&&(m.Name is "Push" or "Remove")),"compiled attach/close never enter native overlay stack");
        Check(!direct.Any(m=>m.DeclaringType.Name=="HbrMemoryGardenUpgradeStoryLocalStore"),"reader and preview have no persistence side effects");
        var node=module.GetType(names[1]);var ready=node.Methods.Single(m=>m.Name=="_Ready");var finish=node.Methods.Single(m=>m.Name=="ShowChoices");
        bool Calls(MethodDefinition m,string name)=>m.Body.Instructions.Any(i=>i.Operand is MethodReference r&&r.Name==name);
        Check(Calls(ready,"RefreshUpgradeMemory")&&Calls(finish,"CompleteUpgradeMemory"),"actual reader routes local initialization and completion through isolated mode");
        var args=module.GetType(names[2]).Methods.Single(m=>m.Name=="Process");Check(!Calls(args,"MarkSeen")&&!Calls(args,"MarkStarted"),"preview command bypasses both record ledgers");
    }
    public static void Run(string path)=>RunAsync(path).GetAwaiter().GetResult();
    static async Task RunAsync(string path)
    {
        string? error=null;try{Install();CatalogCases();await LifecycleCases();CompiledContract();}catch(Exception ex){error=ex.ToString();Console.Error.WriteLine(ex);}
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),profile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Real compiled schema/catalog cache and async local presentation lifecycle; native screen-context Harmony target, real local node Initialize/Complete callbacks; compiled actual attach/close contract audit. Godot capture/load/attach/detach/coverage and world validity are explicit controlled leaves, no engine or render. Native event/screen synchronization methods observed and must remain unused. No actual map/reward rendering, production story content, full game save, package or upload."},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" local reader checks");if(error!=null)throw new Exception("See "+path);
    }
}
