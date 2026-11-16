using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrNativeFixedResourcePatch
{
    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<MethodInfo, MethodInfo> Readers = CreateReaders();

    private static Dictionary<MethodInfo, MethodInfo> CreateReaders()
    {
        Dictionary<MethodInfo, MethodInfo> readers = new()
        {
        [AccessTools.PropertyGetter(typeof(DynamicVar), nameof(DynamicVar.BaseValue))] =
            AccessTools.Method(typeof(HbrNativeFixedResourceHelper), "ReadBase"),
        [AccessTools.PropertyGetter(typeof(DynamicVar), nameof(DynamicVar.IntValue))] =
            AccessTools.Method(typeof(HbrNativeFixedResourceHelper), "ReadInt"),
        [AccessTools.PropertyGetter(typeof(DynamicVar), nameof(DynamicVar.PreviewValue))] =
            AccessTools.Method(typeof(HbrNativeFixedResourceHelper), "ReadPreview"),
        [AccessTools.PropertyGetter(typeof(DynamicVar), nameof(DynamicVar.EnchantedValue))] =
            AccessTools.Method(typeof(HbrNativeFixedResourceHelper), "ReadEnchanted")
        };
        if (AccessTools.PropertyGetter(typeof(DynamicVar), "ShownValue") is { } shown)
            readers[shown] = AccessTools.Method(typeof(HbrNativeFixedResourceHelper), "ReadShown");
        return readers;
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Discover call sites once at patch installation. Never inspect stacks,
        // reflect methods, or scan registries per damage/health command.
        foreach (Type model in HbrNativeFixedResourceHelper.ModelTypes)
        foreach (Type type in WithNestedTypes(model))
        foreach (MethodInfo method in type.GetMethods(Declared))
        {
            if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
            if (method.Name is "CalculateVars" or "OnUpgrade" or "AfterDowngraded") continue;
            if (PatchProcessor.GetOriginalInstructions(method).Any(instruction =>
                    instruction.operand is MethodInfo call && Readers.ContainsKey(call)))
                yield return method;
        }

        // These methods format values only. Raw BaseValue, UpgradeValueBy,
        // ResetToBase, Clone, serialization and setters are deliberately intact.
        yield return AccessTools.Method(typeof(DynamicVar), nameof(DynamicVar.ToHighlightedString));
        yield return AccessTools.Method(typeof(DynamicVar), nameof(DynamicVar.ToString), Type.EmptyTypes);
        if (AccessTools.Method(typeof(DynamicVar), "GetBaseValueForIConvertible") is { } convertible)
            yield return convertible;
        if (AccessTools.PropertyGetter(typeof(DynamicVar), "ShownValue") is { } shown)
            yield return shown;

        // Burn and similar status cards pass DamageVar itself to this adapter;
        // their model method never reads BaseValue. The owner/name guard keeps
        // ordinary attacks on the separate skill-power calculation route.
        foreach (MethodInfo method in typeof(CreatureCmd).GetMethods(Declared))
            if (method.Name == nameof(CreatureCmd.Damage) &&
                method.GetParameters().Any(parameter => parameter.ParameterType == typeof(DamageVar)))
            {
                Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
                MethodInfo body = state == null ? method : AccessTools.Method(state, "MoveNext");
                if (PatchProcessor.GetOriginalInstructions(body).Any(instruction =>
                    instruction.operand is MethodInfo call && Readers.ContainsKey(call)))
                    yield return body;
            }
    }

    private static IEnumerable<Type> WithNestedTypes(Type type)
    {
        yield return type;
        foreach (Type nested in type.GetNestedTypes(Declared))
        foreach (Type child in WithNestedTypes(nested))
            yield return child;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeInstruction[] body = instructions.ToArray();
        for (int index = 0; index < body.Length; index++)
        {
            CodeInstruction instruction = body[index];
            if (instruction.operand is MethodInfo call && Readers.TryGetValue(call, out MethodInfo replacement) &&
                !IsRawStorageRead(body, index))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }

    private static bool IsRawStorageRead(CodeInstruction[] body, int index)
    {
        // Native AbyssalBaths increments Damage.BaseValue after each immerse:
        // var; dup; get_BaseValue; Decimal.One; op_Addition; set_BaseValue.
        // Keep that raw mutation read intact while scaling actual commands and
        // text. Do not infer semantics from arbitrary branches or long ranges.
        if (index == 0 || body[index - 1].opcode != OpCodes.Dup) return false;
        for (int next = index + 1; next < Math.Min(body.Length, index + 7); next++)
        {
            CodeInstruction instruction = body[next];
            if (instruction.opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or FlowControl.Return)
                return false;
            if (instruction.operand is not MethodInfo method) continue;
            if (method.DeclaringType == typeof(DynamicVar))
                return method.Name is "set_BaseValue" or "set_PreviewValue" or "set_EnchantedValue";
            if (method.DeclaringType != typeof(decimal)) return false;
        }
        return false;
    }
}
