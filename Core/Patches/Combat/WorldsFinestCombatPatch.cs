using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class WorldsFinestDamageScopePatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;
    [HarmonyPriority(Priority.First)]
    private static void Prefix(out WorldsFinestCombatHelper.DamageScope __state) =>
        __state = WorldsFinestCombatHelper.BeginDamage();
    private static void Postfix(WorldsFinestCombatHelper.DamageScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        try { __result = WorldsFinestCombatHelper.FinishDamage(__result, __state); }
        finally { WorldsFinestCombatHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, WorldsFinestCombatHelper.DamageScope __state)
    {
        if (__exception != null) WorldsFinestCombatHelper.CloseDamage(__state);
        else WorldsFinestCombatHelper.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class WorldsFinestLethalHpLossPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Creature target, HpLossHookPhase phases, ref decimal __result) =>
        __result = WorldsFinestCombatHelper.PreventLethalHpLoss(target, __result, phases);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterModifyingHpLostAfterOsty))]
internal static class WorldsFinestHpModifiersPatch
{
    private static void Postfix(ref Task __result)
    {
        WorldsFinestCombatHelper.Revival[] pending = WorldsFinestCombatHelper.TakePendingRevivals();
        if (pending.Length > 0) __result = WorldsFinestCombatHelper.FinishHpModifiers(__result, pending);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class WorldsFinestCombatStartPatch
{
    private static void Postfix(ICombatState combatState, ref Task __result) =>
        __result = Finish(__result, combatState);
    private static async Task Finish(Task task, ICombatState combatState)
    {
        await task;
        await WorldsFinestCombatHelper.StartCombat(combatState);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class SpiritTalismanAmountPatch
{
    private static void Prefix(PowerModel __instance, ref int amount)
    {
        if (__instance is SpiritTalismanPower && __instance.IsMutable)
            amount = Math.Clamp(amount, 0, SpiritTalismanPower.MaximumAmount);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class WorldsFinestThirtyOneACardPatch
{
    private static void Postfix(CardPlay cardPlay, ref Task __result)
    {
        if (ThirtyOneAKeywordRules.IsThirtyOneACard(cardPlay?.Card))
            __result = Finish(__result, cardPlay);
    }
    private static async Task Finish(Task original, CardPlay play)
    {
        await original;
        await WorldsFinestCombatHelper.AfterThirtyOneACardPlayed(new ThrowingPlayerChoiceContext(), play);
    }
}
