using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Keep the native async Damage factory intact. A normal C# call-site adapter
// can await an enemy action's queued pre-hit effects before starting that
// factory, without a Harmony Prefix invoking its own patched entry again.
[HarmonyPatch]
internal static class ThirtyOneBDamageCallsitePatch
{
    internal sealed record Site(MethodBase Caller, MethodInfo[] Commands);
    internal sealed record Catalog(Site[] Sites, Site[] SkillWrapperSites, string[] GenericCallers, string[] Reviews,
        string[] IntentionalForwarders, string[] ScannedAssemblies, int ScannedMethods, double ScanMilliseconds);

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private static readonly MethodInfo Native = NativeDamageApiCompatibility.Command;
    private static readonly Type TargetElement = Native.GetParameters()[1].ParameterType.GetGenericArguments().Single();
    private static readonly MethodInfo Adapter = typeof(ThirtyOneBDamageCallsitePatch).GetMethod(
        Native.GetParameters().Length == 6 ? nameof(DamageSix) : nameof(DamageEight),
        BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(TargetElement);
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(code => unchecked((ushort)code.Value));
    private static readonly Lazy<Catalog> CachedCatalog = new(BuildCatalog);
    internal static Catalog Coverage => CachedCatalog.Value;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        Catalog catalog = Coverage;
        if (catalog.GenericCallers.Length != 0 || catalog.Reviews.Length != 0)
            throw new InvalidOperationException("Uncovered 31B damage call sites: " +
                string.Join("; ", catalog.GenericCallers.Concat(catalog.Reviews)));
        ValidateSignature();
        return catalog.Sites.Select(site => site.Caller);
    }

    private static void ValidateSignature()
    {
        if (Native.GetParameters().Length is not (6 or 8) || Adapter.ReturnType != Native.ReturnType ||
            !Adapter.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(Native.GetParameters().Select(parameter => parameter.ParameterType)))
            throw new InvalidOperationException("31B damage adapter changed the canonical native signature.");
    }

