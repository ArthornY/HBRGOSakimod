using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class OverworkPlayScopePatch
{
    // Capture before mode-switch prefixes can begin resolving the play.
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out OverworkPlayScope.Entry __state) =>
        __state = OverworkPlayScope.Begin(__instance);

    private static void Postfix(CardModel __instance, OverworkPlayScope.Entry __state, ref Task __result) =>
        __result = Finish(__result, __instance, __state);

    private static async Task Finish(Task original, CardModel card, OverworkPlayScope.Entry entry)
    {
        try { await original; }
        finally { OverworkPlayScope.End(card, entry); }
    }
}
