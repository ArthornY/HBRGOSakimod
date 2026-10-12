using System.Globalization;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NPower), "RefreshAmount")]
internal static class KillKissFusionAmountDisplayPatch
{
    private static void Postfix(PowerModel ____model, MegaLabel ____amountLabel)
    {
        // Show the pending level only on the combat badge; retain Single elsewhere.
        if (____model is KillKissFusionPower fusion && ____amountLabel != null)
            ____amountLabel.SetTextAutoSize(fusion.DisplayAmount.ToString(CultureInfo.InvariantCulture));
    }
}
