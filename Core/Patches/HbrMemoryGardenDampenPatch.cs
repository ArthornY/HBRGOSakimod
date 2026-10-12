using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class HbrMemoryGardenDampenCandidatesPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo[] predicates = typeof(DampenPower).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(type => AccessTools.GetDeclaredMethods(type))
            .Where(method => method.Name.StartsWith("<AfterApplied>", StringComparison.Ordinal) &&
                method.ReturnType == typeof(bool) &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(CardModel))
            .ToArray();
        if (predicates.Length == 0)
            throw new MissingMethodException(typeof(DampenPower).FullName, "AfterApplied card predicate");
        return predicates;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __0, ref bool __result)
    {
        if (__result && HbrMemoryGardenCardCatalog.IsGardenCard(__0)) __result = false;
    }
}

[HarmonyPatch(typeof(DampenPower), nameof(DampenPower.AfterApplied))]
internal static class HbrMemoryGardenDampenAppliedPatch
{
    private static void Postfix(DampenPower __instance) =>
        HbrMemoryGardenDampen.Apply(__instance.Owner?.Player);
}

[HarmonyPatch(typeof(DampenPower), nameof(DampenPower.AfterRemoved))]
internal static class HbrMemoryGardenDampenRemovedPatch
{
    private static void Postfix(Creature oldOwner) =>
        HbrMemoryGardenDampen.RestorePending(oldOwner?.Player);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class HbrMemoryGardenDampenCombatEndPatch
{
    private static void Prefix(ICombatState combatState)
    {
        // Victory clears powers without calling every AfterRemoved hook. Undo
        // the temporary loss before that cleanup and before the victory save.
        if (combatState == null) return;
        foreach (var player in combatState.Players)
            HbrMemoryGardenDampen.RestorePending(player);
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class HbrMemoryGardenDampenCombatResetPatch
{
    private static void Prefix(CombatManager __instance)
    {
        // Capture this manager's actual players before Reset detaches them; do
        // not use RunManager, which may already refer to a different run. This
        // getter handles both stable and beta's different internal state fields.
        CombatState state = __instance.DebugOnlyGetState();
        if (state == null) return;
        foreach (var player in state.Players)
            HbrMemoryGardenDampen.RestorePending(player);
    }
}
