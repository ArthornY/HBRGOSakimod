using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class PhantomNativeCombatEndPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in new[]
                 {
                     typeof(AdaptablePower), typeof(StockPower),
                     typeof(InfestedPower), typeof(SurprisePower)
                 })
        {
            yield return AccessTools.Method(type, nameof(PowerModel.ShouldStopCombatFromEnding));
        }
    }

    private static void Postfix(PowerModel __instance, ref bool __result)
    {
        if (__instance.Owner?.PetOwner != null)
        {
            // Includes newly created pets whose PhantomPower has not been initialized yet.
            // Enemy second phases must not keep combat alive for an allied phantom.
            __result = false;
        }
    }
}

[HarmonyPatch]
internal static class PhantomNativeDeathPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in new[]
                 {
                     typeof(StockPower), typeof(InfestedPower), typeof(SurprisePower)
                 })
        {
            yield return AccessTools.Method(type, nameof(PowerModel.AfterDeath));
        }
    }

    private static void Prefix(
        PowerModel __instance,
        Creature target,
        bool wasRemovalPrevented,
        out PhantomMinionSummonContext.Scope __state)
    {
        Creature owner = __instance.Owner;
        __state = !wasRemovalPrevented && target == owner &&
                  owner?.PetOwner != null && owner.HasPower<PhantomPower>()
            ? PhantomMinionSummonContext.Begin(owner)
            : null;
    }

    private static void Postfix(
        ref Task __result,
        PhantomMinionSummonContext.Scope __state)
    {
        if (__state == null)
        {
            return;
        }

        // The native async method has captured the scope for its continuations.
        // Restore the caller now, before another creature's hook can run.
        __state.Dispose();
        __result = FinishNativeDeath(__result, __state);
    }

    private static void Finalizer(PhantomMinionSummonContext.Scope __state)
    {
        // Also restore the caller if the original throws before returning a Task.
        __state?.Dispose();
    }

    private static async Task FinishNativeDeath(
        Task nativeDeath,
        PhantomMinionSummonContext.Scope scope)
    {
        await nativeDeath;
        await PhantomHelper.InitializeNativeDeathMinions(
            scope.Leader, scope.CreatedCreatures);
    }
}
