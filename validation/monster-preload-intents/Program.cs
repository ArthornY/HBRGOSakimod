using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;

static class PreloadStandalone { public static void Run(string path)=>ActRosterChecks.RunPreload(path); }
static partial class ActRosterChecks
{
 static bool PreloadNegative=>Environment.GetEnvironmentVariable("HBR_MONSTER_PRELOAD_NEGATIVE")=="1";
 static readonly List<object> PreloadRows=[];
 static readonly MethodInfo NativeGetIntents=typeof(MonsterModel).GetMethod("GetIntents",F)!;
 static object? MonsterField(MonsterModel model,string name)=>typeof(MonsterModel).GetField(name,F)!.GetValue(model);
 static MonsterModel PreloadPrototype(Type type)
 {
  if(!ModelDb.Contains(type))Inject(type);
  return ModelDb.GetById<MonsterModel>(ModelDb.GetId(type));
 }
 static string Cursor(MonsterModel model)=>JsonSerializer.Serialize(new{
  table=((HbrgoMoveTableMonster)model).MoveTableIndex,step=((HbrgoMoveTableMonster)model).MoveTableStep,
  dp=((HbrgoMoveTableMonster)model).DpOnceUsedMask,hp=((HbrgoMoveTableMonster)model).HpOnceUsedMask,
  queue=((HbrgoMoveTableMonster)model).FlavorQueuedTableMoveId,data=((HbrgoMoveTableMonster)model).FlavorQueueCursorData,
  paused=((HbrgoMoveTableMonster)model).FlavorTablePaused});
 static List<AbstractIntent> NativeIntents(MonsterModel model)=>(List<AbstractIntent>)Invoke(NativeGetIntents,model)!;
 static void NativePreloadContract()
 {
  MethodInfo assets=typeof(MonsterModel).GetProperty(nameof(MonsterModel.AssetPaths))!.GetMethod!;
  Check(PatchProcessor.GetOriginalInstructions(assets).Count(i=>i.Calls(NativeGetIntents))==1,"actual native AssetPaths invokes its private GetIntents once without needing a combat actor");
  Check(PatchProcessor.GetOriginalInstructions(NativeGetIntents).Count(i=>i.operand is MethodInfo m&&m.Name=="GenerateMoveStateMachine")==1,"actual native GetIntents generates a separate complete move state machine for asset discovery");
  var model=PreloadPrototype(typeof(AbyssKnockerKiller));Exception? guard=null;try{_ = model.Creature;}catch(Exception e){guard=e;}
  Check(guard is InvalidOperationException&&guard.Message=="Creature was accessed before it was set.","the real native unbound Creature getter still throws; no global guard/assertion bypass");
 }
 static void PreviousPreloadFailure()
 {
  foreach(Type type in new[]{typeof(AbyssKnockerKiller),typeof(IvoryScooper)})
  foreach(bool clone in new[]{false,true})
  {
   MonsterModel canonical=PreloadPrototype(type),model=clone?(MonsterModel)canonical.ToMutable():canonical;
   Check(MonsterField(model,"_creature")==null,"old "+type.Name+" "+clone+" is genuinely unbound before asset enumeration");
   foreach(bool assets in new[]{false,true})
   {
    Exception? failure=null;try{if(assets)_=model.AssetPaths.ToArray();else _=NativeIntents(model);}catch(Exception e){failure=e;}
    Check(failure is InvalidOperationException&&failure.Message=="Creature was accessed before it was set.","old actual "+(assets?"AssetPaths":"GetIntents")+" reproduces "+type.Name+" "+(clone?"mutable-unbound":"canonical")+" preload failure");
   }
   Check(MonsterField(model,"_creature")==null,"negative native enumeration never secretly binds a replacement actor");
  }
 }
 static void UnboundPreloadCases()
 {
  Type[] types=Mod.GetTypes().Where(t=>!t.IsAbstract&&typeof(HbrgoMoveTableMonster).IsAssignableFrom(t)).OrderBy(t=>t.Name,StringComparer.Ordinal).ToArray();
  Check(types.Length>2,"preload sweep is limited to the actual shared HBR table-monster family");
  foreach(Type type in types)
  foreach(bool clone in new[]{false,true})
  {
   MonsterModel canonical=PreloadPrototype(type),model=clone?(MonsterModel)canonical.ToMutable():canonical;
   string cursor=Cursor(model),label=type.Name+"/"+(clone?"mutable-unbound":"canonical");
   object? rng=MonsterField(model,"_rng"),runRng=MonsterField(model,"_runRng"),machine=MonsterField(model,"_moveStateMachine");
   AbstractIntent[] intents=NativeIntents(model).ToArray();string[] paths=model.AssetPaths.ToArray();
   Check(intents.Length>0&&paths.Length>0&&paths.All(path=>path.StartsWith("res://",StringComparison.Ordinal)),label+" executes native GetIntents and AssetPaths with real path strings");
   Check(paths.SequenceEqual(model.AssetPaths),label+" repeated asset discovery is deterministic");
   Check(MonsterField(model,"_creature")==null&&ReferenceEquals(rng,MonsterField(model,"_rng"))&&ReferenceEquals(runRng,MonsterField(model,"_runRng"))&&ReferenceEquals(machine,MonsterField(model,"_moveStateMachine"))&&Cursor(model)==cursor,label+" leaves binding, native combat machine, RNG references and saved table cursor unchanged");
   if(type==typeof(AbyssKnockerKiller)||type==typeof(IvoryScooper))
   {
    Check(intents.Any(intent=>intent is DebuffIntent),label+" preloads the potential debuff intent before mode/side is known");
    Check(new DebuffIntent().AssetPaths.All(paths.Contains),label+" native asset enumeration includes actual potential-debuff resources");
   }
   PreloadRows.Add(new{monster=type.Name,form=clone?"mutable-unbound":"canonical",intents=intents.Select(i=>i.GetType().Name),assetCount=paths.Length});
  }
 }
 static MonsterModel BindPreloadMonster(Type type,HeroKind kind,int asc,bool phantom)
 {
  ResetTable(kind,asc,dp:500);Set(Hero,"_currentHp",5000);
  var model=(MonsterModel)PreloadPrototype(type).ToMutable();
  var actor=new Creature(model,phantom?CombatSide.Player:CombatSide.Enemy,null){CombatState=Combat};
  if(phantom){Set(actor,"_petOwner",HeroPlayer);Power<PhantomPower>(actor,1);Set(Combat,"_allies",new List<Creature>{Hero,actor});}
  else{Set(Combat,"_allies",new List<Creature>{Hero});Set(Combat,"_enemies",new List<Creature>{actor});}
  Set(model,"<Id>k__BackingField",ModelDb.GetId(type));model.RunRng=State<RunState>("RunState").Rng;SetUp(model);
  Check(ReferenceEquals(model.Creature,actor)&&ReferenceEquals(actor.Monster,model),"bound "+type.Name+" uses the real native Creature constructor and one-time MonsterModel binding");
  return model;
 }
 static async Task BoundPreloadCases()
 {
  foreach(Type type in new[]{typeof(AbyssKnockerKiller),typeof(IvoryScooper)})
  foreach(int asc in new[]{0,10})
  foreach(var mode in new[]{(kind:HeroKind.Hbr,phantom:false),(kind:HeroKind.Legacy,phantom:false),(kind:HeroKind.Hbr,phantom:true)})
  {
   MonsterModel model=BindPreloadMonster(type,mode.kind,asc,mode.phantom);
   string id=type==typeof(AbyssKnockerKiller)?"RAGE_WAVE_MOVE":"DESTROY_BASH_MOVE";
   MoveState move=MoveOf(model,id);bool added=mode.kind==HeroKind.Hbr&&!mode.phantom;
   string label=type.Name+"/asc"+asc+"/"+mode.kind+"/phantom"+mode.phantom;
   Check(move.Intents.Any(intent=>intent is DebuffIntent)==added,label+" advertises the attachment only for a bound HBR enemy");
   Check(NativeIntents(model).Any(intent=>intent is DebuffIntent)==added&&model.AssetPaths.Any(),label+" keeps native GetIntents/AssetPaths valid after binding");
   int attacks=Attacks.Count;await move.PerformMove([Hero]);
   int raw=type==typeof(AbyssKnockerKiller)?asc==0?15:17:asc==0?9:10;
   Check(Attacks.Count==attacks+1&&Attacks.Last().Damage==raw&&Attacks.Last().Hits==1&&AttackScopes.Last()=="AllUnits",label+" retains the original raw damage, one hit and all-unit scope");
   if(type==typeof(AbyssKnockerKiller))
   {
    HbrCalamityPower? power=Hero.GetPower<HbrCalamityPower>();
    Check(added?power!=null&&power.Amount==(asc==0?20:30)&&power.RemainingTurns==2:power==null,label+" retains actual twenty/thirty Calamity and two-turn duration only on HBR enemies");
   }
   else
   {
    HbrContinuousDamagePower? power=Hero.GetPower<HbrContinuousDamagePower>();
    Check(added?power!=null&&EffectStrength(power)==(asc==0?10:15)&&power.Amount==4:power==null,label+" retains actual ten/fifteen percent continuous damage and four-turn duration only on HBR enemies");
   }
  }
 }
 public static void RunPreload(string reportPath)
 {
  string? error=null;try{
   FlavorInstall();NativePreloadContract();
   foreach(Type type in new[]{typeof(HbrContinuousDamagePower),typeof(HbrCalamityPower)})if(!ModelDb.Contains(type))Inject(type);
   if(PreloadNegative)PreviousPreloadFailure();else{UnboundPreloadCases();BoundPreloadCases().GetAwaiter().GetResult();}
  }catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);}
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
  File.WriteAllText(reportPath,JsonSerializer.Serialize(new{ok=error==null,checks=Passed.Count,passed=Passed,error,negativeControl=PreloadNegative,cases=PreloadRows,modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),boundary="Actual native MonsterModel.GetIntents/AssetPaths, canonical ToMutable clones, real Creature binding constructor, move bodies and original status application. Shared roster fixture controls attack/animation/wait/preview/history terminals and ascension level, not native preload enumeration or source-binding guard. Only table-family asset discovery and the two affected monsters' HBR/legacy/phantom low/high difficulty moves execute. No Godot/resource loading/rendering or full encounter entry."},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine((error==null?"PASS ":"FAIL ")+Passed.Count+" monster preload intent checks");if(error!=null)throw new Exception("See "+reportPath);
 }
}
