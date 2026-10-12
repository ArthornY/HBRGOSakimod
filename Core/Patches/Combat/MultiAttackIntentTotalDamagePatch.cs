using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(MultiAttackIntent), nameof(MultiAttackIntent.GetTotalDamage))]
internal static class MultiAttackIntentTotalDamagePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo singleDamage = AccessTools.Method(
            typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage));
        MethodInfo repeats = AccessTools.PropertyGetter(
            typeof(AttackIntent), nameof(AttackIntent.Repeats));
        MethodInfo totalDamage = AccessTools.Method(
            typeof(MultiAttackIntentTotalDamagePatch), nameof(CalculateTotalDamage));
        List<CodeInstruction> result = instructions.ToList();
        int replacements = 0;
        for (int index = 3; index + 1 < result.Count; index++)
        {
            if (result[index].opcode != OpCodes.Mul ||
                !result[index - 3].Calls(singleDamage) ||
                result[index - 2].opcode != OpCodes.Ldarg_0 ||
                !result[index - 1].Calls(repeats) ||
                result[index + 1].opcode != OpCodes.Ret)
            {
                continue;
            }

            result[index] = new CodeInstruction(result[index])
            {
                opcode = OpCodes.Call,
                operand = totalDamage
            };
            replacements++;
        }

        if (replacements != 1)
            throw new InvalidOperationException(
                $"Expected one multi-attack intent total; found {replacements}.");
        return result;
    }

    // This total selects the intent icon/animation strength. Saturate its int
    // representation without limiting the actual sum of separate attack hits.
    private static int CalculateTotalDamage(int singleDamage, int repeats) =>
        (int)Math.Clamp((long)singleDamage * repeats, int.MinValue, int.MaxValue);
}
