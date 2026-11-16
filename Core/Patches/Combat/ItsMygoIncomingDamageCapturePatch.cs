using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class ItsMygoIncomingDamageCapturePatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.MoveNext;

    private static void Prefix(out int __state)
    {
        __state = ItsMygoIncomingDamageCapture.ScopeDepth;
    }

    private static void Finalizer(int __state)
    {
        // No capture scope may survive an exception or an async suspension.
        ItsMygoIncomingDamageCapture.RestoreScopeDepth(__state);
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        FieldInfo resultsField = AccessTools.GetDeclaredFields(__originalMethod.DeclaringType)
            .SingleOrDefault(field => field.FieldType == typeof(List<DamageResult>) &&
                field.Name.StartsWith("<results>", StringComparison.Ordinal)) ??
            throw new MissingFieldException(__originalMethod.DeclaringType?.FullName, "results");
        MethodInfo begin = PrivateAccess.Method(
            typeof(ItsMygoIncomingDamageCapture), nameof(ItsMygoIncomingDamageCapture.Begin));
        MethodInfo end = PrivateAccess.Method(
            typeof(ItsMygoIncomingDamageCapture), nameof(ItsMygoIncomingDamageCapture.End));
        MethodInfo appendResults = PrivateAccess.Method(
            typeof(ItsMygoIncomingDamageCapture), nameof(ItsMygoIncomingDamageCapture.AppendResults));
        MethodInfo addRange = typeof(List<DamageResult>).GetMethod(nameof(List<DamageResult>.AddRange));
        int captureCount = 0;
        int appendCount = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo calledMethod &&
                IsActualModifyDamageCall(calledMethod))
            {
                // The damage call's existing arguments stay on the IL stack.
                // Only the result-list argument is consumed by Begin.
                CodeInstruction loadState = new(OpCodes.Ldarg_0);
                loadState.labels.AddRange(instruction.labels);
                loadState.blocks.AddRange(instruction.blocks);
                yield return loadState;
                yield return new CodeInstruction(OpCodes.Ldfld, resultsField);
                yield return new CodeInstruction(OpCodes.Call, begin);
                CodeInstruction call = new(instruction);
                call.labels.Clear();
                call.blocks.Clear();
                yield return call;
                yield return new CodeInstruction(OpCodes.Call, end);
                captureCount++;
            }
            else if (instruction.Calls(addRange))
            {
                CodeInstruction replacement = new(instruction)
                {
                    opcode = OpCodes.Call,
                    operand = appendResults
                };
                yield return replacement;
                appendCount++;
            }
            else
            {
                yield return instruction;
            }
        }

        if (captureCount != 1 || appendCount != 1)
        {
            throw new InvalidOperationException(
                $"Expected one raw damage capture and one result append; found {captureCount} and {appendCount}.");
        }
    }

    private static bool IsActualModifyDamageCall(MethodInfo method)
    {
        // Both orders of the existing StrongestShield transpiler are supported.
        return method.DeclaringType == typeof(Hook) && method.Name == nameof(Hook.ModifyDamage) ||
            method.DeclaringType == typeof(StrongestShieldDamagePatch) &&
            method.Name == "ModifyDamageWithStrongestShield";
    }
}
