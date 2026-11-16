using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Story;

// A local presentation receipt, deliberately independent of run serialization,
// multiplayer state, Godot and the actual story definition/reader.
internal sealed class HbrMemoryGardenUpgradeStoryLocalFile
{
    private readonly record struct Segment(string Perspective, string Actor, int Level);
    private sealed class SegmentPayload
    {
        [JsonPropertyName("perspective")] public string Perspective { get; set; }
        [JsonPropertyName("actor")] public string Actor { get; set; }
        [JsonPropertyName("level")] public int Level { get; set; }
    }
    private sealed class Payload
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("seen")] public List<SegmentPayload> Seen { get; set; }
        [JsonPropertyName("runs")] public SortedDictionary<string, List<SegmentPayload>> Runs { get; set; }
    }

    private readonly string _path;
    private readonly Action<string> _logWarning;
    private readonly object _gate = new();
    private readonly HashSet<Segment> _seen = new();
    private readonly SortedDictionary<string, HashSet<Segment>> _runs = new(StringComparer.Ordinal);
    private bool _loaded, _loadFailed;

    internal HbrMemoryGardenUpgradeStoryLocalFile(string path, Action<string> logWarning = null)
    {
        _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        _logWarning = logWarning;
    }

    internal bool HasStarted(string runKey, string perspective, string actor, int level)
    {
        if (!ValidRunKey(runKey) || !TrySegment(perspective, actor, level, out Segment segment)) return false;
        lock (_gate)
            return Load() && _runs.TryGetValue(runKey, out var started) && started.Contains(segment);
    }

    // Exactly one caller can reserve a fresh chapter; play only after this
    // returns true, so an I/O failure cannot create an unrecorded playback.
    internal bool MarkStarted(string runKey, string perspective, string actor, int level)
    {
        if (!ValidRunKey(runKey) || !TrySegment(perspective, actor, level, out Segment segment)) return false;
        lock (_gate)
        {
            if (!Load()) return false;
            if (!_runs.TryGetValue(runKey, out var started)) _runs.Add(runKey, started = new());
            if (!started.Add(segment)) return false;
            if (Write()) return true;
            started.Remove(segment);
            if (started.Count == 0) _runs.Remove(runKey);
            return false;
        }
    }

    internal bool HasSeen(string perspective, string actor, int level)
    {
        if (!TrySegment(perspective, actor, level, out Segment segment)) return false;
        lock (_gate) return Load() && _seen.Contains(segment);
    }

    internal bool MarkSeen(string perspective, string actor, int level)
    {
        if (!TrySegment(perspective, actor, level, out Segment segment)) return false;
        lock (_gate)
        {
            if (!Load()) return false;
            if (!_seen.Add(segment)) return true;
            if (Write()) return true;
            _seen.Remove(segment);
            return false;
        }
    }

    private static bool ValidRunKey(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048;

    private static bool TrySegment(string perspective, string actor, int level, out Segment result)
    {
        result = default;
        if (level is < 1 or > 7 || string.IsNullOrWhiteSpace(perspective) || string.IsNullOrWhiteSpace(actor)) return false;
        string normalizedPerspective = perspective.Trim().ToLowerInvariant();
        if (normalizedPerspective is not ("31o" or "30a" or "hotaru") || actor.Length > 256) return false;
        result = new Segment(normalizedPerspective, actor.Trim().ToLowerInvariant(), level);
        return true;
    }

    private bool Load()
    {
        if (_loaded) return !_loadFailed;
        _loaded = true;
        try
        {
            if (!File.Exists(_path)) return true;
            Payload payload = JsonSerializer.Deserialize<Payload>(File.ReadAllText(_path));
            if (payload?.Version != 1) throw new JsonException("Unsupported or missing story receipt version.");
            AddSegments(_seen, payload.Seen);
            if (payload.Runs != null)
                foreach (var run in payload.Runs)
                {
                    if (!ValidRunKey(run.Key)) continue;
                    var segments = new HashSet<Segment>(); AddSegments(segments, run.Value);
                    if (segments.Count > 0) _runs.Add(run.Key, segments);
                }
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // Do not overwrite an unreadable file and erase its cross-run seen
            // history. A fresh application can retry after the user fixes it.
            _seen.Clear(); _runs.Clear(); _loadFailed = true;
            _logWarning?.Invoke("Cannot read upgrade-story receipts: " + error.Message);
            return false;
        }
    }

    private static void AddSegments(HashSet<Segment> destination, IEnumerable<SegmentPayload> values)
    {
        if (values == null) return;
        foreach (SegmentPayload value in values)
            if (value != null && TrySegment(value.Perspective, value.Actor, value.Level, out Segment segment))
                destination.Add(segment);
    }

    private static List<SegmentPayload> SaveSegments(IEnumerable<Segment> segments) => segments
        .OrderBy(value => value.Perspective, StringComparer.Ordinal)
        .ThenBy(value => value.Actor, StringComparer.Ordinal).ThenBy(value => value.Level)
        .Select(value => new SegmentPayload { Perspective = value.Perspective, Actor = value.Actor, Level = value.Level }).ToList();

    private bool Write()
    {
        string temporaryPath = _path + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var runs = new SortedDictionary<string, List<SegmentPayload>>(StringComparer.Ordinal);
            foreach (var run in _runs) runs.Add(run.Key, SaveSegments(run.Value));
            string json = JsonSerializer.Serialize(new Payload { Version = 1, Seen = SaveSegments(_seen), Runs = runs });
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logWarning?.Invoke("Cannot save upgrade-story receipts: " + error.Message);
            return false;
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { _logWarning?.Invoke("Cannot remove temporary upgrade-story receipt: " + error.Message); }
        }
    }
}
