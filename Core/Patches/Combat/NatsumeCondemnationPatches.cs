using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
internal static class NatsumeCardPlayStartedPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardPlay cardPlay) => NatsumeCondemnationCombatHelper.OnCardPlayStarted(cardPlay);
}

[HarmonyPatch]
internal static class NatsumeCondemnationNativeKillPatch
{
    private static MethodInfo TargetMethod() => AccessTools.Method(typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd),
        "KillWithoutCheckingWinCondition");
    private static void Prefix(Creature creature, out NatsumeCondemnationCombatHelper.NativeKillScope __state) =>
        __state = NatsumeCondemnationCombatHelper.BeginNativeKill(creature);
    private static void Postfix(NatsumeCondemnationCombatHelper.NativeKillScope __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = NatsumeCondemnationCombatHelper.FinishNativeKill(__result, __state); }
        finally { NatsumeCondemnationCombatHelper.RestoreNativeKillCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, NatsumeCondemnationCombatHelper.NativeKillScope __state)
    {
        if (__exception != null) __state?.Dispose();
        else NatsumeCondemnationCombatHelper.RestoreNativeKillCaller(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NatsumeCondemnationDeathObservedPatch
{
    private static MethodInfo TargetMethod() => AccessTools.Method(typeof(Hook), nameof(Hook.AfterDeath));
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature creature, bool wasRemovalPrevented) =>
        NatsumeCondemnationCombatHelper.ObserveNativeDeath(creature, wasRemovalPrevented);
}

// Resolve the non-generic native body once. The final result credit is visible
// to ordinary AfterAttack listeners and the card's own Fatal-style checks.
[HarmonyPatch]
internal static class NatsumeCondemnationAttackCompletionPatch
{
    private static MethodInfo TargetMethod()
    {
        MethodInfo method = AccessTools.Method(typeof(AttackCommand), nameof(AttackCommand.Execute));
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException("AttackCommand.Execute async body");
        return AccessTools.DeclaredMethod(state, "MoveNext");
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo hook = AccessTools.Method(typeof(Hook), nameof(Hook.AfterAttack));
        MethodInfo adapter = AccessTools.Method(typeof(NatsumeCondemnationAttackCompletionPatch), nameof(AfterAttack));
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(hook)) { instruction.opcode = OpCodes.Call; instruction.operand = adapter; changed++; }
            yield return instruction;
        }
        if (changed != 1) throw new InvalidOperationException("Expected one native AfterAttack call for Condemnation, found " + changed);
    }
    private static async Task AfterAttack(ICombatState combat, PlayerChoiceContext context, AttackCommand command)
    {
        await NatsumeCondemnationCombatHelper.BeforeAfterAttack(command);
        await Hook.AfterAttack(combat, context, command);
    }
}
