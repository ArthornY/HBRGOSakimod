using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Patches;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Map;

// All readers use the saved ledger. Rendering never rolls, reserves a reward,
// writes visited history, or advances a native random stream.
internal static partial class HbrThirtyOneDScoutingHelper
{
    private sealed class Cache
    {
        internal string Data = "";
        internal HbrScoutingState State = new();
        internal bool Generating;
        internal readonly HashSet<string> Pending = new(StringComparer.Ordinal);
        internal HbrScoutingState ReservedState;
        internal IReadOnlySet<string> Reserved = new HashSet<string>();
    }
    private static readonly ConditionalWeakTable<IRunState, Cache> Caches = new();
    private static IEnumerable<ThirtyOneDCollection> Collections(IRunState run) => run?.Players
        .OrderBy(player => player.NetId).Select(player => player.GetRelic<ThirtyOneDCollection>())
        .Where(relic => relic is { IsMutable: true }) ?? Enumerable.Empty<ThirtyOneDCollection>();

    private static HbrScoutingState Read(IRunState run)
    {
        if (run == null) return new();
        Cache cache = Caches.GetOrCreateValue(run);
        string data = Collections(run).Select(relic => relic.ScoutingData).FirstOrDefault(text => !string.IsNullOrEmpty(text)) ?? "";
        if (data == cache.Data) return cache.State;
        HbrScoutingState best = new();
        foreach (string candidate in Collections(run).Select(relic => relic.ScoutingData).Where(text => !string.IsNullOrEmpty(text)).Distinct())
        {
            try
            {
                HbrScoutingState parsed = JsonSerializer.Deserialize<HbrScoutingState>(candidate);
                if (parsed is { Schema: 1, Acts: not null } && parsed.Revision >= best.Revision)
                { best = parsed; data = candidate; }
            }
            catch (JsonException) { }
        }
        cache.Data = data;
        return cache.State = best;
    }

    private static void Store(RunState run, HbrScoutingState state)
    {
        string data = JsonSerializer.Serialize(state with { Revision = checked(state.Revision + 1) });
        var owners = Collections(run).Select(relic => (Relic: relic, Previous: relic.ScoutingData)).ToArray();
        try { foreach (var owner in owners) owner.Relic.SetScoutingData(data); }
        catch
        {
            foreach (var owner in owners)
                try { owner.Relic.SetScoutingData(owner.Previous); } catch { }
            throw;
        }
        Cache cache = Caches.GetOrCreateValue(run);
        cache.Data = data;
        cache.State = state with { Revision = state.Revision + 1 };
        ThirtyOneDScoutingMapUi.Refresh(run);
    }

    internal static int GetRevision(IRunState run) => Read(run).Revision;
    internal static bool CanView(Player player) => player?.RunState is { } run && HbrBattleStatsHelper.IsEnabled(player) &&
        Read(run).Acts.Any(act => act.ActIndex == run.CurrentActIndex && act.Viewers.Contains(player.NetId));
    internal static HbrScoutedPoint GetKnownPoint(IRunState run, MapPoint point) => point == null || run == null ? null :
        Read(run).Acts.FirstOrDefault(act => act.ActIndex == run.CurrentActIndex)?.Points.FirstOrDefault(saved =>
            saved.Column == point.coord.col && saved.Row == point.coord.row && !saved.Invalidated && !saved.Released &&
            saved.OriginalType == point.PointType);
    internal static EventModel ResolveEvent(HbrScoutedPoint point) => string.IsNullOrEmpty(point?.EventId) ? null :
        ModelDb.GetByIdOrNull<EventModel>(new ModelId("EVENT", point.EventId));
    internal static RelicModel ResolveRelic(HbrScoutedRelic relic) => string.IsNullOrEmpty(relic?.RelicId) ? null :
        ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", relic.RelicId));

    internal static IReadOnlySet<string> GetReservedRelicIds(IRunState run)
    {
        if (run == null) return new HashSet<string>();
        HbrScoutingState state = Read(run);
        Cache cache = Caches.GetOrCreateValue(run);
        if (!ReferenceEquals(cache.ReservedState, state))
        {
            cache.Reserved = state.Acts.SelectMany(act => act.Points)
                .Where(point => !point.TreasureOpened && !point.Released && !point.Invalidated)
                .SelectMany(point => point.Relics).Where(relic => relic.Reserved).Select(relic => relic.RelicId)
                .ToHashSet(StringComparer.Ordinal);
            cache.ReservedState = state;
        }
        return cache.Pending.Count == 0 ? cache.Reserved : cache.Reserved.Concat(cache.Pending).ToHashSet(StringComparer.Ordinal);
    }
    internal static void AddPendingReservation(IRunState run, string id)
    { if (run != null && !string.IsNullOrEmpty(id)) Caches.GetOrCreateValue(run).Pending.Add(id); }

    private static HbrScoutingState Replace(HbrScoutingState state, HbrScoutedPoint point) => state with
    {
        Acts = state.Acts.Select(act => act.ActIndex != point.ActIndex ? act : act with
        { Points = act.Points.Select(old => old.Column == point.Column && old.Row == point.Row ? point : old).ToArray() }).ToArray()
    };
    internal static void MarkTreasureOpened(RunState run, HbrScoutedPoint point)
    { if (point != null && !point.TreasureOpened) Store(run, Replace(Read(run), point with { TreasureOpened = true })); }

