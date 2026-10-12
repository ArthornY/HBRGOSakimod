using System.Globalization;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class HotaruSacrificeEnergyDisplayPatch
{
    private static void Postfix(NCard __instance, MegaLabel ____energyLabel)
    {
        if (__instance.Visibility != ModelVisibility.Visible ||
            __instance.Model is not HotaruSacrifice card || card.EnergyCost.CostsX)
            return;
        int cost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
        if (cost < 0) return;
        // The native icon, affordability and color use the actual payable cost.
        // Only this card's printed number includes its three-energy refund.
        ____energyLabel.SetTextAutoSize((cost - 3).ToString(CultureInfo.InvariantCulture));
    }
}
