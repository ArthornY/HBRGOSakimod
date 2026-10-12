using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Map;

internal static class MemoryFragmentMapHelper
{
    internal const int ExtraRows = 2;
    private const string FormatVersion = "v1";
    private static readonly AsyncLocal<IRunState> GenerationRun = new();
    private static readonly ConditionalWeakTable<ActMap, object> ConfiguredMaps = new();

    internal static bool IsEnabled(IRunState runState) => runState?.Players
        .Any(player => player.Character is HbrgoSaki31O or HbrgoSaki30A or HbrgoHotaru) == true;

    internal static bool IsMemoryPoint(IRunState runState, MapCoord coord) =>
        IsEnabled(runState) && GetMemoryCoords(runState, runState.CurrentActIndex).Contains(coord);

    internal static bool IsMemoryPoint(IRunState runState, MapPoint point) =>
        point != null && IsMemoryPoint(runState, point.coord);

    internal static bool IsCeremonyGatewayPoint(IRunState runState, MapCoord coord) =>
        IsEnabled(runState) && GetGatewayCoords(runState, runState.CurrentActIndex).Contains(coord);

    internal static bool IsCeremonyGatewayPoint(IRunState runState, MapPoint point) =>
        point != null && IsCeremonyGatewayPoint(runState, point.coord);

    internal static IReadOnlyList<MapCoord> GetMemoryCoords(IRunState runState, int actIndex) =>
        ReadMarkers(runState).Where(marker => marker.ActIndex == actIndex && !marker.IsGateway)
            .Select(marker => marker.Coord).ToArray();

    internal static IReadOnlyList<MapCoord> GetGatewayCoords(IRunState runState, int actIndex) =>
        ReadMarkers(runState).Where(marker => marker.ActIndex == actIndex && marker.IsGateway)
            .Select(marker => marker.Coord).ToArray();

    internal static IDisposable BeginGeneration(IRunState runState) => new GenerationScope(runState);

    internal static bool ShouldExtendAct(ActModel act) =>
        act != null && act.Index is >= 0 and <= 2 && IsEnabled(GenerationRun.Value);

    internal static void ConfigureMap(IRunState runState, ActMap map, int actIndex)
    {
        if (!IsEnabled(runState) || map == null || map is SavedActMap || actIndex is < 0 or > 2)
            return;

        ElectronicSoldierManual[] manuals = GetManuals(runState).ToArray();
        if (manuals.Length == 0) return;
        // A repeated lifecycle notification must not replace the saved markers
        // while leaving the first set of nonmodifiable points on the same map.
        if (ConfiguredMaps.TryGetValue(map, out _)) return;
        List<Marker> markers = ReadMarkers(runState).Where(marker => marker.ActIndex != actIndex).ToList();
        MapPoint[] points = map.GetAllMapPoints().OrderBy(point => point.coord.row)
            .ThenBy(point => point.coord.col).ToArray();

        // Every ordinary act-one route crosses row two. It stays a normal event
        // node; room routing ensures this first event is the joining ceremony.
        if (actIndex == 0)
        {
            foreach (MapPoint point in points.Where(point => point.coord.row == 2))
            {
                point.PointType = MapPointType.Unknown;
                point.CanBeModified = false;
                markers.Add(new Marker(actIndex, true, point.coord));
            }
        }

        // Native maps use quotas rather than point-by-point probabilities. Match
        // the random rest-site quota, leaving the fixed final campfire intact.
        int memoryCount = points.Count(point => point.PointType == MapPointType.RestSite &&
            point.CanBeModified && point.coord.row < map.GetRowCount() - 1);
        Rng rng = RngCompatibility.CreateNamed(runState.Rng, $"hbrgo_memory_fragment_map_v1_act_{actIndex + 1}");
        List<MapPoint> candidates = points.Where(point => point.CanBeModified && point.coord.row >= 3 &&
            point.coord.row < map.GetRowCount() - 1 &&
            point.PointType is MapPointType.Monster or MapPointType.Unknown).ToList();
        Shuffle(candidates, rng);
        // Prefer using the additional map space instead of removing ordinary ?
        // nodes. Stable ordering retains the shuffled order within each type.
        candidates = candidates.OrderBy(point => point.PointType == MapPointType.Monster ? 0 : 1).ToList();
        HashSet<MapPoint> selected = new();
        foreach (MapPoint candidate in candidates)
        {
            if (selected.Count >= memoryCount) break;
            if (candidate.parents.Concat(candidate.Children).Any(selected.Contains)) continue;
            selected.Add(candidate);
        }

        // Narrow or externally authored maps may not have enough nonadjacent
        // positions; maintain the quota where space exists without editing edges.
        foreach (MapPoint candidate in candidates)
        {
            if (selected.Count >= memoryCount) break;
            selected.Add(candidate);
        }

        foreach (MapPoint point in selected.OrderBy(point => point.coord.row).ThenBy(point => point.coord.col))
        {
            point.PointType = MapPointType.Unknown;
            point.CanBeModified = false;
            markers.Add(new Marker(actIndex, false, point.coord));
        }

        string data = Serialize(markers);
        foreach (ElectronicSoldierManual manual in manuals) manual.SetMemoryFragmentMapData(data);
        ConfiguredMaps.Add(map, new object());
    }

    private static IEnumerable<ElectronicSoldierManual> GetManuals(IRunState runState) =>
        runState?.Players.Where(player => player.Character is HbrgoSaki31O or HbrgoSaki30A or HbrgoHotaru)
            .SelectMany(player => player.Relics.OfType<ElectronicSoldierManual>()) ??
        Enumerable.Empty<ElectronicSoldierManual>();

    private static IReadOnlyList<Marker> ReadMarkers(IRunState runState)
    {
        string data = GetManuals(runState).Select(manual => manual.MemoryFragmentMapData)
            .FirstOrDefault(value => !string.IsNullOrEmpty(value));
        if (string.IsNullOrEmpty(data)) return Array.Empty<Marker>();
        string[] records = data.Split(';');
        if (records[0] != FormatVersion) return Array.Empty<Marker>();
        List<Marker> markers = new();
        foreach (string record in records.Skip(1))
        {
            string[] parts = record.Split(',');
            if (parts.Length != 4 || (parts[1] != "m" && parts[1] != "g") ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int act) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int col) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) ||
                act is < 0 or > 2 || col < 0 || row < 0) continue;
            markers.Add(new Marker(act, parts[1] == "g", new MapCoord { col = col, row = row }));
        }
        return markers.Distinct().ToArray();
    }

    private static string Serialize(IEnumerable<Marker> markers) => FormatVersion + string.Concat(markers
        .OrderBy(marker => marker.ActIndex).ThenBy(marker => marker.Coord.row).ThenBy(marker => marker.Coord.col)
        .Select(marker => FormattableString.Invariant(
            $";{marker.ActIndex},{(marker.IsGateway ? "g" : "m")},{marker.Coord.col},{marker.Coord.row}")));

    private static void Shuffle<T>(IList<T> items, Rng rng)
    {
        for (int index = items.Count - 1; index > 0; index--)
        {
            int other = rng.NextInt(0, index + 1);
            (items[index], items[other]) = (items[other], items[index]);
        }
    }

    private readonly record struct Marker(int ActIndex, bool IsGateway, MapCoord Coord);

    private sealed class GenerationScope : IDisposable
    {
        private readonly IRunState _previous;
        private bool _disposed;

        internal GenerationScope(IRunState runState)
        {
            _previous = GenerationRun.Value;
            GenerationRun.Value = runState;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GenerationRun.Value = _previous;
        }
    }
}