    internal static void EnsureScouted(IRunState run, ActMap generatedMap = null, int? generatedAct = null)
    {
        if (run is not RunState state || !MemoryFragmentMapHelper.IsEnabled(state)) return;
        Cache cache = Caches.GetOrCreateValue(run);
        if (cache.Generating) return;
        HbrScoutingState ledger = Read(run);
        int actIndex = generatedAct ?? state.CurrentActIndex;
        if (actIndex != state.CurrentActIndex) return;
        ActMap map = generatedMap ?? state.Map;
        if (map == null || map is NullActMap) return;
        ulong[] eligible = state.Players.Where(player => HbrBattleStatsHelper.IsEnabled(player) &&
            ThirtyOneDKeywordRules.GetPermanentDistinctCount(player) >= 4 && player.GetRelic<ThirtyOneDCollection>() != null)
            .Select(player => player.NetId).Order().ToArray();
        HbrScoutedAct existing = ledger.Acts.FirstOrDefault(act => act.ActIndex == actIndex);
        cache.Generating = true;
        try
        {
            HbrScoutingState released = ReleasePassed(state, ledger, map);
            if (!ReferenceEquals(released, ledger)) { Store(state, released); ledger = Read(run); }
            if (existing != null)
            {
                ulong[] viewers = existing.Viewers.Concat(eligible).Distinct().Order().ToArray();
                if (!viewers.SequenceEqual(existing.Viewers))
                    ledger = ledger with { Acts = ledger.Acts.Select(act => act.ActIndex == actIndex ? act with { Viewers = viewers } : act).ToArray() };
                if (!ReferenceEquals(ledger, Read(run))) Store(state, ledger);
                return;
            }
            if (eligible.Length == 0)
            { if (!ReferenceEquals(ledger, Read(run))) Store(state, ledger); return; }
            Action undoBag = SnapshotTreasureBag(state);
            try
            {
                HbrScoutedPoint[] points = PreviewAct(state, map, actIndex);
                Store(state, ledger with { Acts = ledger.Acts.Append(new HbrScoutedAct
                { ActIndex = actIndex, Viewers = eligible, Points = points }).ToArray() });
            }
            catch
            {
                undoBag();
                throw;
            }
        }
        finally { cache.Pending.Clear(); cache.Generating = false; }
    }

    private static HbrScoutingState ReleasePassed(RunState run, HbrScoutingState ledger, ActMap map)
    {
        int row = run.CurrentMapCoord?.row ?? -1;
        bool changed = false;
        var acts = ledger.Acts.Select(act => act with { Points = act.Points.Select(point =>
        {
            if (point.Released || point.Invalidated || point.TreasureOpened) return point;
            MapPoint actual = act.ActIndex == run.CurrentActIndex ? map.GetPoint(new MapCoord { col = point.Column, row = point.Row }) : null;
            bool invalid = act.ActIndex == run.CurrentActIndex && (actual == null || actual.PointType != point.OriginalType);
            bool passed = act.ActIndex < run.CurrentActIndex || act.ActIndex == run.CurrentActIndex && point.Row < row;
            if (!invalid && !passed) return point;
            ReturnTreasure(run, point);
            changed = true;
            return point with { Released = passed, Invalidated = invalid };
        }).ToArray() }).ToArray();
        return changed ? ledger with { Acts = acts } : ledger;
    }

    internal static bool TryRoomType(RunState run, MapPointType mapType, IEnumerable<RoomType> blacklist, out RoomType room)
    {
        room = RoomType.Unassigned;
        HbrScoutedPoint point = GetKnownPoint(run, run?.CurrentMapPoint);
        if (point == null || mapType != point.OriginalType)
        {
            // UI queries are read-only. Reconcile a changed real node at the
            // authoritative entry boundary and release any old reservation.
            if (run?.CurrentMapPoint is { } actual)
            {
                HbrScoutedPoint obsolete = Read(run).Acts.FirstOrDefault(act => act.ActIndex == run.CurrentActIndex)?.Points
                    .FirstOrDefault(saved => saved.Column == actual.coord.col && saved.Row == actual.coord.row &&
                        !saved.Invalidated && !saved.Released && saved.OriginalType != actual.PointType);
                if (obsolete != null) Invalidate(run, obsolete);
            }
            return false;
        }
        if (mapType == MapPointType.Unknown && !MemoryFragmentMapHelper.IsMemoryPoint(run, run.CurrentMapPoint))
        {
            var allowed = Hook.ModifyUnknownMapPointRoomTypes(run, new HashSet<RoomType>
                { RoomType.Monster, RoomType.Elite, RoomType.Treasure, RoomType.Shop, RoomType.Event }.Except(blacklist).ToHashSet());
            if (!allowed.Contains(point.RoomType)) { Invalidate(run, point); return false; }
        }
        room = point.RoomType;
        return true;
    }
    internal static bool TryEvent(RunState run, out EventModel selected)
    {
        selected = null;
        HbrScoutedPoint point = GetKnownPoint(run, run?.CurrentMapPoint);
        if (point?.RoomType != RoomType.Event) return false;
        EventModel canonical = ResolveEvent(point);
        bool allowed = canonical is HbrStoryEvent story && point.IsMemory
            ? story.IsAllowedForStoryPool(run, allowReplay: true)
            : canonical != null && canonical.IsAllowed(run) && !run.VisitedEventIds.Contains(canonical.Id);
        if (!allowed) { Invalidate(run, point); return false; }
        run.AddVisitedEvent(canonical);
        selected = canonical;
        return true;
    }
    private static void Invalidate(RunState run, HbrScoutedPoint point)
    {
        ReturnTreasure(run, point);
        Store(run, Replace(Read(run), point with { Invalidated = true }));
    }
}
