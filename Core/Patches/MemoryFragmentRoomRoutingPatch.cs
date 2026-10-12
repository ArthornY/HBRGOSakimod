using HarmonyLib;
using HBRGOSakiMod.Core.Map;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
internal static class MemoryFragmentRoomTypePatch
{
    private static bool Prefix(RunManager __instance, MapPointType pointType, ref RoomType __result)
    {
        RunState state = __instance.DebugOnlyGetState();
        if (pointType != MapPointType.Unknown || state?.CurrentMapCoord is not { } coord ||
            (!MemoryFragmentMapHelper.IsMemoryPoint(state, coord) &&
             !MemoryFragmentMapHelper.IsCeremonyGatewayPoint(state, coord)))
            return true;

        // The gateway still uses normal event selection and its ceremony hook.
        // Neither dedicated kind consumes unknown-room odds or their RNG.
        __result = RoomType.Event;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class MemoryFragmentCreateRoomPatch
{
    private static bool Prefix(RunManager __instance, RoomType roomType, MapPointType mapPointType,
        AbstractModel model, ref AbstractRoom __result)
    {
        RunState state = __instance.DebugOnlyGetState();
        // Explicit/restored models keep their original identity and never reroll.
        if (model != null || roomType != RoomType.Event || mapPointType != MapPointType.Unknown ||
            state?.CurrentMapCoord is not { } coord || !MemoryFragmentMapHelper.IsMemoryPoint(state, coord))
            return true;

        HbrStoryEvent memory = HbrMemoryFragmentPool.ChooseForMemoryNode(state);
        if (memory == null)
        {
            // All shipped fragments remain replayable after the mandatory first
            // ceremony. Keep edited stories with impossible conditions from trapping a run.
            Log.Warn("[HBRGO] Memory node has no eligible story, including replays; using an ordinary event.");
            return true;
        }

        state.AddVisitedEvent(memory);
        __result = new EventRoom(memory);
        return false;
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.MarkRoomVisited))]
internal static class MemoryFragmentOrdinaryQueuePatch
{
    private static bool Prefix(ActModel __instance, RoomType roomType)
    {
        RunState state = RunManager.Instance?.DebugOnlyGetState();
        // Dedicated draws neither read nor advance the ordinary event queue.
        // The gateway and ordinary 30% rolls retain vanilla bookkeeping.
        return roomType != RoomType.Event || state == null || state.Act != __instance ||
            state.CurrentMapCoord is not { } coord || !MemoryFragmentMapHelper.IsMemoryPoint(state, coord) ||
            state.CurrentRoom is not EventRoom { CanonicalEvent: HbrStoryEvent story } ||
            !HbrMemoryFragmentPool.IsMemoryFragment(story);
    }
}
