using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ChieLolitaPlayPatch
{
    [HarmonyPriority(Priority.High)]
    private static void Prefix(CardModel __instance, bool isAutoPlay, out ChieLitaState.ManualScope __state) =>
        __state = ChieLitaState.BeginManual(__instance, isAutoPlay);
    private static void Postfix(ChieLitaState.ManualScope __state, ref Task __result)
    {
        if (__state == null) return;
        __result = ChieLitaState.Finish(__result, __state); ChieLitaState.RestoreCaller(__state);
    }
    private static Exception Finalizer(Exception __exception, ChieLitaState.ManualScope __state)
    { if (__exception != null) __state?.Dispose(); else ChieLitaState.RestoreCaller(__state); return __exception; }
}
[HarmonyPatch]
internal static class ChieLolitaFinalPlayCountPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(CardModel), "GeneratePlayCount");
    private static void Postfix(CardModel __instance, ref Task<int> __result)
    { if (ChieLitaState.ReplayBonus(__instance) > 0) __result = ChieLitaState.FinishPlayCount(__result, __instance); }
}
