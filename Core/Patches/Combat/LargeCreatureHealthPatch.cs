using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class LargeCreatureSetMaximumPatch
{
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(Creature __instance, decimal amount)
    {
        if (__instance.Monster == null ||
            (!LargeCreatureHealthHelper.HasLargeHealth(__instance) && amount <= BigCombatNumbers.NativeStatCap))
            return true;
        LargeCreatureHealthHelper.SetMaxHp(__instance, amount);
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetCurrentHpInternal))]
internal static class LargeCreatureSetCurrentPatch
{
    private static bool Prefix(Creature __instance, decimal amount)
    {
        if (!LargeCreatureHealthHelper.HasLargeHealth(__instance)) return true;
        LargeCreatureHealthHelper.SetCurrentHp(__instance, amount);
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class LargeCreatureLoseHpPatch
{
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(Creature __instance, decimal amount, ValueProp props, ref DamageResult __result)
    {
        if (!LargeCreatureHealthHelper.HasLargeHealth(__instance)) return true;
        __result = LargeCreatureHealthHelper.LoseHp(__instance, amount, props);
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.GetHpPercentRemaining))]
internal static class LargeCreatureHpPercentagePatch
{
    private static bool Prefix(Creature __instance, ref double __result)
    {
        if (!LargeCreatureHealthHelper.HasLargeHealth(__instance)) return true;
        decimal maximum = LargeCreatureHealthHelper.GetMaxHp(__instance);
        __result = maximum > 0m ? (double)(LargeCreatureHealthHelper.GetCurrentHp(__instance) / maximum) : 0d;
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.RemoveAllPowersInternalExcept))]
internal static class LargeCreatureRetainHealthPatch
{
    private static void Prefix(Creature __instance, ref IEnumerable<PowerModel> except)
    {
        if (__instance.GetPower<LargeHealthPower>() is { } health)
            except = (except ?? Array.Empty<PowerModel>()).Append(health).ToArray();
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
internal static class LargeCreatureCommandSetMaximumPatch
{
    private static bool Prefix(Creature creature, decimal amount, ref Task<decimal> __result)
    {
        if (creature.Monster == null ||
            (!LargeCreatureHealthHelper.HasLargeHealth(creature) && amount <= BigCombatNumbers.NativeStatCap))
            return true;
        __result = SetMaximum(creature, amount);
        return false;
    }

    private static async Task<decimal> SetMaximum(Creature creature, decimal amount)
    {
        decimal oldMaximum = LargeCreatureHealthHelper.GetMaxHp(creature);
        creature.SetMaxHpInternal(Math.Max(0m, amount));
        decimal newMaximum = LargeCreatureHealthHelper.GetMaxHp(creature);
        if (newMaximum <= 0m) await CreatureCmd.Kill(creature);
        return newMaximum - oldMaximum;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetCurrentHp))]
internal static class LargeCreatureCommandSetCurrentPatch
{
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        if (!LargeCreatureHealthHelper.HasLargeHealth(creature)) return true;
        __result = SetCurrent(creature, amount);
        return false;
    }

    private static async Task SetCurrent(Creature creature, decimal amount)
    {
        bool wasDead = creature.IsDead;
        decimal oldHp = LargeCreatureHealthHelper.GetCurrentHp(creature);
        creature.SetCurrentHpInternal(amount);
        decimal newHp = LargeCreatureHealthHelper.GetCurrentHp(creature);
        if (newHp != oldHp)
        {
            if (CombatManager.Instance.IsInProgress && wasDead && newHp > 0m)
                NCombatRoom.Instance?.GetCreatureNode(creature)?.StartReviveAnim();
            await Hook.AfterCurrentHpChanged(creature.Player?.RunState ?? creature.CombatState.RunState,
                creature.CombatState, creature, newHp - oldHp);
        }
        if (creature.IsDead) await CreatureCmd.Kill(creature);
    }
}

