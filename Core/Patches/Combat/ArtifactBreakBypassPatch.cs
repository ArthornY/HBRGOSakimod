using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(
    typeof(ArtifactPower),
    nameof(ArtifactPower.TryModifyPowerAmountReceived))]
internal static class ArtifactBreakBypassPatch
{
    private static bool Prefix(
        PowerModel canonicalPower,
        decimal amount,
        ref decimal modifiedAmount,
        ref bool __result)
    {
        if (canonicalPower is not (BreakPower or DestructionRatePower))
        {
            return true;
        }

        modifiedAmount = amount;
        __result = false;
        return false;
    }
}
