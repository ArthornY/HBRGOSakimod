using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
internal static class HbrMemoryGardenCompendiumPatch
{
    private static void Postfix(NInspectCardScreen __instance, NCard ____card) =>
        NMemoryGardenCompendiumSkillsButton.Refresh(__instance, ____card?.Model);
}
