using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class FubukiChangePlayedPatch
{
    private static void Postfix(CardPlay cardPlay, ref Task __result) => __result = Finish(__result, cardPlay);
    private static async Task Finish(Task native, CardPlay play)
    { await native; FubukiChangeCombatHelper.AfterPlay(play); }
}
