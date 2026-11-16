using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
internal static class CharlottaJealousyPatch
{
    private static void Prefix(CardPlay cardPlay) => CharlottaLoveCombatHelper.BeforeCardPlayed(cardPlay);
}
