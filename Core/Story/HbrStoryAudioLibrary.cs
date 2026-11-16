#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.Story;

public sealed record HbrStoryAudioCue
{
    public required string Path { get; init; }
    public required int SampleRate { get; init; }
    public required int Channels { get; init; }
    public required long TotalSamples { get; init; }
    public long? LoopStart { get; init; }
    public long? LoopEnd { get; init; }
    public long? SourceTotalSamples { get; init; }
    public long? SourceLoopEnd { get; init; }
    public required string SourceSha256 { get; init; }
    public string? OggSha256 { get; init; }
    public long? Bytes { get; init; }
    public string? Method { get; init; }
    public string? DecoderSha256 { get; init; }
    public double? WaveformCorrelation { get; init; }
    public long? DecodedSamples { get; init; }
    public string? EncoderVersion { get; init; }
    public string? ScriptSha256 { get; init; }
}

// Pure import/runtime contract: OGG looping uses the native file-end boundary,
// so the importer retains the intro and crops only the unused HCA loop tail.
public sealed class HbrStoryAudioLibrary
{
    public int SchemaVersion { get; init; } = 1;
    public Dictionary<string, HbrStoryAudioCue> Cues { get; init; } = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static bool IsCueName(string? name) => name != null && Regex.IsMatch(name, @"\A[A-Za-z0-9_]{1,256}\z", RegexOptions.CultureInvariant);
    public static bool IsAudioPath(string? path) => path != null &&
        (path.StartsWith("res://sounds/story/", StringComparison.Ordinal) || path.StartsWith("res://sounds/thirty_one_a_cutin/", StringComparison.Ordinal)) &&
        path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && !path.Contains('\\') &&
        !path[6..].Contains(':') && !Array.Exists(path[6..].Split('/'), p => p is "" or "." or "..") &&
        !Array.Exists(path.ToCharArray(), char.IsControl);

    public static HbrStoryAudioLibrary Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        RejectDuplicates(document.RootElement);
        HbrStoryAudioLibrary library = JsonSerializer.Deserialize<HbrStoryAudioLibrary>(json, Options)
            ?? throw new InvalidOperationException("Missing audio library.");
        if (library.SchemaVersion != 1 || library.Cues == null || library.Cues.Count > 4096)
            throw new InvalidOperationException("Unsupported audio library schema or size.");
        foreach (var (name, cue) in library.Cues)
        {
            if (!IsCueName(name) || cue == null || !IsAudioPath(cue.Path) || cue.SampleRate is < 8000 or > 192000 ||
                cue.Channels is < 1 or > 2 || cue.TotalSamples <= 0 || cue.TotalSamples > cue.SampleRate * 60L * 60 ||
                !Regex.IsMatch(cue.SourceSha256 ?? "", @"\A[A-Fa-f0-9]{64}\z", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Invalid audio cue: " + name);
            if (cue.LoopStart.HasValue != cue.LoopEnd.HasValue || cue.LoopStart < 0 || cue.LoopEnd <= cue.LoopStart ||
                cue.LoopEnd.HasValue && cue.LoopEnd != cue.TotalSamples)
                throw new InvalidOperationException("OGG loop must end at encoded totalSamples: " + name);
            if (cue.SourceTotalSamples < cue.TotalSamples || cue.SourceLoopEnd.HasValue && cue.SourceLoopEnd != cue.LoopEnd)
                throw new InvalidOperationException("Inconsistent source sample provenance: " + name);
        }
        return library;
    }
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new InvalidOperationException("Duplicate audio field: " + property.Name);
                RejectDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) RejectDuplicates(child);
    }
    public static float EffectiveGain(float gain, float master, float group)
    {
        if (!float.IsFinite(gain) || !float.IsFinite(master) || !float.IsFinite(group)) return 0;
        master = Math.Clamp(master, 0, 1); group = Math.Clamp(group, 0, 1);
        return Math.Max(gain, 0) * master * master * group * group;
    }
}

// One envelope per playing channel; seek/fast-forward replaces the old fade
// without restarting its stream or letting a stale tween pull the gain back.
public sealed class HbrStoryAudioEnvelope
{
    private float _from, _to;
    private double _elapsed, _duration;
    public float Gain { get; private set; }
    public HbrStoryAudioEnvelope(float gain) { Gain = _from = _to = Normalize(gain); }
    public void FadeTo(float gain, double seconds)
    {
        _from = Gain; _to = Normalize(gain); _elapsed = 0;
        _duration = double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 120) : 0;
        if (_duration == 0) Gain = _to;
    }
    public bool Tick(double seconds)
    {
        if (_duration == 0) return true;
        if (double.IsFinite(seconds)) _elapsed += Math.Max(0, seconds);
        float fraction = (float)Math.Clamp(_elapsed / _duration, 0, 1);
        Gain = _from + (_to - _from) * fraction;
        if (fraction < 1) return false;
        _duration = 0; return true;
    }
    private static float Normalize(float gain) => float.IsFinite(gain) ? Math.Clamp(gain, 0, 4) : 0;
}
