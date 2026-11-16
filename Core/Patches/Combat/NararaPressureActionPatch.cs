using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class NararaPressureAttackScopePatch
{
    private static void Prefix(AttackCommand __instance, out NararaPressureActionHelper.Scope __state) =>
        __state = NararaPressureActionHelper.Begin(__instance.Attacker);
    private static void Postfix(ref Task<AttackCommand> __result, NararaPressureActionHelper.Scope __state)
    {
        if (__state == null) return;
        try { __result = NararaPressureActionHelper.Finish(__result, __state); }
        finally { NararaPressureActionHelper.Restore(__state); }
    }
    private static Exception Finalizer(Exception __exception, NararaPressureActionHelper.Scope __state)
    { if (__exception != null) NararaPressureActionHelper.Close(__state); return __exception; }
}

[HarmonyPatch(typeof(HighDimensionalShellPower), "DamageDpOnly")]
internal static class NararaPressureDpOnlyPatch
{
    private static void Postfix(HighDimensionalShellPower __instance, PlayerChoiceContext choiceContext,
        Creature dealer, CardModel cardSource, ref Task<decimal> __result)
    {
        if (__instance.Owner?.GetPower<NararaPressurePower>() is not { } pressure) return;
        __result = Finish(__result, pressure, choiceContext, dealer, cardSource);
    }
    private static async Task<decimal> Finish(Task<decimal> task, NararaPressurePower power,
        PlayerChoiceContext context, Creature dealer, CardModel card)
    {
        decimal actual = await task;
        if (actual > 0m) await power.RecordHit(context, dealer, card);
        return actual;
    }
}
