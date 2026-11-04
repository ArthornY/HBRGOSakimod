using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

static class OverchargeChecks {public static void Run(string report)=>WidthChecks.Run(report);}
static class WidthChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    const string ProductionOwner="hbr.healthbar-width.checks.production";
    static readonly Assembly Mod=typeof(ElectronicSoldierManual).Assembly;
    static readonly List<string> Passed=[];
    static readonly List<object> Cases=[];
    static readonly Vector2 ExistingSize=new(222f,29f);
    static Vector2 Captured;
    static int Reads,Writes;
    static MethodInfo Target=null!,NativeSetSize=null!,NativeGetSize=null!;
    static T Blank<T>(){T value=(T)RuntimeHelpers.GetUninitializedObject(typeof(T));GC.SuppressFinalize(value!);return value;}
    static FieldInfo Field(Type type,string name)
    {for(Type? cursor=type;cursor!=null;cursor=cursor.BaseType)if(cursor.GetField(name,F) is {} field)return field;throw new MissingFieldException(type.Name,name);}
    static void Set(object owner,string name,object? value)=>Field(owner.GetType(),name).SetValue(owner,value);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Passed.Add(message);}
    static void Near(float actual,float expected,string message)=>Check(Math.Abs(actual-expected)<.002f,$"{message}: {actual} = {expected}");
    static bool Skip()=>false;
    static Vector2 ReadSize(Control? control){Reads++;return ExistingSize;}
    static void CaptureSize(NHealthBar bar,Vector2 size){Writes++;Captured=size;}
    static IEnumerable<CodeInstruction> SizeLeaves(IEnumerable<CodeInstruction> instructions)
    {
        int reads=0,writes=0;
        foreach(CodeInstruction instruction in instructions)
        {
            if(instruction.operand is MethodInfo call&&call==NativeGetSize){instruction.opcode=OpCodes.Call;instruction.operand=typeof(WidthChecks).GetMethod(nameof(ReadSize),F)!;reads++;}
            else if(instruction.operand is MethodInfo write&&write==NativeSetSize){instruction.opcode=OpCodes.Call;instruction.operand=typeof(WidthChecks).GetMethod(nameof(CaptureSize),F)!;writes++;}
            yield return instruction;
        }
        if(reads!=1||writes!=1)throw new Exception($"Expected exactly one Control.Size read/private SetSize leaf; actual {reads}/{writes}");
    }
    static Type NativeType(string name)
    {
        Assembly native=typeof(NHealthBar).Assembly;using var pe=new PEReader(File.OpenRead(native.Location));var reader=pe.GetMetadataReader();
        var definition=reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(type=>reader.GetString(type.Name)==name);
        return native.GetType(reader.GetString(definition.Namespace)+"."+name,true)!;
    }
    static void Install()
    {
        var leaves=new Harmony("hbr.healthbar-width.checks.leaves");leaves.Patch(typeof(StringName).GetConstructor([typeof(string)])!,prefix:new HarmonyMethod(typeof(WidthChecks).GetMethod(nameof(Skip),F)!));
        Type logger=typeof(ModelDb).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger",true)!;
        foreach(MethodInfo method in logger.GetMethods(F).Where(method=>method.Name is "Log" or "Info" or "Warn" or "Error" or "Debug" or "Verbose" or "GetIsRunningFromGodotEditor"))leaves.Patch(method,prefix:new HarmonyMethod(typeof(WidthChecks).GetMethod(nameof(Skip),F)!));
        Target=typeof(NHealthBar).GetMethod("UpdateWidthRelativeToReferenceValue",F)!;
        Check(Target.GetParameters().Select(parameter=>parameter.ParameterType).SequenceEqual(new[]{typeof(float),typeof(float)}),"actual native relative-width method retains two float parameters");
        Check(Target.GetParameters()[0].Name=="refMaxHp","active native first parameter has production reference-MaxHP name");
        Check(Field(typeof(NHealthBar),"_creature").FieldType==typeof(Creature),"native healthbar creature field has expected actual Creature shape");
        MethodInfo[] calls=PatchProcessor.GetOriginalInstructions(Target).Select(instruction=>instruction.operand).OfType<MethodInfo>().ToArray();
        Cases.Add(new{nativeCalls=calls.Select(method=>method.DeclaringType?.FullName+"."+method.Name+"("+string.Join(",",method.GetParameters().Select(parameter=>parameter.ParameterType.Name))+")").ToArray()});
        NativeGetSize=calls.Single(method=>method.DeclaringType==typeof(Control)&&method.Name=="get_Size");
        NativeSetSize=calls.Single(method=>method.DeclaringType==typeof(NHealthBar)&&method.Name=="SetHpBarContainerSizeWithOffsetsImmediately");
        Check(NativeSetSize.ReturnType==typeof(void)&&NativeSetSize.GetParameters().Select(parameter=>parameter.ParameterType).SequenceEqual(new[]{typeof(Vector2)}),"native private SetSize leaf accepts one Vector2 and returns void");
        leaves.Patch(Target,transpiler:new HarmonyMethod(typeof(WidthChecks).GetMethod(nameof(SizeLeaves),F)!));
        Type? patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrPlayerHealthBarWidthPatch");
        if(patch!=null)
        {
            var production=new Harmony(ProductionOwner);Check(production.CreateClassProcessor(patch).Patch().Count==1,"production player width patch installs on exactly one native method");
            var info=Harmony.GetPatchInfo(Target)!;Check(info.Prefixes.Count(item=>item.owner==ProductionOwner)==1&&info.Postfixes.All(item=>item.owner!=ProductionOwner)&&info.Transpilers.All(item=>item.owner!=ProductionOwner),"production normalization changes only prefix reference argument and retains native arithmetic body");
            Check(Harmony.GetPatchInfo(NativeSetSize)?.Owners.Contains(ProductionOwner)!=true,"new production feature does not patch private fixed-size leaf");
        }
    }
    static Creature PlayerActor(int maximum,int current,bool? hbr)
    {
        var player=Blank<Player>();var creature=Blank<Creature>();Set(creature,"_powers",new List<PowerModel>());Set(creature,"<Player>k__BackingField",player);Set(creature,"<Side>k__BackingField",MegaCrit.Sts2.Core.Combat.CombatSide.Player);
        Set(creature,"_maxHp",maximum);Set(creature,"_currentHp",current);Set(player,"<Creature>k__BackingField",creature);
        var relics=new List<RelicModel>();if(hbr.HasValue)
        {
            var manual=Blank<ElectronicSoldierManual>();Set(manual,"<IsMutable>k__BackingField",true);Set(manual,"_owner",player);
            typeof(ElectronicSoldierManual).GetProperty("HbrBattleProgressData",F)!.SetValue(manual,JsonSerializer.Serialize(new{Schema=1,Enabled=hbr.Value,HealthScaleApplied=true,Level=1}));relics.Add(manual);
        }
        Set(player,"_relics",relics);return creature;
    }
    static float Width(string name,Creature creature,float expected,float reference=80f,float referenceWidth=175f)
    {
        var bar=Blank<NHealthBar>();Set(bar,"_creature",creature);int hp=creature.CurrentHp,max=creature.MaxHp;Captured=default;Reads=Writes=0;
        try{Target.Invoke(bar,[reference,referenceWidth]);}catch(TargetInvocationException failure)when(failure.InnerException!=null){ExceptionDispatchInfo.Capture(failure.InnerException).Throw();}
        Cases.Add(new{name,hp,max,reference,referenceWidth,width=Captured.X,height=Captured.Y,Reads,Writes});
        Near(Captured.X,expected,name+" executes expected native width arithmetic");Near(Captured.Y,ExistingSize.Y,name+" preserves existing native height");
        Check(Reads==1&&Writes==1,name+" executes native body with exactly the two isolated size leaves");Check(creature.CurrentHp==hp&&creature.MaxHp==max,name+" does not mutate actual current or maximum HP");return Captured.X;
    }
    static void WidthCases()
    {
        Creature hbr=PlayerActor(8000,1,true);float first=Width("HBR scaled8000 matches old80 width",hbr,175f);
        Set(hbr,"_maxHp",16000);float grown=Width("HBR maximumHP doubled",hbr,350f);Near(grown,first*2f,"native healthbar width grows at the original pre-scale slope");
        Width("repeated native refresh does not compound normalization",hbr,350f);Set(hbr,"_currentHp",8000);Width("current HP does not determine maximum-based width",hbr,350f);
        Width("HBR max3000 matches old30 width",PlayerActor(3000,1,true),65.625f);
        Width("legacy manual disabled remains native",PlayerActor(80,70,false),175f);Width("vanilla without manual remains native",PlayerActor(160,70,null),350f);
        Width("legacy large maximum is not silently rescaled",PlayerActor(8000,70,false),17500f);
        var monster=Blank<Creature>();Set(monster,"_powers",new List<PowerModel>());Set(monster,"_maxHp",8000);Set(monster,"_currentHp",7000);Width("monster remains native relative width",monster,17500f);
        var pet=Blank<Creature>();Set(pet,"_powers",new List<PowerModel>());Set(pet,"_maxHp",8000);Set(pet,"_currentHp",7000);Set(pet,"_petOwner",hbr.Player);Width("HBR-owned pet without actual Player remains native",pet,17500f);
        Width("alternate reference1000 width300",PlayerActor(100000,50000,true),300f,1000f,300f);Width("alternate reference1000 doubled",PlayerActor(200000,50000,true),600f,1000f,300f);
    }
    static void CallerMetadata()
    {
        MethodInfo caller=NativeType("NMultiplayerPlayerState").GetMethod("UpdateHealthBarWidth",F)!;
        var instructions=PatchProcessor.GetOriginalInstructions(caller).ToArray();
        Check(instructions.Count(instruction=>Equals(instruction.operand,Target))==1&&instructions.Any(instruction=>instruction.opcode==OpCodes.Ldc_R4&&Equals(instruction.operand,80f))&&instructions.Any(instruction=>instruction.opcode==OpCodes.Ldc_R4&&Equals(instruction.operand,175f)),"native multiplayer caller retains its original80HP and175width reference constants");
        Type hud=Mod.GetType("HBRGOSakiMod.Core.Nodes.Screens.NTopBarHealthHud",true)!;
        Check(hud.GetConstructors(F).Any(constructor=>PatchProcessor.GetOriginalInstructions(constructor).Any(instruction=>instruction.opcode==OpCodes.Ldc_R4&&Equals(instruction.operand,250f))),"topbar HUD metadata retains existing fixed250 width");
        Check(hud.GetMethods(F|BindingFlags.DeclaredOnly).Where(method=>method.GetMethodBody()!=null).All(method=>!PatchProcessor.GetOriginalInstructions(method).Any(instruction=>Equals(instruction.operand,Target))),"topbar fixed-width metadata never calls the normalized relative-width method");
    }
    public static void Run(string report)
    {
        bool ok=false;string? error=null;try{Install();WidthCases();CallerMetadata();ok=true;}catch(Exception failure){error=failure.ToString();Console.Error.WriteLine(failure);}
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);File.WriteAllText(report,JsonSerializer.Serialize(new{ok,checks=Passed.Count,passed=Passed,error,cases=Cases,
            modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),nativeSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NHealthBar).Assembly.Location))),
            runtimeProfile=System.Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),sourceNativeSha256=System.Environment.GetEnvironmentVariable("HBR_NATIVE_SOURCE_SHA256"),telemetryIsolation=System.Environment.GetEnvironmentVariable("HBR_TELEMETRY_ISOLATION"),
            boundary="Real native NHealthBar relative-width arithmetic body and production prefix execute against mutable Player/Creature/manual state. Only Control.Size read and private SetSize calls within that body are replaced with managed read/capture leaves; NHealthBar carrier is uninitialized and its finalizer suppressed, no Godot node constructor/_Ready/native binding/renderer runs. Multiplayer caller/topbar paths checked by IL metadata only. No rendered layout, HP text/fill animation, full event UI, game session, package or installed-file changes."},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine((ok?"PASS ":"FAIL ")+Passed.Count+" native healthbar width checks");if(!ok)throw new Exception("Inspect "+report);
    }
}
