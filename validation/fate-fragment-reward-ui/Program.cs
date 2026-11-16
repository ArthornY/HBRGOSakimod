using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Environment = System.Environment;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Rewards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Combat.Rewards;

static class RewardUiStandalone { public static void Run(string path)=>RewardUiChecks.Run(path).GetAwaiter().GetResult(); }
static class RewardUiChecks
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static readonly Assembly Mod=typeof(HbrFateFragmentReward).Assembly;
 static readonly Harmony H=new("hbr.fate-fragment-reward-ui.checks");
 static readonly List<string> Passed=[];
 static readonly List<object> Samples=[];
 static Type Patch=null!,Runtime=null!; static Player Player=null!; static RunState CurrentRun=null!;
 static bool ChildSuccess=true,ChildThrows; static int ChildCalls,Skipped,HookCalls; static LinkedRewardSet? Selecting; static object? SelectingReceipt;
 static bool Negative=>Environment.GetEnvironmentVariable("HBR_REWARD_UI_NEGATIVE")=="1";
 static void Check(bool yes,string name){if(!yes)throw new Exception(name);Passed.Add(name);}
 static T Blank<T>(){T value=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));GC.SuppressFinalize(value!);return value;}
 static FieldInfo Field(Type type,string name){for(Type? t=type;t!=null;t=t.BaseType)if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f)return f;throw new MissingFieldException(type.FullName,name);}
 static void Set(object owner,string name,object? value)=>Field(owner.GetType(),name).SetValue(owner,value);
 static object? Invoke(MethodInfo method,object? owner,params object?[] args){try{return method.Invoke(owner,args);}catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
 static object? Call(string name,params object?[] args)=>Invoke(Patch.GetMethod(name,F)!,null,args);
 static object Receipt()=>Activator.CreateInstance(Patch.GetNestedType("CompletionReceipt",F)!,true)!;
 static bool Gate(LinkedRewardSet? group,object? receipt)=>(bool)Call("TryClaimCompletion",group,receipt)!;
 static bool IsFragment(LinkedRewardSet? group)=>(bool)Call("IsFateFragmentGroup",group)!;
 static bool Skip()=>false;
 static bool StringLeaf(StringName __instance){GC.SuppressFinalize(__instance);return false;}
 static bool Listeners(ref IEnumerable<AbstractModel> __result){__result=Array.Empty<AbstractModel>();return false;}
 static bool ChildLeaf(HbrFateFragmentReward __instance,ref Task<bool> __result)
 {
  ChildCalls++;if(Selecting!=null&&SelectingReceipt!=null)Check(!Gate(Selecting,SelectingReceipt),"parent completion cannot be claimed while the real child selection is still running");
  __result=ChildThrows?Task.FromException<bool>(new InvalidOperationException("controlled fragment claim failure")):Task.FromResult(ChildSuccess);return false;
 }
 static void ObserveReward(IReadOnlyList<object> ignored){}
 static void BeforeRewardTaken(Reward reward)=>HookCalls++;
 static void ObserveSkip()=>Skipped++;
 static void Setup()
 {
  H.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(RewardUiChecks).GetMethod(nameof(StringLeaf),F)!));
  Type logger=typeof(Reward).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
  foreach(MethodInfo method in logger.GetMethods(F).Where(m=>m.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose" or "GetIsRunningFromGodotEditor"))H.Patch(method,prefix:new HarmonyMethod(typeof(RewardUiChecks).GetMethod(nameof(Skip),F)!));
  Player=Blank<Player>();CurrentRun=Blank<RunState>();Set(Player,"_runState",CurrentRun);
  H.Patch(typeof(RunState).GetMethod("IterateHookListeners",F)!,prefix:new HarmonyMethod(typeof(RewardUiChecks).GetMethod(nameof(Listeners),F)!));
  MethodInfo after=typeof(Hook).GetMethods(F).Single(m=>m.Name=="AfterRewardTaken");
  H.Patch(after,prefix:new HarmonyMethod(typeof(RewardUiChecks).GetMethod(nameof(BeforeRewardTaken),F)!));
 }
 static HbrFateFragmentReward Fragment(int id)
 {
  ConstructorInfo constructor=typeof(HbrFateFragmentReward).GetConstructors(F).Single(c=>c.GetParameters().Length==4);
  return (HbrFateFragmentReward)constructor.Invoke([Player,Blank<ElectronicSoldierManual>(),"one-shared-fixture-receipt","fragment-"+id]);
 }
 static LinkedRewardSet Group(out HbrFateFragmentReward[] children)
 {children=[Fragment(1),Fragment(2),Fragment(3)];return new LinkedRewardSet(children.Cast<Reward>().ToList(),Player);}
 static bool Prepare(LinkedRewardSet group,Reward child)=>(bool)Invoke(Runtime.GetMethod("TryPrepareSelection",F)!,null,group,child)!;
 static void Install()
 {
  Patch=Mod.GetType("HBRGOSakiMod.Core.Patches.HbrFateFragmentRewardUiPatch",true)!;
  MethodInfo reload=typeof(NLinkedRewardSet).GetMethod("Reload",F)!;
  MethodInfo from=typeof(Callable).GetMethod(nameof(Callable.From),[typeof(Action)])!;
  var il=PatchProcessor.GetOriginalInstructions(reload).ToList();
  Check(il.Count(op=>op.Calls(from))==1,"active native Reload has exactly one legacy zero-argument callable factory");
  Check(H.CreateClassProcessor(Patch).Patch().Count==1,"actual production reward UI transpiler installs on native Reload");
  Check(Harmony.GetPatchInfo(reload)!.Transpilers.Any(p=>p.owner==H.Id&&p.PatchMethod.DeclaringType==Patch),"real native Reload owns the installed production transpiler");
  MethodInfo transpiler=Patch.GetMethod("Transpiler",F)!;
  var rewritten=((IEnumerable<CodeInstruction>)Invoke(transpiler,null,il)!).ToList();
  Check(rewritten.Count(op=>op.Calls(Patch.GetMethod("CreateClaimCallable",F)!))==1&&!rewritten.Any(op=>op.Calls(from)),"the real transpiler rewrites only the unique native callable factory");
  Check(il.Count==rewritten.Count&&il.Zip(rewritten).Count(pair=>pair.First.opcode!=pair.Second.opcode||!Equals(pair.First.operand,pair.Second.operand))==1,"all remaining native Reload IL instructions are unchanged");
  Type ritsu=typeof(LinkedRewardSets).Assembly.GetType("STS2RitsuLib.Combat.Rewards.Patches.LinkedRewardSetOnSelectPatch",true)!;
  MethodInfo onSelect=typeof(LinkedRewardSet).GetMethod("OnSelect",F)!;
  H.Patch(onSelect,prefix:new HarmonyMethod(ritsu.GetMethod("Prefix",F)!));
  Check(Harmony.GetPatchInfo(onSelect)!.Prefixes.Any(p=>p.owner==H.Id&&p.PatchMethod.DeclaringType==ritsu),"actual Ritsu ChooseOne OnSelect patch is installed on the real native backend");
  Runtime=typeof(LinkedRewardSets).Assembly.GetType("STS2RitsuLib.Combat.Rewards.LinkedRewardSetRuntime",true)!;
  H.Patch(typeof(HbrFateFragmentReward).GetMethod("OnSelect",F)!,prefix:new HarmonyMethod(typeof(RewardUiChecks).GetMethod(nameof(ChildLeaf),F)!));
  H.Patch(typeof(Reward).GetMethod("OnSkipped",F)!,prefix:new HarmonyMethod(typeof(RewardUiChecks).GetMethod(nameof(ObserveSkip),F)!));
 }
 static Action NodeAction(LinkedRewardSet group,out NLinkedRewardSet node)
 {
  node=Blank<NLinkedRewardSet>();Set(node,"<LinkedRewardSet>k__BackingField",group);
  return (Action)Delegate.CreateDelegate(typeof(Action),node,typeof(NLinkedRewardSet).GetMethod("GetReward",F)!);
 }
 static void CallableCases()
 {
  var group=Group(out _);Action original=NodeAction(group,out var node);
  var callable=(Callable)Call("CreateClaimCallable",original)!;
  Check(callable.Delegate is Action<NRewardButton>,"actual production factory creates a genuine one-NRewardButton delegate without engine execution");
  Check(callable.Delegate.Method.GetParameters().Length==1&&callable.Delegate.GetType().GenericTypeArguments.Single()==typeof(NRewardButton),"typed callback arity/type matches native child RewardClaimed signature");
  var second=(Callable)Call("CreateClaimCallable",original)!;
  object captured=callable.Delegate.Target!,again=second.Delegate.Target!;
  object CapturedReceipt(object closure)=>closure.GetType().GetFields(F).Single(f=>f.FieldType==Patch.GetNestedType("CompletionReceipt",F)).GetValue(closure)!;
  Check(ReferenceEquals(CapturedReceipt(captured),CapturedReceipt(again)),"repeated factory construction for one group node shares one completion receipt");
  Action otherOriginal=NodeAction(group,out _);var other=(Callable)Call("CreateClaimCallable",otherOriginal)!;
  Check(!ReferenceEquals(CapturedReceipt(captured),CapturedReceipt(other.Delegate.Target!)),"different group nodes have independent completion receipts");
  int invocations=0;Action plain=()=>invocations++;
  var plainResult=(Callable)Call("CreateClaimCallable",plain)!;
  Check(ReferenceEquals(plainResult.Delegate,plain),"non-group callbacks preserve the exact original native zero-argument delegate");
  plainResult.Delegate.DynamicInvoke();Check(invocations==1,"the unchanged ordinary delegate remains callable");
  var ordinary=new LinkedRewardSet([new DummyReward(Player)],Player);var ordinaryAction=NodeAction(ordinary,out _);
  Check(ReferenceEquals(((Callable)Call("CreateClaimCallable",ordinaryAction)!).Delegate,ordinaryAction),"non-fragment linked group retains the exact original callback");
  var derived=new DerivedGroup([Fragment(9)],Player);var derivedAction=NodeAction(derived,out _);
  Check(!IsFragment(derived)&&ReferenceEquals(((Callable)Call("CreateClaimCallable",derivedAction)!).Delegate,derivedAction),"derived linked group behavior is never captured by this fragment-only fix");
  Check(!IsFragment(null)&&!IsFragment(new LinkedRewardSet([],Player)),"null and empty groups are not newly classified as fragment groups");
  Check(!IsFragment(new LinkedRewardSet([Fragment(1),new DummyReward(Player)],Player)),"mixed custom/native reward group is left untouched");
  MethodInfo factory=Patch.GetMethod("CreateClaimCallable",F)!;
  Check(PatchProcessor.GetOriginalInstructions(factory).Count(i=>i.operand is MethodInfo m&&m.DeclaringType==typeof(Callable)&&m.Name=="From"&&m.IsGenericMethod&&m.GetGenericArguments().SequenceEqual([typeof(NRewardButton)]))==1,"production IL uses the native typed factory exactly once, without a generic detour");
  MethodInfo notify=Patch.GetMethod("NotifyClaimed",F)!;var notifyIl=PatchProcessor.GetOriginalInstructions(notify).ToArray();
  Check(notifyIl.Count(i=>i.operand is MethodInfo m&&m.Name=="EmitSignal")==1&&!notifyIl.Any(i=>i.operand is MethodInfo m&&m.Name=="RewardCollectedFrom"),"completion emits one native group signal and never directly calls screen removal");
  Check(typeof(NLinkedRewardSet.RewardClaimedEventHandler).GetMethod("Invoke")!.GetParameters().Select(p=>p.ParameterType).SequenceEqual([typeof(NLinkedRewardSet)]),"native parent completion signal contract carries exactly its group node");
 }
 static async Task SelectionCases()
 {
  Check(!Gate(null,Receipt()),"null parent cannot notify");
  for(int pick=0;pick<3;pick++)
  {
   var group=Group(out var children);object receipt=Receipt();Selecting=group;SelectingReceipt=receipt;ChildCalls=Skipped=HookCalls=0;ChildSuccess=true;ChildThrows=false;
   Check(IsFragment(group)&&group.Rewards.Count==3,"actual three-fragment group is classified before choice "+pick);
   Check(!Gate(group,receipt),"unselected parent cannot notify "+pick);
   Check(Prepare(group,children[pick]),"actual Ritsu prepares the requested physical child "+pick);
   bool success=await group.SelectUnsynchronized();
   Check(success&&group.SuccessfullySelected,"native parent selection sets success only after the actual Ritsu completion "+pick);
   Check(children[pick].SuccessfullySelected&&children.Where((_,index)=>index!=pick).All(r=>!r.SuccessfullySelected),"only selected child obtains its native success flag "+pick);
   Check(group.Rewards.Count==0&&children.All(r=>r.ParentRewardSet==null),"actual ChooseOne removes chosen and all sibling backend options "+pick);
   Check(ChildCalls==1&&Skipped==2&&HookCalls==2,"choice invokes one child claim, skips two siblings, and runs native child/parent reward hooks "+pick);
   Check(!Prepare(group,children[(pick+1)%3]),"late sibling selection cannot be prepared after backend completion "+pick);
   Check(!IsFragment(group)&&Gate(group,receipt),"already captured completion still notifies after Ritsu has emptied the group "+pick);
   Check(!Gate(group,receipt)&&!Gate(group,receipt),"duplicate group callbacks cannot notify twice "+pick);
   object concurrent=Receipt();int won=0;Parallel.For(0,32,_=>{if(Gate(group,concurrent))Interlocked.Increment(ref won);});
   Check(won==1,"receipt CompareExchange admits exactly one concurrent completion "+pick);
   Check(!Gate(group,null),"missing receipt cannot claim completion "+pick);
   Samples.Add(new{pick,children=children.Length,success,parent=group.SuccessfullySelected,remaining=group.Rewards.Count,ChildCalls,Skipped,HookCalls});
  }
  foreach(bool throws in new[]{false,true})
  {
   var group=Group(out var children);object receipt=Receipt();Selecting=group;SelectingReceipt=receipt;ChildSuccess=false;ChildThrows=throws;
   Check(Prepare(group,children[1]),"pending choice accepts controlled rejected/failed claim");
   Exception? error=null;bool success=false;try{success=await group.SelectUnsynchronized();}catch(Exception exception){error=exception;}
   Check(throws?error is InvalidOperationException:error==null&&!success,"actual rejected/throwing child result propagates without fake completion");
   Check(!group.SuccessfullySelected&&children.All(r=>!r.SuccessfullySelected)&&group.Rewards.Count==3&&children.All(r=>r.ParentRewardSet==group),"failed claim preserves all children and the native parent success state");
   Check(!Gate(group,receipt),"failed claim does not consume notification receipt");
   ChildSuccess=true;ChildThrows=false;Check(Prepare(group,children[1]),"real Ritsu can prepare a retry after rejected/failed claim");
   Check(await group.SelectUnsynchronized()&&Gate(group,receipt),"later successful retry completes and notifies once");
  }
  Selecting=null;SelectingReceipt=null;
  var regularChildren=new[]{new DummyReward(Player),new DummyReward(Player)};
  var regular=LinkedRewardSets.Create(regularChildren,Player,LinkedRewardSelectionMode.TakeAll);
  Check(Prepare(regular,regularChildren[0])&&await regular.SelectUnsynchronized(),"ordinary configured TakeAll still completes through actual Ritsu backend");
  Check(regularChildren.All(r=>r.SuccessfullySelected)&&regular.Rewards.Count==0,"ordinary TakeAll semantics retain both claimed children");
 }
 static void NegativeCases()
 {
  Check(Mod.GetType("HBRGOSakiMod.Core.Patches.HbrFateFragmentRewardUiPatch")==null,"previous release lacks the new fragment-only UI repair");
  MethodInfo from=typeof(Callable).GetMethod(nameof(Callable.From),[typeof(Action)])!;
  Check(PatchProcessor.GetOriginalInstructions(typeof(NLinkedRewardSet).GetMethod("Reload",F)!).Count(i=>i.Calls(from))==1,"previous runtime Reload really connects the zero-argument native factory");
  Action handler=()=>throw new Exception("must not reach the legacy callback body");var callable=Callable.From(handler);
  Check(callable.Delegate.Method.GetParameters().Length==0&&typeof(NRewardButton.RewardClaimedEventHandler).GetMethod("Invoke")!.GetParameters().Length==1,"native reward signal and previous actual callable have incompatible arities");
  Exception? mismatch=null;try{callable.Delegate.DynamicInvoke((object?)null);}catch(Exception e){mismatch=e;}
  Check(mismatch is TargetParameterCountException,"actual previous zero-argument delegate rejects the native signal's one argument before invoking its body");
 }
 public static async Task Run(string path)
 {
  string? error=null;try{Setup();if(Negative)NegativeCases();else{Install();CallableCases();await SelectionCases();}}catch(Exception exception){error=exception.ToString();Console.Error.WriteLine(exception);}
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,cases=Samples,negativeControl=Negative,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual native Reload Harmony installation/unique IL callsite, real managed Callable factory/delegate signature, real Ritsu ChooseOne and native SelectUnsynchronized flags. Fragment OnSelect claim result is a controlled leaf; real claim/net transactions are untouched. Completion gate is pure and exercised without Godot Emit. Negative delegate arity rejection is CLR DynamicInvoke, not a Godot signal dispatch. No full reward UI/engine/gameplay or multiplayer transport test."},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" fate fragment reward UI checks");if(error!=null)throw new Exception("See "+path);
 }
 sealed class DummyReward(Player player):Reward(player)
 {
  protected override RewardType RewardType=>RewardType.None;public override int RewardsSetIndex=>0;public override MegaCrit.Sts2.Core.Localization.LocString Description=>new("gameplay_ui","unused");public override bool IsPopulated=>true;
  public override void Populate(){} protected override Task<bool> OnSelect()=>Task.FromResult(true);public override void MarkContentAsSeen(){}
 }
 sealed class DerivedGroup(List<Reward> rewards,Player player):LinkedRewardSet(rewards,player);
}
