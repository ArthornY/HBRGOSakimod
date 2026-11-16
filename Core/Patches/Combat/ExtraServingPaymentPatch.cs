using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.CanPlay), new[] { typeof(UnplayableReason), typeof(AbstractModel) },
    new[] { ArgumentType.Out, ArgumentType.Out })]
internal static class ExtraServingCanPlayPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __instance, ref UnplayableReason reason, ref bool __result)
    {
        if (!reason.HasFlag(UnplayableReason.EnergyCostTooHigh) || !ExtraServingCombatHelper.Eligible(__instance)) return;
        reason &= ~UnplayableReason.EnergyCostTooHigh;
        __result = reason == UnplayableReason.None;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
internal static class ExtraServingSpendResourcesPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out ExtraServingCombatHelper.Payment __state) => __state = ExtraServingCombatHelper.BeginPayment(__instance);
    private static void Postfix(ExtraServingCombatHelper.Payment __state, ref Task<ValueTuple<int, int>> __result)
    {
        if (__state == null) return;
        try { __result = ExtraServingCombatHelper.FinishPayment(__result, __state); }
        finally { ExtraServingCombatHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, ExtraServingCombatHelper.Payment __state)
    { if (__exception != null) ExtraServingCombatHelper.Close(__state); return __exception; }
}

// Synchronous argument adjustment only: native history, deduction and hooks run.
[HarmonyPatch(typeof(CardModel), "SpendEnergy")]
internal static class ExtraServingSpendEnergyPatch
{
    private static void Prefix(CardModel __instance, ref int amount) => ExtraServingCombatHelper.ClampPayment(__instance, ref amount);
}
