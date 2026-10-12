using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.PreviewValue), MethodType.Setter)]
internal static class DamagePreviewLimitPatch
{
    private const int NativeMaximumValue = 999_999_999;

    private static decimal GetMaximum(DynamicVar variable) =>
        variable is DamageVar or CalculatedDamageVar or OstyDamageVar
            ? DamageLimitHelper.MaximumSingleHitDamage
            : NativeMaximumValue;

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        ConstructorInfo decimalConstructor = typeof(decimal).GetConstructor(new[] { typeof(int) });
        MethodInfo minimum = AccessTools.Method(typeof(Math), nameof(Math.Min),
            new[] { typeof(decimal), typeof(decimal) });
        MethodInfo maximum = AccessTools.Method(typeof(DamagePreviewLimitPatch), nameof(GetMaximum));
        List<CodeInstruction> result = instructions.ToList();
        int replacements = 0;
        for (int index = 1; index + 2 < result.Count; index++)
        {
            if (result[index - 1].opcode != OpCodes.Ldarg_1 ||
                !result[index].LoadsConstant(NativeMaximumValue) ||
                result[index + 1].opcode != OpCodes.Newobj ||
                !Equals(result[index + 1].operand, decimalConstructor) ||
                !result[index + 2].Calls(minimum))
                continue;

            // Change only the upper bound. Keep the native setter's assignment,
            // assertions and any subsequent updates intact for every var type.
            result[index] = new CodeInstruction(result[index])
            {
                opcode = OpCodes.Ldarg_0,
                operand = null
            };
            result[index + 1] = new CodeInstruction(result[index + 1])
            {
                opcode = OpCodes.Call,
                operand = maximum
            };
            replacements++;
        }

        if (replacements != 1)
            throw new InvalidOperationException(
                $"Expected one native preview-value cap; found {replacements}.");
        return result;
    }
}
