using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(ActiveScreenContext), nameof(ActiveScreenContext.GetCurrentScreen))]
internal static class HbrMemoryUpgradeStoryScreenContextPatch
{
    private static void Postfix(ref IScreenContext __result) =>
        __result = HbrMemoryUpgradeStoryPresentation.ResolveScreenContext(__result);
}
