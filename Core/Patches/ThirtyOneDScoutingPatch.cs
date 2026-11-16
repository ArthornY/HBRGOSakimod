using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Map;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
internal static class ThirtyOneDScoutedRoomTypePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(RunManager __instance, MapPointType pointType,
        IEnumerable<RoomType> blacklist, ref RoomType __result)
    {
        if (!HbrThirtyOneDScoutingHelper.TryRoomType(__instance.DebugOnlyGetState(), pointType, blacklist, out RoomType type)) return true;
        __result = type;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "CreateRoom")]
internal static class ThirtyOneDScoutedEventRoomPatch
{
    // Run before the dedicated-memory random draw; explicit save/quest models
    // still retain native priority and never enter this replacement.
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(RunManager __instance, RoomType roomType, MapPointType mapPointType,
        AbstractModel model, ref AbstractRoom __result)
    {
        if (model != null || roomType != RoomType.Event || mapPointType != MapPointType.Unknown ||
            !HbrThirtyOneDScoutingHelper.TryEvent(__instance.DebugOnlyGetState(), out EventModel selected)) return true;
        __result = new EventRoom(selected);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), "AfterMapLocationChanged")]
internal static class ThirtyOneDScoutedLocationPatch
{
    private static void Postfix(RunManager __instance) =>
        HbrThirtyOneDScoutingHelper.EnsureScouted(__instance.DebugOnlyGetState());
}