// Replace only decimal arithmetic reads. The caller keeps its original hooks,
// animation, player history, revival event and command order.
[HarmonyPatch]
internal static class LargeCreatureHealthArithmeticPatch
{
    internal static MethodInfo AsyncBody(Type type, string name)
    {
        MethodInfo method = AccessTools.Method(type, name);
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException(type.FullName, name + " async state machine");
        return AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Creature), nameof(Creature.HealInternal));
        yield return AsyncBody(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp));
        yield return AsyncBody(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp));
        // Native full restoration is identified by this exact call site, never
        // by comparing an arbitrary SetCurrentHp argument with the proxy max.
        yield return AccessTools.Method(typeof(Creature), nameof(Creature.ScaleMonsterHpForMultiplayer));
        yield return AsyncBody(typeof(IllusionPower), "ReviveMove");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        List<CodeInstruction> result = instructions.ToList();
        MethodInfo current = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.CurrentHp));
        MethodInfo maximum = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.MaxHp));
        MethodInfo conversion = AccessTools.Method(typeof(decimal), "op_Implicit", new[] { typeof(int) });
        int changed = 0;
        if (__originalMethod.DeclaringType?.Name.Contains("IllusionPower") == true ||
            __originalMethod.DeclaringType?.DeclaringType == typeof(IllusionPower))
        {
            // This move computes (max-current) as Int32 before converting. Use
            // decimal subtraction but retain the native full-heal command.
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].Calls(current) || result[i].Calls(maximum))
                {
                    result[i].opcode = OpCodes.Call;
                    result[i].operand = AccessTools.Method(typeof(LargeCreatureHealthHelper),
                        result[i].Calls(current) ? nameof(LargeCreatureHealthHelper.GetCurrentHp) :
                        nameof(LargeCreatureHealthHelper.GetMaxHp));
                    changed++;
                }
                else if (result[i].opcode == OpCodes.Sub)
                {
                    result[i].opcode = OpCodes.Call;
                    result[i].operand = AccessTools.Method(typeof(decimal), "op_Subtraction");
                }
                else if (result[i].Calls(conversion) && i > 0 && result[i - 1].Calls(
                    AccessTools.Method(typeof(decimal), "op_Subtraction")))
                {
                    result[i].opcode = OpCodes.Nop;
                    result[i].operand = null;
                }
            }
        }
        else
        {
            for (int i = 0; i + 1 < result.Count; i++)
            {
                bool isCurrent = result[i].Calls(current);
                if ((!isCurrent && !result[i].Calls(maximum)) || !result[i + 1].Calls(conversion)) continue;
                result[i].opcode = OpCodes.Call;
                result[i].operand = AccessTools.Method(typeof(LargeCreatureHealthHelper),
                    isCurrent ? nameof(LargeCreatureHealthHelper.GetCurrentHp) : nameof(LargeCreatureHealthHelper.GetMaxHp));
                // Nop preserves labels and exception blocks.
                result[i + 1].opcode = OpCodes.Nop;
                result[i + 1].operand = null;
                changed++;
            }
        }
        if (changed == 0) throw new InvalidOperationException("No decimal HP reads in " + __originalMethod);
        return result;
    }
}

[HarmonyPatch]
internal static class LargeCreatureNativeDeathPatch
{
    private static MethodBase TargetMethod() =>
        LargeCreatureHealthArithmeticPatch.AsyncBody(typeof(CreatureCmd), "KillWithoutCheckingWinCondition");

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo lose = AccessTools.Method(typeof(Creature), nameof(Creature.LoseHpInternal));
        MethodInfo hook = AccessTools.Method(typeof(Hook), nameof(Hook.AfterCurrentHpChanged));
        int losses = 0, hooks = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(lose))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(LargeCreatureHealthHelper), nameof(LargeCreatureHealthHelper.LoseHpForKill));
                losses++;
            }
            else if (instruction.Calls(hook))
            {
                instruction.operand = AccessTools.Method(typeof(LargeCreatureHealthHelper), nameof(LargeCreatureHealthHelper.AfterKillHpChanged));
                hooks++;
            }
            yield return instruction;
        }
        if (losses != 1 || hooks != 1) throw new InvalidOperationException("Native death drain shape changed.");
    }
}

[HarmonyPatch]
internal static class LargeCreatureNativeHalfHealthPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(LagavulinMatriarch), nameof(LagavulinMatriarch.AfterDamageReceived));
        MethodInfo[] predicates = typeof(FrogKnight).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => method.Name.StartsWith("<GenerateMoveStateMachine>b__") && method.ReturnType == typeof(bool)).ToArray();
        if (predicates.Length != 2) throw new InvalidOperationException("Expected the two FrogKnight half-health predicates.");
        foreach (MethodInfo predicate in predicates) yield return predicate;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        ReplaceCurrent(instructions, nameof(LargeCreatureHealthHelper.GetCurrentForHalfComparison));

    internal static IEnumerable<CodeInstruction> ReplaceCurrent(IEnumerable<CodeInstruction> instructions, string replacement)
    {
        MethodInfo getter = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.CurrentHp));
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(getter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(LargeCreatureHealthHelper), replacement);
                matches++;
            }
            yield return instruction;
        }
        if (matches != 1) throw new InvalidOperationException("Expected one current HP comparison.");
    }
}

[HarmonyPatch]
internal static class LargeCreatureShriekThresholdPatch
{
    private static MethodBase TargetMethod() => LargeCreatureHealthArithmeticPatch.AsyncBody(typeof(ShriekPower), nameof(ShriekPower.AfterDamageReceived));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        LargeCreatureNativeHalfHealthPatch.ReplaceCurrent(instructions, nameof(LargeCreatureHealthHelper.GetCurrentForShriekComparison));
}

[HarmonyPatch]
internal static class LargeCreaturePlowThresholdPatch
{
    private static MethodBase TargetMethod() => LargeCreatureHealthArithmeticPatch.AsyncBody(typeof(PlowPower), nameof(PlowPower.AfterDamageReceived));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        LargeCreatureNativeHalfHealthPatch.ReplaceCurrent(instructions, nameof(LargeCreatureHealthHelper.GetCurrentForPlowComparison));
}

[HarmonyPatch(typeof(DoomPower), nameof(DoomPower.IsOwnerDoomed))]
internal static class LargeCreatureDoomThresholdPatch
{
    private static bool Prefix(DoomPower __instance, ref bool __result)
    {
        if (!LargeCreatureHealthHelper.HasLargeHealth(__instance.Owner)) return true;
        __result = LargeCreatureHealthHelper.GetCurrentHp(__instance.Owner) <= __instance.Amount;
        return false;
    }
}
