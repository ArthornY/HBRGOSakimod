using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(EventModel), nameof(EventModel.BeginEvent))]
internal static class AncientEventDeflectorProtectionPatch
{
    private static void Postfix(
        EventModel __instance,
        bool isPreFinished,
        ref Task __result)
    {
        if (__instance is not AncientEventModel ancientEvent || isPreFinished)
        {
            return;
        }

        __result = RestoreDpAfterEntry(__result, ancientEvent);
    }

    private static async Task RestoreDpAfterEntry(
        Task original,
        AncientEventModel ancientEvent)
    {
        await original;

        if (ancientEvent.Owner == null || ancientEvent.HealedAmount <= 0)
        {
            return;
        }

        ancientEvent.Owner
            .GetRelic<DeflectorProtection>()
            ?.RestoreDpFromHealAmount(ancientEvent.HealedAmount);
    }
}
