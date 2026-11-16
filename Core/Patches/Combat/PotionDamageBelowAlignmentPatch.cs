using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Patches.Combat;

// New native potion holders create with None, then place the tooltip below.
// Remember that explicit operation for later damage-description refreshes.
[HarmonyPatch]
internal static class PotionDamageBelowAlignmentPatch
{
    private static bool Prepare() => NativeHoverTipCompatibility.BelowMethod != null;
    private static MethodInfo TargetMethod() => NativeHoverTipCompatibility.BelowMethod;
    private static void Postfix(NHoverTipSet __instance) => NativeHoverTipCompatibility.RecordBelow(__instance);
}
