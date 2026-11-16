using System;
using System.Collections.Concurrent;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace HBRGOSakiMod.Core.Story;

internal static class HbrMemoryUpgradeStoryCatalog
{
    internal const string DirectoryPath = "res://story/memory_upgrades/";
    private sealed record Entry(HbrMemoryUpgradeStoryDefinition Definition);
    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    internal static bool TryFind(string actorId, HbrMemoryUpgradeStoryPerspective perspective, int level,
        out HbrMemoryUpgradeStorySegment segment)
    {
        segment = null;
        if (level is < 1 or > 7 || !HbrStoryCharacterCatalog.IsKnown(actorId)) return false;
        HbrMemoryUpgradeStoryDefinition definition = Entries.GetOrAdd(actorId, Read).Definition;
        return definition?.TryFind(perspective, level, out segment) == true;
    }
    private static Entry Read(string actorId)
    {
        string path = DirectoryPath + actorId + ".json";
        string json = ReadText(path);
        if (json == null) return new Entry(null);
        try
        {
            HbrMemoryUpgradeStoryDefinition definition = HbrMemoryUpgradeStoryDefinition.Parse(json, path);
            if (!string.Equals(definition.CharacterId, actorId, StringComparison.Ordinal))
                throw new InvalidOperationException("characterId must match the file name.");
            return new Entry(definition);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
        {
            Log.Warn("[HBRGOSakiMod] Skipping invalid upgrade memory " + path + ": " + error.Message);
            return new Entry(null);
        }
    }
    // One read per known actor, including misses. Never enumerate resource
    // folders, accept an arbitrary path, or log absent future story files.
    private static string ReadText(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }
}
