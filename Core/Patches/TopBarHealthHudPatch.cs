using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class TopBarHealthHudInstallPatch
{
    private static void Postfix(NGlobalUi __instance, RunState runState) => NTopBarHealthHud.Install(__instance, runState);
}

[HarmonyPatch(typeof(NHealthBar), "RefreshBlockUi")]
internal static class TopBarHealthHudBlockPatch
{
    private static bool Prefix(NHealthBar __instance) => !__instance.HasMeta(NTopBarHealthHud.ViewMarker);
}
