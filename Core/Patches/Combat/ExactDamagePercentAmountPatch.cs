using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ApplyInternal))]
internal static class ExactDamagePercentInitialAmountPatch
{
    private sealed record Preparation(ExactDamagePercentState State, decimal? Previous);
    private static void Prefix(PowerModel __instance, ref decimal amount, out Preparation __state)
    {
        __state = null;
        if (__instance is not IExactDamagePercentPower power || amount == 0m) return;
        ExactDamagePercentState state = power.PercentState;
        __state = new(state, state.PrepareInitialNativePercent(__instance.Amount, amount));
        amount = ExactDamagePercentState.ToNativePercent(amount);
    }
    private static void Finalizer(Preparation __state) =>
        __state?.State.RestorePreparedNativePercent(__state.Previous);
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class ExactDamagePercentNativeAmountPatch
{
    private static void Prefix(PowerModel __instance, ref int amount,
        out ExactDamagePercentState.NativePercentWrite __state) =>
        __state = __instance is IExactDamagePercentPower power
            ? power.PercentState.BeginNativePercentWrite<ExactDamagePercentState.NativePercentWrite>(__instance, ref amount)
            : null;
    private static void Postfix(ExactDamagePercentState.NativePercentWrite __state) =>
        __state?.State.FinishNativePercentWrite(__state);
    private static void Finalizer(ExactDamagePercentState.NativePercentWrite __state) =>
        __state?.State.FinishNativePercentWrite(__state);
}

[HarmonyPatch(typeof(PowerModel), "AddDumbVariablesToDescription")]
internal static class ExactDamagePercentDescriptionAmountPatch
{
    private static void Postfix(PowerModel __instance, LocString description, int? amountOverride)
    {
        if (amountOverride == null && __instance is IExactDamagePercentPower power)
            description.Add("Amount", power.DamagePercent);
    }
}
