using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// The ordinary non-generic public entry has the same typed Task on both game
// profiles. It runs even when the native draw is prevented, zero or hand-full.
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw),
    new[] { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) })]
internal static class ThirtyOneFStartingHandDrawPatch
{
    private static void Prefix(Player player, bool fromHandDraw, out ThirtyOneFBondHelper.DrawScope __state) =>
        __state = fromHandDraw ? ThirtyOneFBondHelper.BeginTurnDraw(player) : null;
    private static void Postfix(ThirtyOneFBondHelper.DrawScope __state, ref Task<IEnumerable<CardModel>> __result)
    {
        if (__state == null) return;
        try { __result = Finish(__result, __state); }
        finally { ThirtyOneFBondHelper.RestoreCaller(__state); }
    }
    private static async Task<IEnumerable<CardModel>> Finish(Task<IEnumerable<CardModel>> original, ThirtyOneFBondHelper.DrawScope scope)
    {
        try { var result = await original; ThirtyOneFBondHelper.CompleteTurnDraw(scope); return result; }
        finally { scope.Dispose(); }
    }
    private static void Finalizer(System.Exception __exception, ThirtyOneFBondHelper.DrawScope __state)
    { if (__exception != null) __state?.Dispose(); else ThirtyOneFBondHelper.RestoreCaller(__state); }
}
