using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches;

internal static class TemporaryStatRollTargets
{
    internal static IEnumerable<MethodBase> Resolve(string method)
    {
        foreach (Type type in new[]
                 {
                     typeof(TemporaryStrengthPower), typeof(TemporaryDexterityPower),
                     typeof(TemporaryFocusPower)
                 })
            yield return AccessTools.DeclaredMethod(type, method);
    }
}

[HarmonyPatch]
internal static class TemporaryStatRollBeforeAppliedPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        TemporaryStatRollTargets.Resolve(nameof(PowerModel.BeforeApplied));

    private static void Prefix(PowerModel __instance, Creature target,
        ref decimal amount, Creature applier, CardModel cardSource,
        out TemporaryStatRollScope.Scope __state)
    {
        // Native powers store integer amounts. Passing a fractional initial
        // amount to their callback otherwise looks like an additional stack.
        amount = decimal.Truncate(amount);
        __state = TemporaryStatRollScope.Begin(__instance, target, amount, applier, cardSource);
    }

    private static void Postfix(TemporaryStatRollScope.Scope __state) => __state?.Dispose();

    // The native async method captures the scope for its own continuations;
    // restore the caller immediately, including synchronous exception paths.
    private static void Finalizer(TemporaryStatRollScope.Scope __state) => __state?.Dispose();
}

[HarmonyPatch]
internal static class TemporaryStatRollAmountChangedPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        TemporaryStatRollTargets.Resolve(nameof(PowerModel.AfterPowerAmountChanged));

    private static bool Prefix(PowerModel __instance, PowerModel power,
        ref decimal amount, Creature applier, CardModel cardSource,
        out TemporaryStatRollScope.Scope __state, ref Task __result)
    {
        __state = null;
        if (!ReferenceEquals(power, __instance)) return true;

        // Double copies both the total stat and its expiry counter. Reapplying
        // the counter's stat here would count the temporary contribution twice.
        if (TemporaryStatDuplicationScope.IsCopiedCounter(power, cardSource))
        {
            __result = Task.CompletedTask;
            return false;
        }

        // Normalize the offset, matching PowerCmd.ModifyAmount, rather than
        // the resulting total. This also preserves negative stat adjustments.
        amount = decimal.Truncate(amount);
        __state = TemporaryStatRollScope.Begin(__instance, __instance.Owner,
            amount, applier, cardSource);
        return true;
    }

    private static void Postfix(TemporaryStatRollScope.Scope __state) => __state?.Dispose();

    private static void Finalizer(TemporaryStatRollScope.Scope __state) => __state?.Dispose();
}
