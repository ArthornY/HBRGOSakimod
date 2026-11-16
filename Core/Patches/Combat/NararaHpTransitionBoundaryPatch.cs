using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class NararaHpCardActionScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out NararaHpTransitionBoundary.ActionScope __state) =>
        __state = NararaHpTransitionBoundary.Begin(__instance.Owner?.Creature.CombatState);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NararaHpTransitionBoundary.ActionScope __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = NararaHpTransitionBoundary.Finish(__result, __state); }
        finally { NararaHpTransitionBoundary.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, NararaHpTransitionBoundary.ActionScope __state)
    {
        if (__exception != null) NararaHpTransitionBoundary.Close(__state, failed: true);
        else NararaHpTransitionBoundary.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class NararaHpAttackActionScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(AttackCommand __instance, out NararaHpTransitionBoundary.ActionScope __state) =>
        __state = NararaHpTransitionBoundary.Begin(__instance.Attacker?.CombatState);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NararaHpTransitionBoundary.ActionScope __state, ref Task<AttackCommand> __result)
    {
        if (__state == null) return;
        try { __result = NararaHpTransitionBoundary.Finish(__result, __state); }
        finally { NararaHpTransitionBoundary.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, NararaHpTransitionBoundary.ActionScope __state)
    {
        if (__exception != null) NararaHpTransitionBoundary.Close(__state, failed: true);
        else NararaHpTransitionBoundary.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class NararaHpDamageActionScopePatch
{
    private static MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature dealer, CardModel cardSource,
        out NararaHpTransitionBoundary.ActionScope __state) =>
        __state = NararaHpTransitionBoundary.Begin(NararaHpTransitionBoundary.CombatFor(dealer, cardSource));

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NararaHpTransitionBoundary.ActionScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state == null) return;
        try { __result = NararaHpTransitionBoundary.Finish(__result, __state); }
        finally { NararaHpTransitionBoundary.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, NararaHpTransitionBoundary.ActionScope __state)
    {
        if (__exception != null) NararaHpTransitionBoundary.Close(__state, failed: true);
        else NararaHpTransitionBoundary.RestoreCaller(__state);
        return __exception;
    }
}

// Phantom attacks send segments directly to the native damage command instead
// of using AttackCommand. Keep the whole intent (including copied moves) in one
// action even if a standalone EndTurn request changes the side mid-resolution.
[HarmonyPatch(typeof(PhantomHelper), "PerformCurrentIntent")]
internal static class NararaHpPhantomActionScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature phantom, out NararaHpTransitionBoundary.ActionScope __state) =>
        __state = NararaHpTransitionBoundary.Begin(phantom?.CombatState);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NararaHpTransitionBoundary.ActionScope __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = NararaHpTransitionBoundary.Finish(__result, __state); }
        finally { NararaHpTransitionBoundary.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, NararaHpTransitionBoundary.ActionScope __state)
    {
        if (__exception != null) NararaHpTransitionBoundary.Close(__state, failed: true);
        else NararaHpTransitionBoundary.RestoreCaller(__state);
        return __exception;
    }
}
