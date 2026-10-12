using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class DamageResultAmountPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo damageMethod = AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
            .Where(method => method.Name == nameof(CreatureCmd.Damage))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length is 6 or 7 &&
                    parameters[0].ParameterType == typeof(PlayerChoiceContext) &&
                    parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == typeof(ValueProp) &&
                    parameters[4].ParameterType == typeof(Creature) &&
                    parameters[5].ParameterType == typeof(CardModel);
            })
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName, nameof(CreatureCmd.Damage));

        AsyncStateMachineAttribute stateMachine =
            damageMethod.GetCustomAttribute<AsyncStateMachineAttribute>() ??
            throw new MissingMethodException("Missing damage command state machine.");
        return AccessTools.Method(stateMachine.StateMachineType, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo decimalToInt = typeof(decimal).GetMethods()
            .Single(method => method.Name == "op_Explicit" &&
                method.ReturnType == typeof(int) &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(decimal));
        MethodInfo safeConversion = AccessTools.Method(
            typeof(DamageResultAmountPatch), nameof(ToResultAmount));
        MethodInfo blockedDamageSetter = AccessTools.PropertySetter(
            typeof(DamageResult), nameof(DamageResult.BlockedDamage));
        MethodInfo exactBlockedDamageSetter = AccessTools.Method(
            typeof(CardDamageHistoryHelper), nameof(CardDamageHistoryHelper.SetBlockedDamage));
        List<CodeInstruction> result = instructions.ToList();
        int replacements = 0;
        int blockedDamageConversions = 0;
        int unblockedDamageZeroChecks = 0;
        for (int index = 0; index < result.Count; index++)
        {
            CodeInstruction instruction = result[index];
            if (!instruction.Calls(decimalToInt)) continue;

            if (index == 0 || index + 1 >= result.Count ||
                result[index - 1].opcode != OpCodes.Ldfld ||
                result[index - 1].operand is not FieldInfo field ||
                field.FieldType != typeof(decimal))
                throw new InvalidOperationException(
                    $"Unexpected damage-result integer conversion input at instruction {index}.");

            if (field.Name.StartsWith("<blockedDamage>", StringComparison.Ordinal) &&
                result[index + 1].Calls(blockedDamageSetter))
            {
                blockedDamageConversions++;
                // Keep the decimal on the stack and let the setter preserve its
                // exact value for presentation before the native Int32 projection.
                result[index] = new CodeInstruction(instruction) { opcode = OpCodes.Nop, operand = null };
                result[index + 1] = new CodeInstruction(result[index + 1])
                {
                    opcode = OpCodes.Call,
                    operand = exactBlockedDamageSetter
                };
            }
            else if (field.Name.StartsWith("<unblockedDamage>", StringComparison.Ordinal) &&
                index + 2 < result.Count &&
                result[index + 1].opcode == OpCodes.Ldc_I4_0 &&
                result[index + 2].opcode == OpCodes.Ceq)
            {
                unblockedDamageZeroChecks++;
            }
            else
                throw new InvalidOperationException(
                    $"Unexpected damage-result integer conversion context at instruction {index}.");

            replacements++;
            // Copy the original opcode, labels and exception boundaries; only
            // the called conversion changes, leaving decimal calculations intact.
            if (result[index].opcode != OpCodes.Nop)
                result[index] = new CodeInstruction(instruction) { operand = safeConversion };
        }
        // One conversion checks whether HP damage truncates to zero, and two
        // populate DamageResult.BlockedDamage (normal and redirected targets).
        if (replacements != 3 || blockedDamageConversions != 2 ||
            unblockedDamageZeroChecks != 1)
            throw new InvalidOperationException(
                $"Expected two BlockedDamage conversions and one unblocked-damage zero check; " +
                $"found {blockedDamageConversions}, {unblockedDamageZeroChecks} " +
                $"({replacements} total).");
        return result;
    }

    // A protected DP layer can intercept an arbitrarily large decimal hit.
    // Saturate only the integer result; keep all damage/absorption math intact.
    private static int ToResultAmount(decimal amount) =>
        (int)Math.Clamp(amount, int.MinValue, int.MaxValue);
}
