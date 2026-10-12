using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class CardPlayContextPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, bool isAutoPlay,
        out CardPlayContextHelper.PlayScope __state) =>
        __state = CardPlayContextHelper.Begin(__instance, isAutoPlay);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardPlayContextHelper.PlayScope __state, ref Task __result)
    {
        try { __result = CardPlayContextHelper.Finish(__result, __state); }
        finally { CardPlayContextHelper.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception,
        CardPlayContextHelper.PlayScope __state)
    {
        if (__exception != null) CardPlayContextHelper.Close(__state);
        else CardPlayContextHelper.RestoreCaller(__state);
        return __exception;
    }
}
