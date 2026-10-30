using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class HighHitDamageAttackScopePatch
{
    private static void Prefix(AttackCommand __instance,
        out HighHitDamageVisualHelper.AttackScope __state) =>
        __state = HighHitDamageVisualHelper.BeginAttack(__instance);

    private static void Postfix(HighHitDamageVisualHelper.AttackScope __state,
        ref Task<AttackCommand> __result)
    {
        try { __result = HighHitDamageVisualHelper.FinishAttack(__result, __state); }
        finally { HighHitDamageVisualHelper.RestoreAttackCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception,
        HighHitDamageVisualHelper.AttackScope __state, AttackCommand __instance)
    {
        if (__exception != null)
        {
            HbrComboHelper.CancelAttack(__instance);
            HighHitDamageVisualHelper.CloseAttack(__state);
        }
        else HighHitDamageVisualHelper.RestoreAttackCaller(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class HighHitDamageResolvedCountPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo method = AccessTools.DeclaredMethod(typeof(AttackCommand), nameof(AttackCommand.Execute));
        Type state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("AttackCommand.Execute async state machine missing.");
        return AccessTools.DeclaredMethod(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.DeclaredMethod(typeof(Hook), nameof(Hook.ModifyAttackHitCount));
        MethodInfo replacement = AccessTools.DeclaredMethod(typeof(HighHitDamageVisualHelper),
            nameof(HighHitDamageVisualHelper.ResolveHitCount));
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(original)) { yield return instruction; continue; }
            changed++;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
        }
        if (changed != 1)
            throw new InvalidOperationException("Expected one resolved attack hit-count boundary.");
    }
}

[HarmonyPatch]
internal static class HighHitDamageCommandScopePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method =>
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length is 6 or 7 &&
                parameters[0].ParameterType == typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext) &&
                parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
                parameters[2].ParameterType == typeof(decimal) &&
                parameters[3].ParameterType == typeof(ValueProp) &&
                parameters[4].ParameterType == typeof(Creature) &&
                parameters[5].ParameterType == typeof(CardModel);
        }).OrderBy(method => method.GetParameters().Length).First();

    private static void Prefix(ValueProp props, Creature dealer, CardModel cardSource,
        out HighHitDamageVisualHelper.DamageScope __state) =>
        __state = HighHitDamageVisualHelper.BeginDamage(props, dealer, cardSource);

    private static void Postfix(HighHitDamageVisualHelper.DamageScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        try { __result = HighHitDamageVisualHelper.FinishDamage(__result, __state); }
        finally { HighHitDamageVisualHelper.RestoreDamageCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception,
        HighHitDamageVisualHelper.DamageScope __state)
    {
        if (__exception != null) HighHitDamageVisualHelper.CloseDamage(__state);
        else HighHitDamageVisualHelper.RestoreDamageCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(NDamageNumVfx), nameof(NDamageNumVfx.Create),
    new[] { typeof(Creature), typeof(DamageResult) })]
internal static class HighHitDamageNumberPatch
{
    private static bool Prefix(Creature target, DamageResult result, ref NDamageNumVfx __result)
    {
        if (!HighHitDamageVisualHelper.TryMergeNumber(target, result)) return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(NDamageBlockedVfx), nameof(NDamageBlockedVfx.Create), new[] { typeof(Creature) })]
internal static class HighHitDamageBlockedNumberPatch
{
    private static bool Prefix(Creature target, ref NDamageBlockedVfx __result)
    {
        if (!HighHitDamageVisualHelper.TryMergeBlocked(target)) return true;
        __result = null;
        return false;
    }
}
