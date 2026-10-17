using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.BaseValue), MethodType.Getter)]
internal static class HotaruMyHappinessBaseValuePatch
{
    private static void Postfix(DynamicVar __instance, ref decimal __result) =>
        __result = HotaruMyHappinessCardValueHelper.BaseValue(__instance, __result);
}

[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.EnchantedValue), MethodType.Getter)]
internal static class HotaruMyHappinessEnchantedValuePatch
{
    private static void Postfix(DynamicVar __instance, ref decimal __result) =>
        __result = HotaruMyHappinessCardValueHelper.EnchantedValue(__instance, __result);
}

[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.PreviewValue), MethodType.Getter)]
internal static class HotaruMyHappinessPreviewValuePatch
{
    private static void Postfix(DynamicVar __instance, ref decimal __result) =>
        __result = HotaruMyHappinessCardValueHelper.PreviewValue(__instance, __result);
}

[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.EnchantedValue), MethodType.Setter)]
internal static class HotaruMyHappinessEnchantedWritePatch
{
    private static void Postfix(DynamicVar __instance) =>
        HotaruMyHappinessCardValueHelper.RecordEnchantedWrite(__instance);
}

[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.PreviewValue), MethodType.Setter)]
internal static class HotaruMyHappinessPreviewWritePatch
{
    private static void Postfix(DynamicVar __instance) =>
        HotaruMyHappinessCardValueHelper.RecordPreviewWrite(__instance);
}

[HarmonyPatch]
internal static class HotaruMyHappinessRawCardValuePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertySetter(typeof(DynamicVar), nameof(DynamicVar.BaseValue));
        yield return AccessTools.Method(typeof(DynamicVar), nameof(DynamicVar.UpgradeValueBy));
        yield return AccessTools.Method(typeof(DynamicVar), nameof(DynamicVar.Clone));
        yield return AccessTools.Method(typeof(DynamicVarSet), nameof(DynamicVarSet.RecalculateForUpgradeOrEnchant));
        yield return AccessTools.Method(typeof(CalculatedVar), nameof(CalculatedVar.SetOwner));
        yield return AccessTools.Method(typeof(CalculatedVar), nameof(CalculatedVar.RecalculateForUpgradeOrEnchant));
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.UpgradeInternal));
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.DowngradeInternal));
        yield return AccessTools.Method(typeof(CardModel), "DeepCloneFields");
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.ToSerializable));
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.FromSerializable));
        yield return AccessTools.Method(typeof(EnchantmentModel), nameof(EnchantmentModel.ModifyCard));
    }

    private static void Prefix(out HotaruMyHappinessCardValueHelper.Scope __state) =>
        __state = HotaruMyHappinessCardValueHelper.ReadRawValues();

    private static Exception Finalizer(Exception __exception, HotaruMyHappinessCardValueHelper.Scope __state)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch]
internal static class HotaruMyHappinessCardValueReadModifyWritePatch
{
    private static readonly MethodInfo BaseGetter =
        AccessTools.PropertyGetter(typeof(DynamicVar), nameof(DynamicVar.BaseValue));
    private static readonly MethodInfo BaseSetter =
        AccessTools.PropertySetter(typeof(DynamicVar), nameof(DynamicVar.BaseValue));
    private static readonly MethodInfo RawGetter = AccessTools.Method(
        typeof(HotaruMyHappinessCardValueHelper), nameof(HotaruMyHappinessCardValueHelper.ReadStoredBaseValue));

    private static IEnumerable<MethodBase> TargetMethods() =>
        new[] { typeof(CardModel).Assembly, typeof(HotaruMyHappinessCardValueReadModifyWritePatch).Assembly }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(CardModel).IsAssignableFrom(type))
            .SelectMany(DeclaredAndNestedMethods)
            .Distinct()
            .Where(method => !method.ContainsGenericParameters && method.GetMethodBody() != null)
            .Where(method => WriteReads(PatchProcessor.GetOriginalInstructions(method)).Any());

    private static IEnumerable<MethodInfo> DeclaredAndNestedMethods(Type type) =>
        AccessTools.GetDeclaredMethods(type).Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(DeclaredAndNestedMethods));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> result = instructions.ToList();
        foreach (int index in WriteReads(result))
            result[index] = new CodeInstruction(result[index]) { opcode = OpCodes.Call, operand = RawGetter };
        return result;
    }

    private static IEnumerable<int> WriteReads(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions as List<CodeInstruction> ?? instructions.ToList();
        for (int index = 1; index < code.Count; index++)
        {
            // C# emits dup/get/.../set for += and similar updates. Prove that
            // the duplicated receiver survives until this exact property's
            // setter; reads of other inputs (such as Rampage's Increase) stay
            // boosted. Branches or an uncertain stack shape fail closed.
            if (code[index - 1].opcode != OpCodes.Dup || !code[index].Calls(BaseGetter) ||
                code[index].labels.Count > 0 || code[index].blocks.Count > 0) continue;
            int depth = 2; // preserved DynamicVar receiver, then its old decimal
            for (int next = index + 1; next < code.Count; next++)
            {
                CodeInstruction instruction = code[next];
                if (instruction.labels.Count > 0 || instruction.blocks.Count > 0) break;
                if (instruction.Calls(BaseSetter) && depth == 2)
                {
                    yield return index;
                    break;
                }
                if (instruction.opcode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch or
                    FlowControl.Return or FlowControl.Throw ||
                    !TryStackChange(instruction, out int pop, out int push) || pop >= depth) break;
                depth += push - pop;
            }
        }
    }

    private static bool TryStackChange(CodeInstruction instruction, out int pop, out int push)
    {
        MethodBase method = instruction.operand as MethodBase;
        pop = instruction.opcode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or
                StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or
                StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_pop1 or
                StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or
                StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or
                StackBehaviour.Popref_popi_popref => 3,
            StackBehaviour.Varpop when method != null => method.GetParameters().Length +
                (instruction.opcode == OpCodes.Newobj || method.IsStatic ? 0 : 1),
            _ => -1
        };
        push = instruction.opcode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 or StackBehaviour.Pushi or StackBehaviour.Pushi8 or StackBehaviour.Pushr4 or
                StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Varpush when instruction.opcode == OpCodes.Newobj => 1,
            StackBehaviour.Varpush when method is MethodInfo info => info.ReturnType == typeof(void) ? 0 : 1,
            _ => -1
        };
        return pop >= 0 && push >= 0;
    }
}

[HarmonyPatch]
internal static class HotaruMyHappinessDisplayCalculationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DynamicVar), nameof(DynamicVar.ResetToBase));
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.UpdateDynamicVarPreview));
    }

    private static void Prefix(out HotaruMyHappinessCardValueHelper.Scope __state) =>
        __state = HotaruMyHappinessCardValueHelper.CalculateDisplayValues();

    private static Exception Finalizer(Exception __exception, HotaruMyHappinessCardValueHelper.Scope __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
