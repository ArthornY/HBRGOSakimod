using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class CountedRollFailedPlayPatch
{
    internal sealed class PlayMarker
    {
        internal RollGlossaryCapability Capability;
        internal object Reservations;
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, out PlayMarker __state)
    {
        RollGlossaryCapability capability = __instance.Capability<RollGlossaryCapability>();
        __state = capability == null ? null : new PlayMarker
        {
            Capability = capability,
            Reservations = capability.CaptureRollReservationMarker()
        };
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PlayMarker __state, ref Task __result)
    {
        if (__state != null) __result = Finish(__result, __state);
    }

    private static async Task Finish(Task original, PlayMarker marker)
    {
        try { await original; }
        catch (Exception error)
        {
            try { await marker.Capability.CleanupFailedRollReservations(marker.Reservations); }
            catch (Exception cleanup)
            {
                // Report the original failed play, including cancellation,
                // even when a removal/notification also fails during cleanup.
                try { error.Data["HBRGOSakiMod.RollCleanupFailure"] = cleanup; }
                catch { }
            }
            throw;
        }
    }
}
