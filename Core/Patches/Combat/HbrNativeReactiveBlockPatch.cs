using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrNativeSkittishBlockPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo method = AccessTools.DeclaredMethod(typeof(SkittishPower), nameof(SkittishPower.AfterAttack));
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        return state == null ? method : AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.GainBlock),
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool) });
        MethodInfo adapter = AccessTools.Method(typeof(HbrNativeSkittishBlockPatch), nameof(GainBlock));
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = adapter;
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1) throw new InvalidOperationException("Expected one native Skittish block command.");
    }

    private static Task<decimal> GainBlock(Creature target, decimal raw, ValueProp props, CardPlay card, bool fast)
    {
        decimal amount = HbrBattleStatsHelper.IsEnabled(target)
            ? HbrNativeEffectSkillHelper.ResolveNativeBlockAmount(target, raw) : raw;
        // Preserve Unpowered and every native block hook; the input alone is
        // now resolved. Already-world-unit Plating never enters this adapter.
        return CreatureCmd.GainBlock(target, amount, props, card, fast);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.DisplayAmount), MethodType.Getter)]
internal static class HbrNativeSkittishBlockBadgePatch
{
    private static void Postfix(PowerModel __instance, ref int __result)
    {
        if (__instance is not SkittishPower || !__instance.IsMutable ||
            !HbrBattleStatsHelper.IsEnabled(__instance.Owner)) return;
        __result = (int)Math.Clamp(HbrNativeEffectSkillHelper.ResolveNativeBlockAmount(
            __instance.Owner, __instance.Amount), 0m, BigCombatNumbers.NativeStatCap);
    }
}
