using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class ThirtyAHighSongSynergyPatch
{
    private static void Postfix(CardPlay cardPlay, ref Task __result)
    {
        if (cardPlay?.Card is HighSongHbrgoSaki)
        {
            __result = ResolveAfterCardHooks(__result, cardPlay);
        }
    }

    private static async Task ResolveAfterCardHooks(Task originalTask, CardPlay cardPlay)
    {
        await originalTask;
        // Run once per completed play, after every model's Late hook (including
        // Lost Star Song's draw effect), rather than once per 30A card listener.
        await ThirtyAHighSongSynergy.Resolve(cardPlay);
    }
}
