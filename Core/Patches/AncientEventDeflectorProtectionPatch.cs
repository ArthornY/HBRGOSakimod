using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
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

        if (ancientEvent.Owner == null)
        {
            return;
        }

        // Ancient healing is a fraction of missing health, not maximum health.
        // DP uses its own missing amount even when the player is at full HP.
        // Hotaru's separate HP-recovery guard must not suppress DP recovery.
        decimal recoveryPercent = AscensionHelper.GetValueIfAscension(
            AscensionLevel.WearyTraveler,
            0.8m,
            1m);
        ancientEvent.Owner
            .GetRelic<DeflectorProtection>()
            ?.RestoreDpByMissingPercent(recoveryPercent);
    }
}
// This native entry method only resets Neow HP, heals and animates that heal.
// Skip it before any HP mutation; the rest of BeginEvent still runs normally.
[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class HotaruAncientHealthRecoveryPatch
{
    private static bool Prefix(AncientEventModel __instance, bool isPreFinished, ref Task __result)
    {
        if (isPreFinished || __instance.Owner?.Character is not HbrgoHotaru) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
