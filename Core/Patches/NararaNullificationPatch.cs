using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class NararaNullificationPlayScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance,
        out NararaNullificationHelper.PlayScope __state) =>
        __state = NararaNullificationHelper.Begin(__instance);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NararaNullificationHelper.PlayScope __state, ref Task __result)
    {
        try { __result = NararaNullificationHelper.Finish(__result, __state); }
        finally { NararaNullificationHelper.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception,
        NararaNullificationHelper.PlayScope __state)
    {
        if (__exception != null) NararaNullificationHelper.Close(__state);
        else NararaNullificationHelper.RestoreCaller(__state);
        return __exception;
    }
}

// Intercept commands rather than raw model setters: shell clamping, revivals,
// combat teardown and resistance updates retain their own model invariants.
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetCurrentHp))]
internal static class NararaNullificationSetCurrentHpPatch
{
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        if (amount >= creature.CurrentHp || !NararaNullificationHelper.IsCardNullified(creature)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp))]
internal static class NararaNullificationSetMaxHpPatch
{
    private static bool Prefix(Creature creature, decimal amount, ref Task<decimal> __result)
    {
        if (amount >= creature.MaxHp || !NararaNullificationHelper.IsCardNullified(creature)) return true;
        __result = Task.FromResult(0m);
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp))]
internal static class NararaNullificationLoseMaxHpPatch
{
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        if (amount <= 0m || !NararaNullificationHelper.IsCardNullified(creature)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Kill),
    new[] { typeof(IReadOnlyCollection<Creature>), typeof(bool) })]
internal static class NararaNullificationKillPatch
{
    private static bool Prefix(ref IReadOnlyCollection<Creature> creatures, ref Task __result)
    {
        // The single-target overload delegates here. Keep unprotected targets,
        // including in the card that removes powers then uses force:true.
        if (!creatures.Any(target => NararaNullificationHelper.IsCardNullified(target))) return true;
        creatures = creatures.Where(target => !NararaNullificationHelper.IsCardNullified(target)).ToArray();
        if (creatures.Count > 0) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Stun),
    new[] { typeof(Creature), typeof(Func<IReadOnlyList<Creature>, Task>), typeof(string) })]
internal static class NararaNullificationStunPatch
{
    private static bool Prefix(Creature creature, ref Task __result)
    {
        if (!NararaNullificationHelper.IsCardNullified(creature)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(HighDimensionalShellPower), "BreakImmediately")]
internal static class NararaNullificationBreakDpPatch
{
    private static bool Prefix(HighDimensionalShellPower __instance, ref Task __result)
    {
        if (!NararaNullificationHelper.IsCardNullified(__instance.Owner)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

// The layered shell overrides the complete operation; patching the base alone
// does not prevent it consuming an earlier layer or setting a protected one to 1.
[HarmonyPatch(typeof(NararaLayeredDpPower), "BreakImmediately")]
internal static class NararaNullificationBreakLayeredDpPatch
{
    private static bool Prefix(NararaLayeredDpPower __instance, ref Task __result)
    {
        if (!NararaNullificationHelper.IsCardNullified(__instance.Owner)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply),
    new[] { typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal),
        typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class NararaNullificationApplyPowerPatch
{
    private static bool Prefix(PowerModel power, Creature target, decimal amount,
        CardModel cardSource, ref Task __result)
    {
        if (!NararaNullificationHelper.BlocksPowerChange(power, target, amount, cardSource)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
internal static class NararaNullificationModifyPowerPatch
{
    private static bool Prefix(PowerModel power, decimal offset, CardModel cardSource,
        ref Task<int> __result)
    {
        if (!NararaNullificationHelper.BlocksPowerChange(power, power.Owner, offset, cardSource)) return true;
        __result = Task.FromResult(power.Amount);
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Remove), new[] { typeof(PowerModel) })]
internal static class NararaNullificationRemovePowerPatch
{
    private static bool Prefix(PowerModel power, ref Task __result)
    {
        if (!NararaNullificationHelper.BlocksPowerRemoval(power)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
