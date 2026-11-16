using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class MagicSwordTurnEndPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out MagicSwordTurnEndHelper.CardPlayScope __state) =>
        __state = MagicSwordTurnEndHelper.BeginCardPlay(__instance);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(MagicSwordTurnEndHelper.CardPlayScope __state, ref Task __result)
    {
        if (__state != null) __result = FinishPlay(__result, __state);
    }

    private static async Task FinishPlay(Task original, MagicSwordTurnEndHelper.CardPlayScope scope)
    {
        bool successful = false;
        try
        {
            await original;
            successful = true;
        }
        finally { MagicSwordTurnEndHelper.EndCardPlay(scope, successful); }
    }

    private static Exception Finalizer(Exception __exception, MagicSwordTurnEndHelper.CardPlayScope __state)
    {
        // Also release the scope if a synchronous prefix/wrapper throws before
        // it can return a task. The scope closes once on either failure path.
        if (__exception != null) MagicSwordTurnEndHelper.EndCardPlay(__state, successful: false);
        return __exception;
    }
}
