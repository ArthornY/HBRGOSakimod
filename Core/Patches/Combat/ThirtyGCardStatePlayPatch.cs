using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ThirtyGGraduatedWrapperPatch
{
    [HarmonyPriority(Priority.First + 100)]
    private static void Prefix(CardModel __instance, out ThirtyGProposalState.PlayScope __state) =>
        __state = ThirtyGProposalState.BeginPlay(__instance);
    private static void Postfix(ThirtyGProposalState.PlayScope __state, ref Task __result)
    {
        if (__state == null) return;
        __result = ThirtyGProposalState.FinishPlay(__result, __state); ThirtyGProposalState.RestoreCaller(__state);
    }
    private static Exception Finalizer(Exception __exception, ThirtyGProposalState.PlayScope __state)
    { if (__exception != null) ThirtyGProposalState.ClosePlay(__state); else ThirtyGProposalState.RestoreCaller(__state); return __exception; }
}
[HarmonyPatch]
internal static class ThirtyGNullifiedPlayCountPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardModel), "GeneratePlayCount");
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __instance, ref Task<int> __result)
    { if (ThirtyGProposalState.IsDisabled(__instance)) __result = Nullify(__result, __instance); }
    // Retain native payment, result-pile handling and per-play cost cleanup.
    // Zero iterations suppress the entire card, including attached effects and
    // external replays, while the next-play state advances only after cleanup.
    private static async Task<int> Nullify(Task<int> original, CardModel card)
    {
        await original;
        await PowerCardPlayVisualsHelper.FinishNullifiedPowerPlay(card);
        return 0;
    }
}
// Only ordinary virtual callback bodies are wrapped. No native generic command
// factory or async state-machine entry is detoured.
[HarmonyPatch]
internal static class ThirtyGHeartfeltHandEndPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(ThirtyGHeartfeltHandEndPatch).Assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(CardModel).IsAssignableFrom(type))
        .Select(type => type.GetMethod(nameof(CardModel.BeforeSideTurnEnd), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        .Where(method => method != null && method.ReturnType == typeof(Task));
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(CardModel __instance, ref Task __result)
    {
        if (!ThirtyGProposalState.IsDisabled(__instance)) return true;
        __result = Task.CompletedTask; return false;
    }
}
