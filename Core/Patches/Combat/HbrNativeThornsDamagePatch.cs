using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Thorns retains native stacks and the Unpowered retaliation flag. Declare its
// raw amount as a skill at the command boundary without creating an attacking
// hit that could recursively trigger another creature's Thorns.
[HarmonyPatch]
internal static class HbrNativeThornsDamagePatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo method = AccessTools.Method(typeof(ThornsPower), nameof(ThornsPower.BeforeDamageReceived));
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("Expected native Thorns async damage hook.");
        return AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        FieldInfo power = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        List<CodeInstruction> result = new(instructions);
        int matches = 0;
        foreach (CodeInstruction instruction in result)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(CreatureCmd) &&
                method.Name == nameof(CreatureCmd.Damage) &&
                Array.Exists(method.GetParameters(), parameter => parameter.ParameterType == typeof(decimal)))
            {
                CodeInstruction loadState = new(OpCodes.Ldarg_0);
                loadState.labels.AddRange(instruction.labels);
                loadState.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();
                yield return loadState;
                yield return new CodeInstruction(OpCodes.Ldfld, power);
                instruction.opcode = OpCodes.Call;
                instruction.operand = HbrNativeEffectSkillDamagePatch.CreateWrapper(method, typeof(PowerModel));
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one native Thorns retaliation command.");
    }
}
