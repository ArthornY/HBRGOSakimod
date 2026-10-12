using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.CommonUi;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Patches;

// Native RefreshAmount runs on initial/model binding and DisplayAmountChanged,
// including a loaded Millsage. Remove the retired left label, preserving Layers.
[HarmonyPatch(typeof(NRelicInventoryHolder), "RefreshAmount")]
internal static class MillsageDamageMultiplierCounterPatch
{
    private static void Postfix(NRelicInventoryHolder __instance, MegaLabel ____amountLabel) =>
        NMillsageDamageMultiplierCounter.Refresh(__instance, ____amountLabel);
}
