using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Map;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

// The native Begin method keeps votes, fake multiplayer decisions, events and
// awarding. Only selection leaves are intercepted while its saved scout scope
// is active; no command body or generic native entry is replaced.
[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), nameof(TreasureRoomRelicSynchronizer.BeginRelicPicking))]
internal static class ThirtyOneDScoutingTreasureBeginPatch
{
    private static void Prefix(TreasureRoomRelicSynchronizer __instance,
        out HbrThirtyOneDScoutingHelper.TreasurePickingScope __state) =>
        __state = HbrThirtyOneDScoutingHelper.BeginTreasurePicking(__instance);
    private static void Postfix(HbrThirtyOneDScoutingHelper.TreasurePickingScope __state) => __state?.Complete();
    private static void Finalizer(HbrThirtyOneDScoutingHelper.TreasurePickingScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldGenerateTreasure))]
internal static class ThirtyOneDScoutingTreasureEligibilityPatch
{
    private static void Postfix(Player player, bool __result) => HbrThirtyOneDScoutingHelper.ObserveTreasureEligibility(player, __result);
}

[HarmonyPatch(typeof(RelicFactory), nameof(RelicFactory.RollRarity), new[] { typeof(Rng) })]
internal static class ThirtyOneDScoutingTreasureRarityPatch
{
    private static bool Prefix(Rng rng, ref RelicRarity __result)
    {
        if (!HbrThirtyOneDScoutingHelper.TryScoutedTreasureRarity(rng, out RelicRarity rarity)) return true;
        __result = rarity; return false;
    }
}

[HarmonyPatch(typeof(TreasureRoomRelicSynchronizer), "TryGetRelicForTutorial")]
internal static class ThirtyOneDScoutingTreasureTutorialPatch
{
    // Tutorial eligibility is deliberately decided at actual room entry, when
    // path history is known. Native TryTutorial removes Gorget and awards it.
    private static void Postfix(TreasureRoomRelicSynchronizer __instance, RelicModel __result) =>
        HbrThirtyOneDScoutingHelper.ObserveTutorialTreasure(__instance, __result);
}

[HarmonyPatch(typeof(RelicGrabBag), nameof(RelicGrabBag.PullFromFront),
    new[] { typeof(RelicRarity), typeof(IRunState) })]
internal static class ThirtyOneDScoutingTreasurePullPatch
{
    private static bool Prefix(RelicGrabBag __instance, RelicRarity rarity, IRunState runState, ref RelicModel __result)
    {
        if (!HbrThirtyOneDScoutingHelper.TryTakeScoutedTreasure(__instance, rarity, runState, out RelicModel relic)) return true;
        __result = relic; return false;
    }
}

// A reserved ID remains in player-specific bags and may reappear in a refreshed
// native deque. Preserve the original eligibility predicate and exclude it on
// every filtered front/back draw, without mutating those other bags.
[HarmonyPatch]
internal static class ThirtyOneDScoutingReservedRelicFilterPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(RelicGrabBag).GetMethods()
        .Where(method => method.Name is nameof(RelicGrabBag.PullFromFront) or nameof(RelicGrabBag.PullFromBack))
        .Where(method => method.GetParameters().Length == 3 && method.GetParameters()[1].ParameterType == typeof(Func<RelicModel, bool>));
    private static void Prefix(ref Func<RelicModel, bool> filter, IRunState runState)
    {
        IReadOnlySet<string> reserved = HbrThirtyOneDScoutingHelper.GetReservedRelicIds(runState);
        if (reserved.Count == 0) return;
        Func<RelicModel, bool> original = filter;
        filter = relic => !reserved.Contains(relic.Id.Entry) && original(relic);
    }
}
