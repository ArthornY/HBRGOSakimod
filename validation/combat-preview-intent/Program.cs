using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Environment = System.Environment;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

static class PreviewStandalone { public static void Run(string path)=>PreviewChecks.Run(path).GetAwaiter().GetResult(); }
static class PreviewChecks
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 static readonly Assembly Mod=typeof(SmallHopper).Assembly;
 static readonly Harmony Harmony=new("hbr.preview-player-intent.native-checks");
 static readonly List<string> Passed=[];
 static readonly List<object> Receipts=[];
 static readonly Dictionary<Creature,NCreature> Nodes=[];
 static CombatManager Manager=null!;static NCombatRoom Room=null!;static CombatState Combat=null!;
 static Creature Hero=null!,Enemy=null!,Summon=null!;static bool RoomAvailable=true,NodeValid=true,InsideTree=true;
 static int RoomReads,NodeReads,Updates,Notifications;static string? Caller;static Creature[] LastTargets=[];
 static bool Negative=>Environment.GetEnvironmentVariable("HBR_PREVIEW_INTENT_NEGATIVE")=="1";
 static void Check(bool valid,string message){if(!valid)throw new Exception(message);Passed.Add(message);}
 static T Blank<T>(){T value=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));GC.SuppressFinalize(value!);return value;}
 static FieldInfo Field(Type type,string name){for(Type? t=type;t!=null;t=t.BaseType)if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} field)return field;throw new MissingFieldException(type.FullName,name);}
 static void Set(object owner,string name,object? value)=>Field(owner.GetType(),name).SetValue(owner,value);
 static bool StringLeaf(StringName __instance){GC.SuppressFinalize(__instance);return false;}
 static bool ManagerLeaf(ref CombatManager __result){__result=Manager;return false;}
 static bool RoomLeaf(ref NCombatRoom? __result){RoomReads++;__result=RoomAvailable?Room:null;return false;}
 static bool NodeLeaf(Creature __0,ref NCreature? __result){NodeReads++;__result=Nodes.GetValueOrDefault(__0);return false;}
 static bool ValidLeaf(GodotObject? __0,ref bool __result){__result=__0!=null&&NodeValid;return false;}
 static bool TreeLeaf(ref bool __result){__result=InsideTree;return false;}
 static bool NotifyLeaf(string __0){Notifications++;Caller=__0;return false;}
 static Task UpdateTerminal(NCreature node,IEnumerable<Creature> targets){Updates++;LastTargets=targets.ToArray();Receipts.Add(new{side=node.Entity.Side.ToString(),monster=node.Entity.Monster?.GetType().Name,targetCount=LastTargets.Length});return Task.CompletedTask;}
 static IEnumerable<CodeInstruction> KeepNativeGuard(IEnumerable<CodeInstruction> instructions)
 {
  var il=instructions.ToList();int guardThrow=il.FindIndex(i=>i.opcode==OpCodes.Throw);
  if(guardThrow<0||!il.Take(guardThrow).Any(i=>i.opcode==OpCodes.Ldstr&&Equals(i.operand,"Only valid on monsters."))||il.Take(guardThrow+1).Any(i=>i.blocks.Count!=0))throw new Exception("Expected native synchronous monster guard without exception regions in the retained prefix");
  Check(il.Take(guardThrow).Any(i=>i.operand is MethodInfo m&&m.Name=="get_Monster"),"actual native UpdateIntent guard tests Entity.Monster before its real throw");
  foreach(var instruction in il.Take(guardThrow+1))yield return instruction;
  var terminal=new CodeInstruction(OpCodes.Ldarg_0);terminal.labels.AddRange(il[guardThrow+1].labels);yield return terminal;
  yield return new CodeInstruction(OpCodes.Ldarg_1);
  yield return new CodeInstruction(OpCodes.Call,typeof(PreviewChecks).GetMethod(nameof(UpdateTerminal),F)!);
  yield return new CodeInstruction(OpCodes.Ret);
 }
 static void Patch(MethodBase target,string prefix)=>Harmony.Patch(target,prefix:new HarmonyMethod(typeof(PreviewChecks).GetMethod(prefix,F)!));
 static Creature Actor(bool monster,CombatSide side)
 {
  var actor=Blank<Creature>();Set(actor,"<CombatState>k__BackingField",Combat);Set(actor,"<Side>k__BackingField",side);
  if(monster)Set(actor,"<Monster>k__BackingField",Blank<SmallHopper>());
  else Set(actor,"<Player>k__BackingField",Blank<Player>());
  var node=Blank<NCreature>();Set(node,"<Entity>k__BackingField",actor);Nodes[actor]=node;return actor;
 }
 static void Setup()
 {
  Patch(typeof(StringName).GetConstructor([typeof(string)])!,nameof(StringLeaf));
  Manager=Blank<CombatManager>();Set(Manager,"<StateTracker>k__BackingField",Blank<CombatStateTracker>());
  Patch(typeof(CombatManager).GetProperty("Instance",F)!.GetMethod!,nameof(ManagerLeaf));
  Patch(typeof(CombatStateTracker).GetMethod("NotifyCombatStateChanged",F)!,nameof(NotifyLeaf));
  Room=Blank<NCombatRoom>();Patch(typeof(NCombatRoom).GetProperty("Instance",F)!.GetMethod!,nameof(RoomLeaf));
  Patch(typeof(NCombatRoom).GetMethods(F).Single(m=>m.Name=="GetCreatureNode"&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual([typeof(Creature)])),nameof(NodeLeaf));
  Patch(typeof(GodotObject).GetMethod("IsInstanceValid",[typeof(GodotObject)])!,nameof(ValidLeaf));
  Patch(typeof(Node).GetMethod("IsInsideTree",Type.EmptyTypes)!,nameof(TreeLeaf));
  Harmony.Patch(typeof(NCreature).GetMethod("UpdateIntent",F)!,transpiler:new HarmonyMethod(typeof(PreviewChecks).GetMethod(nameof(KeepNativeGuard),F)!));
  Combat=Blank<CombatState>();Hero=Actor(false,CombatSide.Player);Enemy=Actor(true,CombatSide.Enemy);Summon=Actor(true,CombatSide.Player);
  Set(Summon,"_petOwner",Hero.Player);Set(Combat,"_allies",new List<Creature>{Hero,Summon});Set(Combat,"_enemies",new List<Creature>{Enemy});
 }
 static Task Refresh(Creature? creature)
 {
  try{return (Task)Mod.GetType("HBRGOSakiMod.Core.Helpers.CombatPreviewRefreshHelper",true)!.GetMethod("RefreshCreatureIntent",F)!.Invoke(null,[creature])!;}
  catch(TargetInvocationException e)when(e.InnerException!=null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
 }
 static void ResetCounts(){RoomReads=NodeReads=Updates=Notifications=0;Caller=null;LastTargets=[];RoomAvailable=NodeValid=InsideTree=true;}
 static async Task Cases()
 {
  await Refresh(null);var detached=Actor(false,CombatSide.Player);Set(detached,"<CombatState>k__BackingField",null);await Refresh(detached);
  Check(RoomReads==0&&Updates==0&&Notifications==0,"null/detached actors return before any engine lookup or state notification");
  ResetCounts();Exception? guard=null;try{await Nodes[Hero].UpdateIntent([Enemy]);}catch(Exception e){guard=e;}
  Check(guard is InvalidOperationException&&guard.Message=="Only valid on monsters."&&Updates==0,"the genuine native player guard still throws; the host has not replaced UpdateIntent with a permissive leaf");
  ResetCounts();Exception? error=null;try{await Refresh(Hero);}catch(Exception e){error=e;}
  if(Negative){
   Check(error is InvalidOperationException&&error.Message=="Only valid on monsters.","released actual helper reproduces the remote player's native UpdateIntent exception");
   Check(RoomReads==1&&NodeReads==1&&Updates==0&&Notifications==0,"released failure occurs after real helper node routing and before any render terminal");return;
  }
  Check(error==null,"player refresh now completes through the actual production helper");
  Check(RoomReads==0&&NodeReads==0&&Updates==0,"player path stops before NCombatRoom and NCreature lookup");
  Check(Notifications==1&&Caller=="RefreshCreatureIntent","player stat changes still invoke the existing coalesced state-tracker notification");
  ResetCounts();await Refresh(Enemy);
  Check(RoomReads==1&&NodeReads==1&&Updates==1&&Notifications==0,"enemy still reaches the real guarded native UpdateIntent once");
  Check(LastTargets.SequenceEqual(new[]{Hero,Summon}),"enemy receives the actual combat-state opponents including the allied summon");
  ResetCounts();await Refresh(Summon);
  Check(Updates==1&&LastTargets.SequenceEqual(new[]{Enemy}),"allied monster summon retains intent refresh with enemy targets; no Enemy-side-only restriction");
  ResetCounts();RoomAvailable=false;await Refresh(Enemy);Check(RoomReads==1&&NodeReads==0&&Updates==0,"missing combat room retains its original harmless return");
  ResetCounts();NodeValid=false;await Refresh(Enemy);Check(NodeReads==1&&Updates==0,"invalid creature node still bypasses native rendering");
  ResetCounts();InsideTree=false;await Refresh(Enemy);Check(Updates==0,"detached creature node still bypasses native rendering");
  ResetCounts();Nodes.Remove(Enemy);await Refresh(Enemy);Check(NodeReads==1&&Updates==0,"missing indexed creature node remains harmless");
 }
 public static async Task Run(string path)
 {
  string? error=null;try{Setup();await Cases();}catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  File.WriteAllText(path,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,negativeControl=Negative,receipts=Receipts,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual production RefreshCreatureIntent and RefreshCombatState execute. Real native UpdateIntent Entity.Monster guard and throw IL retained; only rendering after that guard is replaced with a recording Task terminal. Room/node lookup, Godot validity/tree checks and native tracker notification are controlled engine terminals. Real CombatState.GetOpponentsOf supplies targets. No whole-helper skip, full card OnPlay/Apply pipeline, UI engine or game rendering test."},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" combat preview intent checks");if(error!=null)throw new Exception("See "+path);
 }
}
