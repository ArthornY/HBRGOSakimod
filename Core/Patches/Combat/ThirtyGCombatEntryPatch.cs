using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native combat piles already exist here. Restore the physical card ledger
// before opening passives; new proposals wait for the owner's turn start.
[HarmonyPatch]
internal static class ThirtyGCombatEntryPatch
{
    private static MethodBase TargetMethod() => MonsterCardRecipientPatch.GetAsyncBody(
        // Stable exposes a parameterless public method; beta makes the same
        // boundary private and passes its CombatTurnState explicitly.
        AccessTools.Method(typeof(CombatManager), "StartCombatInternal"));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo native = AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCombatStart));
        MethodInfo adapter = AccessTools.Method(typeof(ThirtyGCombatEntryPatch), nameof(PrepareThenNativeStart));
        var code = instructions.ToList();
        var calls = code.Where(instruction => instruction.Calls(native)).ToArray();
        if (calls.Length != 1) throw new InvalidOperationException("Expected one native combat-start hook boundary.");
        calls[0].operand = adapter;
        return code;
    }
    internal static async Task PrepareThenNativeStart(IRunState runState, ICombatState combatState)
    {
        if (combatState != null)
            foreach (var player in combatState.Players)
                await ThirtyGBondHelper.PrepareBattle(new BlockingPlayerChoiceContext(), player);
        await Hook.BeforeCombatStart(runState, combatState);
    }
}

// Filter each existing event stream once rather than patching every individual
// card callback. The permanent deck itself and its roster counts stay intact.
[HarmonyPatch]
internal static class ThirtyGGraduatedHookListenersPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RunState), nameof(RunState.IterateHookListeners));
        yield return AccessTools.Method(typeof(CombatState), nameof(CombatState.IterateHookListeners));
    }
    private static void Postfix(ref IEnumerable<AbstractModel> __result) => __result = Enabled(__result);
    private static IEnumerable<AbstractModel> Enabled(IEnumerable<AbstractModel> models)
    {
        foreach (AbstractModel model in models)
        {
            CardModel card = model switch
            {
                CardModel owned => owned,
                EnchantmentModel { HasCard: true } enchantment => enchantment.Card,
                AfflictionModel { HasCard: true } affliction => affliction.Card,
                _ => null
            };
            if (card == null || !ThirtyGProposalState.IsDisabled(card)) yield return model;
        }
    }
}
