using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class ChallengeAnnouncementPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NMainMenu __instance) =>
        NHbrChallengeAnnouncementPrompt.Install(__instance);
}
