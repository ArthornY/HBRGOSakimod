using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class RightTopBarInfoLayoutPatch
{
    private static void Postfix(NGlobalUi __instance) => NRightTopBarInfoLayout.Install(__instance);
}
