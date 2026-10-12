using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay._Ready))]
internal static class CombatHudLayerPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NCreatureStateDisplay __instance) =>
        CombatHudLayerHelper.Apply(__instance);
}

[HarmonyPatch(typeof(NCreature), "OnFocus")]
internal static class CombatHudFocusLayerPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NCreatureStateDisplay ____stateDisplay, bool ____isRemotePlayerOrPet) =>
        // Native remote focus writes ZIndex = 1; preserve its raised priority
        // within the status layer instead of bringing it above cards and menus.
        CombatHudLayerHelper.Apply(____stateDisplay, raised: ____isRemotePlayerOrPet);
}
