using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class SingleHitDamageLimitPatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
        typeof(Creature), nameof(Creature.LoseHpInternal),
        new[] { typeof(decimal), typeof(ValueProp) });

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        ConstructorInfo decimalConstructor = typeof(decimal).GetConstructor(
            new[] { typeof(int) });
        MethodInfo minimum = AccessTools.Method(typeof(Math), nameof(Math.Min),
            new[] { typeof(decimal), typeof(decimal) });
        MethodInfo clamp = AccessTools.Method(typeof(Math), nameof(Math.Clamp),
            new[] { typeof(decimal), typeof(decimal), typeof(decimal) });
        MethodInfo decimalToInt = typeof(decimal).GetMethods()
            .Single(method => method.Name == "op_Explicit" &&
                method.ReturnType == typeof(int) &&
                method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(decimal));
        FieldInfo decimalZero = AccessTools.Field(typeof(decimal), nameof(decimal.Zero));
        List<CodeInstruction> result = instructions.ToList();
        int replacements = 0;
        for (int index = 0; index + 3 < result.Count; index++)
        {
            bool isMinimum = index > 0 &&
                result[index - 1].opcode == OpCodes.Ldarg_1 &&
                result[index + 2].Calls(minimum);
            bool isClamp = index > 1 &&
                result[index - 2].opcode == OpCodes.Ldarg_1 &&
                result[index - 1].opcode == OpCodes.Ldsfld &&
                Equals(result[index - 1].operand, decimalZero) &&
                result[index + 2].Calls(clamp);
            if (!result[index].LoadsConstant(999999999) ||
                result[index + 1].opcode != OpCodes.Newobj ||
                !Equals(result[index + 1].operand, decimalConstructor) ||
                !result[index + 3].Calls(decimalToInt) ||
                (!isMinimum && !isClamp))
            {
                continue;
            }

            // Stable uses Min; beta uses Clamp with a zero lower bound. Only
            // replace their upper damage bound, retaining all native HP logic.
            result[index] = new CodeInstruction(result[index])
            {
                opcode = OpCodes.Ldc_I4,
                operand = DamageLimitHelper.MaximumSingleHitDamage
            };
            replacements++;
        }

        if (replacements != 1)
            throw new InvalidOperationException(
                $"Expected one native single-hit damage cap; found {replacements}.");
        return result;
    }
}
