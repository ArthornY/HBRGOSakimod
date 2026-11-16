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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class DamageResultAmountPatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.MoveNext;

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
        int intendedHpLossConversions = 0;
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
            else if (NativeDamageApiCompatibility.UsesTargetData &&
                field.Name.StartsWith("<damageRemaining>", StringComparison.Ordinal) &&
                result[index + 1].operand is MethodInfo intendedSetter &&
                intendedSetter.DeclaringType == typeof(DamageResult) &&
                intendedSetter.Name == "set_IntendedHpLoss")
            {
                // 0.113 records the intended pre-HP loss on three paths. This
                // extra integer receipt must saturate just like blocked damage.
                intendedHpLossConversions++;
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
        // The legacy three projections remain. 0.113 adds three independent
        // IntendedHpLoss receipts; validate their exact shape rather than
        // weakening the transpiler to accept an arbitrary conversion count.
        int expectedIntended = NativeDamageApiCompatibility.UsesTargetData ? 3 : 0;
        if (replacements != 3 + expectedIntended || blockedDamageConversions != 2 ||
            unblockedDamageZeroChecks != 1 || intendedHpLossConversions != expectedIntended)
            throw new InvalidOperationException(
                $"Expected two BlockedDamage conversions, one unblocked-damage zero check " +
                $"and {expectedIntended} IntendedHpLoss conversions; " +
                $"found {blockedDamageConversions}, {unblockedDamageZeroChecks}, {intendedHpLossConversions} " +
                $"({replacements} total).");
        return result;
    }

    // A protected DP layer can intercept an arbitrarily large decimal hit.
    // Saturate only the integer result; keep all damage/absorption math intact.
    private static int ToResultAmount(decimal amount) =>
        (int)Math.Clamp(amount, int.MinValue, int.MaxValue);
}
