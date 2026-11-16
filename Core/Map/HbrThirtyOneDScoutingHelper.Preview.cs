using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Map;

internal static partial class HbrThirtyOneDScoutingHelper
{
    private static HbrScoutedPoint[] PreviewAct(RunState run, ActMap map, int actIndex)
    {
        Rng rng = RngCompatibility.CreateNamed(run.Rng, $"hbrgo_31d_scout_v1_act_{actIndex + 1}");
        // Both dictionaries retain native insertion order, including disabled
        // negative odds and any base odds modified earlier in this run.
        var current = new Dictionary<RoomType, float>((Dictionary<RoomType, float>)
            AccessTools.Field(typeof(UnknownMapPointOdds), "_nonEventOdds").GetValue(run.Odds.UnknownMapPoint));
        var basis = new Dictionary<RoomType, float>((Dictionary<RoomType, float>)
            AccessTools.Field(typeof(UnknownMapPointOdds), "_baseOdds").GetValue(run.Odds.UnknownMapPoint));
        RoomSet queue = RoomSet.FromSave(run.Act.ToSave().SerializableRooms);
        HbrMemoryFragmentPoolRules.RemoveFromOrdinaryQueue(queue.events, ref queue.eventsVisited, HbrMemoryFragmentPool.IsMemoryFragment);
        HashSet<ModelId> virtualEvents = new();
        List<string> virtualMemories = new();
        List<HbrScoutedPoint> result = new();
        int unknownSeen = run.MapPointHistory.SelectMany(act => act).Count(point => point.MapPointType == MapPointType.Unknown);
        int currentRow = run.CurrentMapCoord?.row ?? -1;
        int gatewayRow = actIndex == 0 ? MemoryFragmentMapHelper.GetGatewayCoords(run, actIndex)
            .Select(coord => coord.row).DefaultIfEmpty(-1).Max() : -1;
        bool futureCeremony = gatewayRow >= 0 && currentRow < gatewayRow;
        if (futureCeremony)
        {
            // Every route crosses one mandatory ceremony node. Its native
            // event/unknown history will exist before later nodes are entered;
            // simulate it without actually visiting the ceremony early.
            queue.eventsVisited++;
            unknownSeen++;
        }
        foreach (MapPoint point in map.GetAllMapPoints().OrderBy(point => point.coord.row).ThenBy(point => point.coord.col))
        {
            if (point.coord.row <= currentRow || run.VisitedMapCoords.Contains(point.coord) ||
                MemoryFragmentMapHelper.IsCeremonyGatewayPoint(run, point)) continue;
            bool memoryPoint = MemoryFragmentMapHelper.IsMemoryPoint(run, point);
            if (!memoryPoint && point.PointType is not (MapPointType.Unknown or MapPointType.Treasure)) continue;
            RoomType type = point.PointType == MapPointType.Treasure ? RoomType.Treasure : RoomType.Event;
            if (!memoryPoint && point.PointType == MapPointType.Unknown)
            {
                HashSet<RoomType> blacklist = new();
                if (point.parents.Any(parent => parent.PointType == MapPointType.Shop || result.Any(saved =>
                        saved.Row == parent.coord.row && saved.Column == parent.coord.col && saved.RoomType == RoomType.Shop)) ||
                    point.Children.Count > 0 && point.Children.All(child => child.PointType == MapPointType.Shop))
                    blacklist.Add(RoomType.Shop);
                type = RollPreviewRoomType(run, current, basis, blacklist, rng, unknownSeen++);
            }
            EventModel selected = null;
            bool isMemory = false;
            if (type == RoomType.Event)
            {
                HbrStoryEvent memory = HbrMemoryFragmentPool.ChooseForScouting(run, memoryPoint, virtualMemories, rng,
                    futureCeremony && point.coord.row > gatewayRow);
                if (!memoryPoint)
                {
                    // Advance only the virtual queue. The actual queue and
                    // visited-event set remain untouched until a room is entered.
                    for (int attempt = 0; attempt < queue.events.Count; attempt++)
                    {
                        EventModel next = queue.NextEvent;
                        queue.eventsVisited++;
                        if (next.IsAllowed(run) && !run.VisitedEventIds.Contains(next.Id) && !virtualEvents.Contains(next.Id))
                        { selected = next; break; }
                    }
                }
                if (memory != null)
                {
                    selected = memory;
                    isMemory = true;
                    virtualMemories.Add(memory.Definition.Id);
                }
                else if (selected != null) virtualEvents.Add(selected.Id);
            }
            if (memoryPoint) unknownSeen++;
            HbrScoutedRelic[] relics = type == RoomType.Treasure ? ReserveTreasure(run, rng) : Array.Empty<HbrScoutedRelic>();
            foreach (HbrScoutedRelic relic in relics.Where(relic => relic.Reserved)) AddPendingReservation(run, relic.RelicId);
            result.Add(new HbrScoutedPoint
            {
                ActIndex = actIndex, Column = point.coord.col, Row = point.coord.row,
                OriginalType = point.PointType, RoomType = type, EventId = selected?.Id.Entry ?? "",
                IsMemory = isMemory, Relics = relics
            });
        }
        return result.ToArray();
    }

    // Mirrors native UnknownMapPointOdds.Roll, with explicit virtual first-run
    // history. Calling it directly with the real history would make every
    // unvisited preview a tutorial event. Stable/beta parity is tested against
    // their actual native method; only cloned dictionaries are adjusted here.
    internal static RoomType RollPreviewRoomType(RunState run, Dictionary<RoomType, float> odds,
        Dictionary<RoomType, float> basis, IEnumerable<RoomType> blacklist, Rng rng, int virtualUnknownSeen)
    {
        if (run.UnlockState.NumberOfRuns == 0 && virtualUnknownSeen < 3)
            return virtualUnknownSeen < 2 ? RoomType.Event : RoomType.Monster;
        IReadOnlySet<RoomType> allowed = Hook.ModifyUnknownMapPointRoomTypes(run,
            odds.Keys.Append(RoomType.Event).Except(blacklist).ToHashSet());
        RoomType chosen = allowed.Contains(RoomType.Event) ? RoomType.Event : allowed.Order().First();
        float roll = rng.NextFloat(), total = 0f;
        foreach (var pair in odds)
        {
            if (!allowed.Contains(pair.Key) || pair.Value < 0f) continue;
            total += pair.Value;
            if (roll <= total) { chosen = pair.Key; break; }
        }
        foreach (var pair in basis)
        {
            if (chosen == pair.Key) odds[pair.Key] = pair.Value;
            else if (allowed.Contains(pair.Key))
                odds[pair.Key] += Hook.ModifyOddsIncreaseForUnrolledRoomType(run, pair.Key, pair.Value);
        }
        return chosen;
    }
}
