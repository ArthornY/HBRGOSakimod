using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class MagicSwordCostDisplayPatch
{
    private static void Postfix(NCard __instance, PileType pileType) => NMagicSwordCostBadge.Update(__instance, pileType);
}

[HarmonyPatch(typeof(NCard), "UpdateStarCostVisuals")]
internal static class MagicSwordCostLayoutPatch
{
    private static void Postfix(NCard __instance) => NMagicSwordCostBadge.UpdateLayout(__instance);
}

[HarmonyPatch]
internal static class MagicSwordCostPoolPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NCard), nameof(NCard.OnFreedToPool));
        yield return AccessTools.Method(typeof(NCard), nameof(NCard.OnReturnedFromPool));
    }
    private static void Prefix(NCard __instance) => NMagicSwordCostBadge.Reset(__instance);
}
