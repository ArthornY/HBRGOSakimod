using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(EventModel), nameof(EventModel.AfterEventStarted))]
internal static class AncientEventDeflectorProtectionPatch
{
    private static void Postfix(EventModel __instance)
    {
        if (__instance is not AncientEventModel ancientEvent || ancientEvent.Owner == null || ancientEvent.HealedAmount <= 0)
        {
            return;
        }

        ancientEvent.Owner.GetRelic<DeflectorProtection>()?.RestoreDpFromHealAmount(ancientEvent.HealedAmount);
    }
}
