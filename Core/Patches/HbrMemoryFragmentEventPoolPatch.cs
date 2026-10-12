using HarmonyLib;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyNextEvent))]
internal static class HbrMemoryFragmentEventPoolPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(IRunState runState, EventModel currentEvent, ref EventModel __result)
    {
        // One central roll per random event, shared deterministically by every peer.
        // A ceremony, quest, or other explicit replacement retains its priority.
        if (runState is not RunState state || __result == null || currentEvent == null
            || __result is AncientEventModel or JoiningCeremonyHbrgoSaki
            || __result.Id != currentEvent.Id) return;

        HbrStoryEvent memory = HbrMemoryFragmentPool.Choose(state);
        if (memory != null) __result = memory;
        // ActModel records only the final event in VisitedEventIds after this hook.
    }
}

[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.EnsureNextEventIsValid))]
internal static class HbrMemoryFragmentLegacyQueuePatch
{
    private static void Prefix(RoomSet __instance)
    {
        // Shared registration supplies Ritsu ownership, not a second draw route.
        // Filter both newly generated queues and saved queues before ordinary draws.
        HbrMemoryFragmentPoolRules.RemoveFromOrdinaryQueue(__instance.events,
            ref __instance.eventsVisited, HbrMemoryFragmentPool.IsMemoryFragment);
    }
}
