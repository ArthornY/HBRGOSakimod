using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Never detour a native generic entry point. CoreCLR's hidden generic context
// is not a safe Harmony boundary. Only replace calls in ordinary, nongeneric
// native/mod methods with our normally compiled, signature-identical adapter.
[HarmonyPatch]
internal static class ThirtyOneAYukiEnumerableDebuffPatch
{
    internal sealed record Site(MethodBase Caller, MethodInfo[] Commands);
    internal sealed record Catalog(Site[] Sites, string[] GenericCallers, string[] Reviews,
        string[] IntentionalForwarders, int ScannedMethods, double ScanMilliseconds);

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private static readonly MethodInfo NativeEnumerable = typeof(PowerCmd).GetMethods(Declared)
        .Single(method => method.Name == nameof(PowerCmd.Apply) && method.IsGenericMethodDefinition &&
            method.GetParameters() is { Length: 6 } parameters && parameters[1].ParameterType == typeof(IEnumerable<Creature>));
    private static readonly MethodInfo Adapter = typeof(ThirtyOneAYukiEnumerableDebuffPatch)
        .GetMethod(nameof(ApplyEnumerable), BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
        .ToDictionary(code => unchecked((ushort)code.Value));
    private static readonly Lazy<Catalog> CachedCatalog = new(BuildCatalog);
    internal static Catalog Coverage => CachedCatalog.Value;
    internal static int TargetCount => Coverage.Sites.Length;
    internal static int ScannedMethodCount => Coverage.ScannedMethods;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        Catalog catalog = Coverage;
        if (catalog.GenericCallers.Length != 0 || catalog.Reviews.Length != 0)
            throw new InvalidOperationException("Uncovered Yuki enumerable call sites: " +
                string.Join("; ", catalog.GenericCallers.Concat(catalog.Reviews)));
        return catalog.Sites.Select(site => site.Caller);
    }

    private static Catalog BuildCatalog()
    {
        Stopwatch clock = Stopwatch.StartNew();
        List<Site> sites = new(); List<string> generic = new(), reviews = new(), forwards = new();
        int scanned = 0;
        foreach (Assembly assembly in new[] { typeof(PowerCmd).Assembly, typeof(ThirtyOneAYukiEnumerableDebuffPatch).Assembly }.Distinct())
        foreach (Type type in assembly.GetTypes())
        foreach (MethodBase method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).Distinct())
        {
            if (method.IsAbstract) continue;
            try
            {
                if (method.GetMethodBody() == null) continue;
                scanned++;
                MethodInfo[] commands = ReadCalls(method).Where(IsNativeEnumerable).ToArray();
                if (commands.Length == 0) continue;
                string name = type.FullName + "." + method.Name;
                // Both intentional native forwards must stay untouched, or
                // the adapter would call itself recursively after rewriting.
                if (IsAdapterType(type)) { forwards.Add(name); continue; }
                if (method.ContainsGenericParameters) { generic.Add(name); continue; }
                sites.Add(new(method, commands));
            }
            catch (Exception error) when (error is ArgumentException or BadImageFormatException or NotSupportedException or TypeLoadException)
            { reviews.Add(type.FullName + "." + method.Name + ": " + error.GetType().Name + " " + error.Message); }
        }
        clock.Stop();
        return new(sites.ToArray(), generic.ToArray(), reviews.ToArray(), forwards.ToArray(), scanned, clock.Elapsed.TotalMilliseconds);
    }

    private static bool IsAdapterType(Type type)
    {
        for (; type != null; type = type.DeclaringType)
            if (type == typeof(ThirtyOneAYukiEnumerableDebuffPatch)) return true;
        return false;
    }

    private static bool IsNativeEnumerable(MethodInfo method) => method.IsGenericMethod &&
        method.Module == NativeEnumerable.Module && method.GetGenericMethodDefinition().MetadataToken == NativeEnumerable.MetadataToken;

    // Read only method-call operands. Unlike a DynamicMethod IL reader, this
    // also handles unbound generic containers so the coverage review cannot
    // silently skip a hidden generic caller. No model is instantiated.
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

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int expected = Coverage.Sites.Single(site => site.Caller == __originalMethod).Commands.Length;
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && IsNativeEnumerable(method))
            {
                MethodInfo replacement = Adapter.MakeGenericMethod(method.GetGenericArguments());
                if (replacement.ReturnType != method.ReturnType ||
                    !replacement.GetParameters().Select(parameter => parameter.ParameterType)
                        .SequenceEqual(method.GetParameters().Select(parameter => parameter.ParameterType)))
                    throw new InvalidOperationException("Yuki enumerable adapter changed a native call signature.");
                changed++;
                yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
            }
            else yield return instruction;
        }
        if (changed != expected)
            throw new InvalidOperationException($"Yuki enumerable call count changed in {__originalMethod}: {changed}/{expected}");
    }

    internal static Task<IReadOnlyList<T>> ApplyEnumerable<T>(PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets, decimal amount, Creature applier, CardModel cardSource, bool silent = false) where T : PowerModel
    {
        Creature owner = (applier?.Player ?? applier?.PetOwner)?.Creature ?? applier;
        return amount > 0m && owner?.GetPower<ThirtyOneAYukiDebuffPower>()?.Amount > 0
            ? ApplyBatched<T>(choiceContext, targets, amount, applier, cardSource, silent)
            : PowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);
    }

    private static async Task<IReadOnlyList<T>> ApplyBatched<T>(PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets, decimal amount, Creature applier, CardModel cardSource, bool silent) where T : PowerModel
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        return await PowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);
    }
}
