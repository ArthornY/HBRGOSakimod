using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetWithModifiers))]
internal static class MonakaFaithEnergyCostPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel ____card, CostModifiers modifiers, ref int __result)
    { if (modifiers.HasFlag(CostModifiers.Global) && __result >= 0 && MonakaWindForestCombatHelper.IsFaithFree(____card)) __result = 0; }
}
[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
internal static class MonakaFaithPaymentPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out MonakaWindForestCombatHelper.Payment __state) =>
        __state = MonakaWindForestCombatHelper.BeginPayment(__instance);
    private static void Postfix(MonakaWindForestCombatHelper.Payment __state, ref Task<ValueTuple<int, int>> __result)
    { if (__state != null) __result = MonakaWindForestCombatHelper.FinishPayment(__result, __state); }
    private static Exception Finalizer(Exception __exception, MonakaWindForestCombatHelper.Payment __state)
    { if (__exception != null) MonakaWindForestCombatHelper.CancelPayment(__state); return __exception; }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class MonakaWarMeritDamagePatch
{
    private static void Postfix(Creature dealer, DamageResult results, ValueProp props, Creature target, CardModel cardSource, ref Task __result)
    {
        if (MonakaWindForestCombatHelper.IsOwnAttack(cardSource, dealer, target, props)) __result = Finish(__result, cardSource, results);
    }
    private static async Task Finish(Task original, CardModel source, DamageResult result)
    { await original; MonakaWindForestCombatHelper.RecordDamage(source, result); }
}
