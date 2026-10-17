using System;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

internal static class HotaruFirstManualEnergyScope
{
    internal sealed class Scope
    {
        internal CardModel Card;
        internal Scope Previous;
        internal bool Active = true;
    }

    private static readonly AsyncLocal<Scope> Current = new();

    internal static bool ShouldBeFree(CardModel card) => card?.IsMutable == true &&
        card.Owner?.PlayerCombatState?.Phase == PlayerTurnPhase.Play &&
        card.Owner?.Creature?.GetPower<HotaruMyIndulgencePower>()?.ShouldMakeFirstManualFree(card) == true;

    internal static Scope Begin(CardModel card)
    {
        if (!ShouldBeFree(card)) return null;
        Scope scope = new() { Card = card, Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }

    internal static bool Contains(CardModel card)
    {
        for (Scope scope = Current.Value; scope != null; scope = scope.Previous)
            if (scope.Active && ReferenceEquals(scope.Card, card)) return true;
        return false;
    }

    internal static void RestoreCaller(Scope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    internal static void Close(Scope scope)
    {
        if (scope != null) scope.Active = false;
        RestoreCaller(scope);
    }

    internal static async Task<ValueTuple<int, int>> Finish(Task<ValueTuple<int, int>> original, Scope scope)
    {
        try
        {
            (int _, int stars) = await original;
            return (0, stars);
        }
        finally { Close(scope); }
    }
}

[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetWithModifiers))]
internal static class HotaruFirstManualEnergyPreviewPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardEnergyCost __instance, CardModel ____card,
        CostModifiers modifiers, ref int __result)
    {
        if (!__instance.CostsX && __result >= 0 && modifiers.HasFlag(CostModifiers.Global) &&
            HotaruFirstManualEnergyScope.ShouldBeFree(____card)) __result = 0;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
internal static class HotaruFirstManualSpendResourcesPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out HotaruFirstManualEnergyScope.Scope __state) =>
        __state = HotaruFirstManualEnergyScope.Begin(__instance);

    private static void Postfix(HotaruFirstManualEnergyScope.Scope __state,
        ref Task<ValueTuple<int, int>> __result)
    {
        if (__state == null) return;
        __result = HotaruFirstManualEnergyScope.Finish(__result, __state);
        HotaruFirstManualEnergyScope.RestoreCaller(__state);
    }

    private static Exception Finalizer(Exception __exception, HotaruFirstManualEnergyScope.Scope __state)
    {
        if (__exception != null) HotaruFirstManualEnergyScope.Close(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(CardModel), "SpendEnergy")]
internal static class HotaruFirstManualSpendEnergyPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(CardModel __instance, int amount, ref Task __result)
    {
        if (!HotaruFirstManualEnergyScope.Contains(__instance)) return true;
        // X still means the energy available before playing. Only payment is
        // waived; native SpendStars and the captured effect value stay intact.
        if (__instance.EnergyCost.CostsX) __instance.EnergyCost.CapturedXValue = amount;
        __result = Hook.AfterEnergySpent(__instance.CombatState, __instance, 0);
        return false;
    }
}
