using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

static partial class EffectChecks
{
    static Task? ObservedCanonicalDamageTask;
    static void ObserveCanonicalDamageTask(Task<IEnumerable<DamageResult>> __result)=>ObservedCanonicalDamageTask=__result;
    static async Task DamageCallsiteCatalogCases()
    {
        Type patch=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.ThirtyOneBDamageCallsitePatch",true)!;
        Type compatibility=Mod.GetType("HBRGOSakiMod.Core.Compatibility.NativeDamageApiCompatibility",true)!;
        var command=(MethodInfo)compatibility.GetField("Command",F)!.GetValue(null)!;
        object catalog=patch.GetProperty("Coverage",F)!.GetValue(null)!;
        T Property<T>(object value,string name)=>(T)value.GetType().GetProperty(name,F)!.GetValue(value)!;
        string[] generic=Property<string[]>(catalog,"GenericCallers"),reviews=Property<string[]>(catalog,"Reviews"),excluded=Property<string[]>(catalog,"IntentionalForwarders");
        object[] sites=((System.Collections.IEnumerable)Property<object>(catalog,"Sites")).Cast<object>().ToArray();
        object[] owned=((System.Collections.IEnumerable)Property<object>(catalog,"SkillWrapperSites")).Cast<object>().ToArray();
        MethodBase[] targets=((IEnumerable<MethodBase>)patch.GetMethod("TargetMethods",F)!.Invoke(null,null)!).ToArray();
        Check(generic.Length==0&&reviews.Length==0,"canonical31B Damage catalog contains no hidden generic caller or unreviewed IL source");
        Check(sites.Length>0&&targets.Length==sites.Length&&targets.Distinct().Count()==targets.Length,"canonical31B Damage scan targets every discovered ordinary caller exactly once");
        string[] scannedAssemblies=Property<string[]>(catalog,"ScannedAssemblies");
        Check(scannedAssemblies.Contains(typeof(CreatureCmd).Assembly.GetName().Name!)&&scannedAssemblies.Contains(Mod.GetName().Name!)&&targets.Any(method=>method.DeclaringType!.Assembly==typeof(CreatureCmd).Assembly),"canonical31B Damage catalog scans both native and mod assemblies and finds native ordinary callers");
        Check(targets.All(method=>!method.ContainsGenericParameters&&method!=command&&!method.DeclaringType!.FullName!.StartsWith(patch.FullName!,StringComparison.Ordinal)),"the native Damage factory and the complete generic adapter family remain outside the detour set");
        Check(excluded.Length>0&&excluded.All(name=>name.StartsWith(patch.FullName!,StringComparison.Ordinal)),"all intentionally excluded forwarders belong to the adapter's own ordinary/generic/async cache family");
        var rows=new List<object>();
        var ownedRows=new List<object>();
        foreach(object site in owned)
        {
            MethodBase caller=Property<MethodBase>(site,"Caller");MethodInfo[] commands=Property<MethodInfo[]>(site,"Commands");
            Check(!targets.Contains(caller)&&commands.Length>0,"the established raw-skill wrapper exclusively owns its canonical operand: "+caller.DeclaringType?.FullName+"."+caller.Name);
            ownedRows.Add(new{caller=caller.DeclaringType?.FullName+"."+caller.Name,calls=commands.Length,owner="HbrNativeEffectSkillDamagePatch.CreateWrapper central prepared invocation"});
        }
        foreach(object site in sites)
        {
            MethodBase caller=Property<MethodBase>(site,"Caller");MethodInfo[] commands=Property<MethodInfo[]>(site,"Commands");
            Check(commands.Length>0&&commands.All(method=>method.Module==command.Module&&method.MetadataToken==command.MetadataToken),"catalog operands resolve to the one exact canonical Damage overload: "+caller.DeclaringType?.FullName+"."+caller.Name);
            CodeInstruction[] original=PatchProcessor.GetOriginalInstructions(caller).ToArray();
            CodeInstruction[] rewritten=((IEnumerable<CodeInstruction>)patch.GetMethod("Transpiler",F)!.Invoke(null,[original,caller])!).ToArray();
            var substitutions=rewritten.Where(instruction=>instruction.operand is MethodInfo method&&method.DeclaringType==patch&&method.Name is "DamageSix" or "DamageEight").ToArray();
            Check(rewritten.Length==original.Length&&substitutions.Length==commands.Length,"production transpiler changes only the expected number of call operands: "+caller.DeclaringType?.FullName+"."+caller.Name);
            foreach(var instruction in substitutions)
            {
                var adapter=(MethodInfo)instruction.operand;
                Check(adapter.IsGenericMethod&&!adapter.ContainsGenericParameters&&adapter.ReturnType==command.ReturnType&&adapter.GetParameters().Select(parameter=>parameter.ParameterType).SequenceEqual(command.GetParameters().Select(parameter=>parameter.ParameterType)),"the closed ordinary-C# adapter preserves the complete native Task/parameter signature: "+caller.DeclaringType?.FullName+"."+caller.Name);
            }
            bool untouched=true;
            for(int index=0;index<original.Length;index++)
            {
                if(original[index].operand is MethodInfo called&&called.Module==command.Module&&called.MetadataToken==command.MetadataToken)
                    untouched&=rewritten[index].labels.SequenceEqual(original[index].labels)&&rewritten[index].blocks.SequenceEqual(original[index].blocks);
                else untouched&=ReferenceEquals(rewritten[index],original[index]);
            }
            Check(untouched,"production callsite rewrite preserves all non-Damage instructions, labels and exception boundaries: "+caller.DeclaringType?.FullName+"."+caller.Name);
            rows.Add(new{caller=caller.DeclaringType?.FullName+"."+caller.Name,assembly=caller.DeclaringType?.Assembly.GetName().Name,calls=commands.Length,adapter=string.Join(";",substitutions.Select(instruction=>instruction.operand.ToString()))});
        }
        Type bridge=Mod.GetType("HBRGOSakiMod.Core.Compatibility.CreatureCommandCompatibility",true)!;
        MethodInfo bridgeEntry=bridge.GetMethod("InvokeDamage",F)!,bridgeAwait=bridge.GetMethod("PrepareThenInvokeDamage",F)!;
        CodeInstruction[] bridgeIl=PatchProcessor.GetOriginalInstructions(bridgeEntry).ToArray();
        Check(bridgeIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="get_HasPending"&&method.DeclaringType==Helper("ThirtyOneBEnemyActionHelper"))&&bridgeIl.Any(instruction=>Equals(instruction.operand,bridgeAwait)),"the explicit reflection bridge gates its own ordinary async pre-hit path behind HasPending");
        Type bridgeState=bridgeAwait.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
        CodeInstruction[] slowIl=PatchProcessor.GetOriginalInstructions(bridgeState.GetMethod("MoveNext",F)!).ToArray();
        Check(slowIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="PrepareDamageTargets")&&slowIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="Invoke"&&method.DeclaringType?.Name=="CompatibilityReflection"),"the reflection bridge prepares requested Creature targets then calls its original compatibility native invocation");
        Check(!slowIl.Any(instruction=>Equals(instruction.operand,bridgeEntry)),"the reflection bridge async path never recursively invokes its own patched entry");
        Type skillWrapper=Mod.GetType("HBRGOSakiMod.Core.Patches.Combat.HbrNativeEffectSkillDamagePatch",true)!;
        MethodInfo prepared=skillWrapper.GetMethod("InvokePrepared",F)!;
        CodeInstruction[] preparedIl=PatchProcessor.GetOriginalInstructions(prepared).ToArray();
        Check(preparedIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="get_HasPending")&&preparedIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="PrepareThenInvoke"),"every exclusively owned raw-skill wrapper has a central HasPending preparation gate");
        Type skillState=skillWrapper.GetMethod("PrepareThenInvoke",F)!.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
        CodeInstruction[] skillIl=PatchProcessor.GetOriginalInstructions(skillState.GetMethod("MoveNext",F)!).ToArray();
        Check(skillIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="PrepareDamage")&&skillIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="PrepareDamageTargets"),"the central raw-skill wrapper covers both canonical target records and Creature compatibility targets before invocation");
        Type runState=skillWrapper.GetMethod("Run",F)!.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>()!.StateMachineType;
        CodeInstruction[] runIl=PatchProcessor.GetOriginalInstructions(runState.GetMethod("MoveNext",F)!).ToArray();
        Check(runIl.Any(instruction=>Equals(instruction.operand,prepared))&&!runIl.Any(instruction=>instruction.operand is MethodInfo method&&method.Name=="Invoke"&&method.DeclaringType?.IsGenericType==true&&method.DeclaringType.GetGenericTypeDefinition()==typeof(Func<,>)&&method.GetParameters() is{Length:1} parameters&&parameters[0].ParameterType==typeof(object[])),"all raw-skill wrapper Run branches delegate through preparation rather than directly bypassing the excluded callsite");
        await DamageFastTaskCase(patch,command);
        File.WriteAllText(Path.Combine(ReportDirectory,"damage-callsite-catalog.json"),JsonSerializer.Serialize(new{modSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Mod.Location))),runtimeProfile=Environment.GetEnvironmentVariable("HBR_NATIVE_PROFILE"),canonical=command.ToString(),scannedAssemblies,scannedMethods=Property<int>(catalog,"ScannedMethods"),scanMilliseconds=Property<double>(catalog,"ScanMilliseconds"),genericCallers=generic,reviews,excludedAdapterMethods=excluded,skillWrapperOwnedSites=ownedRows,sites=rows,explicitReflectiveBridge=bridgeEntry.ToString(),boundary="Ordinary native and this-mod canonical call operands plus the explicitly prepared compatibility reflection bridge; existing raw-skill wrappers retain exclusive operand ownership and prepare in their central invocation. Unknown third-party direct canonical reflection/invocation is outside this catalog."},new JsonSerializerOptions{WriteIndented=true}));
    }
    static async Task DamageFastTaskCase(Type patch,MethodInfo command)
    {
        BondReset();Check(!(bool)Helper("ThirtyOneBEnemyActionHelper").GetProperty("HasPending",F)!.GetValue(null)!,"the ordinary Damage adapter fast path has no pending pre-hit effect");
        ParameterInfo[] parameters=command.GetParameters();Type targetElement=parameters[1].ParameterType.GetGenericArguments().Single();
        var adapter=patch.GetMethod(parameters.Length==6?"DamageSix":"DamageEight",F)!.MakeGenericMethod(targetElement);
        var h=new Harmony(ProductionId+".bond-damage-task-identity");
        h.Patch(command,postfix:new HarmonyMethod(typeof(EffectChecks).GetMethod(nameof(ObserveCanonicalDamageTask),F)!){priority=Priority.Last});
        try
        {
            object?[] arguments=parameters.Select(parameter=>parameter.Position switch
            {
                0=>(object)new ThrowingPlayerChoiceContext(),1=>ProfileTargets(parameter.ParameterType,Enemy),2=>1m,3=>ValueProp.Unpowered,4=>Hero,
                _=>parameter.ParameterType==typeof(ModifyDamageHookType)?(object)ModifyDamageHookType.All:parameter.HasDefaultValue?parameter.DefaultValue:null
            }).ToArray();
            ObservedCanonicalDamageTask=null;
            Task returned=(Task)adapter.Invoke(null,arguments)!;
            Check(ReferenceEquals(returned,ObservedCanonicalDamageTask),"the no-pending strongly typed delegate returns the native factory's exact original Task object");
            await returned;Check(Hits.Count==1,"the cached typed delegate executes one actual native Damage command without recursive wrapper entry");
        }
        finally{h.UnpatchAll(h.Id);ObservedCanonicalDamageTask=null;}
    }
}