    private static Catalog BuildCatalog()
    {
        Stopwatch clock = Stopwatch.StartNew();
        List<Site> sites = new(), owned = new(); List<string> generic = new(), reviews = new(), forwards = new();
        HashSet<MethodBase> skillWrappers = SkillWrapperCallers();
        Assembly[] assemblies = { typeof(CreatureCmd).Assembly, typeof(ThirtyOneBDamageCallsitePatch).Assembly };
        int scanned = 0;
        foreach (Assembly assembly in assemblies.Distinct())
        foreach (Type type in assembly.GetTypes())
        foreach (MethodBase method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).Distinct())
        {
            if (method.IsAbstract) continue;
            try
            {
                if (method.GetMethodBody() == null) continue;
                scanned++;
                // Exclude our complete generic adapter family, including its
                // compiler-created async states and delegate cache types.
                if (IsAdapterType(type)) { forwards.Add(type.FullName + "." + method.Name); continue; }
                MethodInfo[] commands = ReadCalls(method).Where(IsNative).ToArray();
                if (commands.Length == 0) continue;
                // These callers already replace the same operand with the
                // established raw-skill wrapper. Its central invocation owns
                // pending preparation; two transpilers cannot own that call.
                if (skillWrappers.Contains(method)) { owned.Add(new(method, commands)); continue; }
                string name = type.FullName + "." + method.Name;
                if (method.ContainsGenericParameters) { generic.Add(name); continue; }
                if (method == Native) { reviews.Add(name + ": native factory must remain untouched"); continue; }
                sites.Add(new(method, commands));
            }
            catch (Exception error) when (error is ArgumentException or BadImageFormatException or NotSupportedException or TypeLoadException)
            { reviews.Add(type.FullName + "." + method.Name + ": " + error.GetType().Name + " " + error.Message); }
        }
        // Ritsu's BeforeAttackHit may change targets, damage and provenance.
        // Prepare at its two actual Damage leaves, after that callback, rather
        // than wrapping the complete per-hit operation before it can retarget.
        MethodBase[] ritsuCallers = RitsuAttackHitDamageCompatibility.GetCallers(Native);
        foreach (MethodBase caller in ritsuCallers)
        {
            MethodInfo[] commands = ReadCalls(caller).Where(IsNative).ToArray();
            if (caller.ContainsGenericParameters || commands.Length != 1)
                throw new InvalidOperationException("Ritsu per-hit damage leaf changed: " + caller);
            sites.Add(new(caller, commands));
            scanned++;
        }
        clock.Stop();
        return new(sites.ToArray(), owned.ToArray(), generic.ToArray(), reviews.ToArray(), forwards.ToArray(),
            assemblies.Concat(ritsuCallers.Select(caller => caller.Module.Assembly)).Distinct()
                .Select(assembly => assembly.GetName().Name).ToArray(), scanned, clock.Elapsed.TotalMilliseconds);
    }

    private static HashSet<MethodBase> SkillWrapperCallers()
    {
        HashSet<MethodBase> callers = new();
        foreach (Type patch in new[] { typeof(HbrNativeEffectSkillDamagePatch), typeof(HbrNativeProfiledDamagePatch),
            typeof(HbrNativeRelicEffectSkillDamagePatch), typeof(HbrNativePotionEffectSkillDamagePatch),
            typeof(HbrNativeThornsDamagePatch), typeof(HbrNativeGalvanicDamagePatch), typeof(HbrNativeDamageOverTimePatch) })
        {
            MethodInfo selector = patch.GetMethod("TargetMethods", Declared) ?? patch.GetMethod("TargetMethod", Declared)
                ?? throw new MissingMethodException(patch.FullName, "TargetMethod(s)");
            object result = selector.Invoke(null, null);
            if (result is MethodBase single) callers.Add(single);
            else foreach (MethodBase method in (IEnumerable<MethodBase>)result) callers.Add(method);
        }
        return callers;
    }

    private static bool IsAdapterType(Type type)
    {
        for (; type != null; type = type.DeclaringType)
            if (type == typeof(ThirtyOneBDamageCallsitePatch)) return true;
        return false;
    }
    private static bool IsNative(MethodInfo method) => method.Module == Native.Module && method.MetadataToken == Native.MetadataToken;

    private static IEnumerable<MethodInfo> ReadCalls(MethodBase method)
    {
        byte[] bytes = method.GetMethodBody().GetILAsByteArray();
        int offset = 0;
        while (offset < bytes.Length)
        {
            ushort value = bytes[offset++];
            if (value == 0xfe) value = (ushort)(0xfe00 | bytes[offset++]);
            OpCode code = Opcodes[value];
            int size = code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4
            };
            if ((code == OpCodes.Call || code == OpCodes.Callvirt) &&
                method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset), method.DeclaringType?.GetGenericArguments(),
                    method.IsGenericMethod ? method.GetGenericArguments() : null) is MethodInfo called)
                yield return called;
            offset += size;
        }
    }

    // Ritsu's per-hit hook owns the original Execute -> Damage operand at
    // priority Last (0). Retain that wrapper unchanged; its two actual damage
    // leaves are separately prepared after Ritsu's before-hit callbacks.
    [HarmonyAfter("com.ritsukage.sts2-RitsuLib.framework-core")]
    [HarmonyPriority(-100)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        ValidateSignature();
        int expected = Coverage.Sites.Single(site => site.Caller == __originalMethod).Commands.Length;
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                instruction.operand is MethodInfo method && IsNative(method))
            {
                changed++;
                yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = Adapter };
            }
            else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                instruction.operand is MethodInfo hitHook && RitsuAttackHitDamageCompatibility.IsHitHook(hitHook, Native))
            {
                changed++;
                yield return instruction;
            }
            else yield return instruction;
        }
        if (changed != expected)
            throw new InvalidOperationException($"31B damage call count changed in {__originalMethod}: {changed}/{expected}");
    }

    private delegate Task<IEnumerable<DamageResult>> DamageSixCall<TTarget>(PlayerChoiceContext context,
        IEnumerable<TTarget> targets, decimal amount, ValueProp props, Creature dealer, CardModel source);
    private delegate Task<IEnumerable<DamageResult>> DamageEightCall<TTarget>(PlayerChoiceContext context,
        IEnumerable<TTarget> targets, decimal amount, ValueProp props, Creature dealer, CardModel source,
        CardPlay play, ModifyDamageHookType hookType);
    // Only the cache for the active runtime arity is initialized. The other
    // delegate cannot accidentally bind the wrong profile's native signature.
    private static class NativeSix<TTarget>
    {
        internal static readonly DamageSixCall<TTarget> Call = Native.CreateDelegate<DamageSixCall<TTarget>>();
    }
    private static class NativeEight<TTarget>
    {
        internal static readonly DamageEightCall<TTarget> Call = Native.CreateDelegate<DamageEightCall<TTarget>>();
    }

    internal static Task<IEnumerable<DamageResult>> DamageSix<TTarget>(PlayerChoiceContext context,
        IEnumerable<TTarget> targets, decimal amount, ValueProp props, Creature dealer, CardModel source) =>
        ThirtyOneBEnemyActionHelper.HasPending
            ? PrepareSix(context, targets, amount, props, dealer, source)
            : NativeSix<TTarget>.Call(context, targets, amount, props, dealer, source);

    private static async Task<IEnumerable<DamageResult>> PrepareSix<TTarget>(PlayerChoiceContext context,
        IEnumerable<TTarget> targets, decimal amount, ValueProp props, Creature dealer, CardModel source)
    {
        object[] arguments = { context, targets, amount, props, dealer, source };
        await ThirtyOneBEnemyActionHelper.PrepareDamage(arguments);
        return await NativeSix<TTarget>.Call(context, (IEnumerable<TTarget>)arguments[1], amount, props, dealer, source);
    }

    internal static Task<IEnumerable<DamageResult>> DamageEight<TTarget>(PlayerChoiceContext context,
        IEnumerable<TTarget> targets, decimal amount, ValueProp props, Creature dealer, CardModel source,
        CardPlay play, ModifyDamageHookType hookType) => ThirtyOneBEnemyActionHelper.HasPending
            ? PrepareEight(context, targets, amount, props, dealer, source, play, hookType)
            : NativeEight<TTarget>.Call(context, targets, amount, props, dealer, source, play, hookType);

    private static async Task<IEnumerable<DamageResult>> PrepareEight<TTarget>(PlayerChoiceContext context,
        IEnumerable<TTarget> targets, decimal amount, ValueProp props, Creature dealer, CardModel source,
        CardPlay play, ModifyDamageHookType hookType)
    {
        object[] arguments = { context, targets, amount, props, dealer, source, play, hookType };
        await ThirtyOneBEnemyActionHelper.PrepareDamage(arguments);
        return await NativeEight<TTarget>.Call(context, (IEnumerable<TTarget>)arguments[1], amount, props, dealer, source, play, hookType);
    }
}
