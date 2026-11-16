using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Story;

public enum HbrMemoryUpgradeStoryPerspective { ThirtyOneO, ThirtyA, Hotaru }

public static class HbrMemoryUpgradeStoryPerspectives
{
    public static string Key(HbrMemoryUpgradeStoryPerspective perspective) => perspective switch
    {
        HbrMemoryUpgradeStoryPerspective.ThirtyOneO => "31o",
        HbrMemoryUpgradeStoryPerspective.ThirtyA => "30a",
        HbrMemoryUpgradeStoryPerspective.Hotaru => "hotaru",
        _ => throw new ArgumentOutOfRangeException(nameof(perspective))
    };
    public static bool TryParse(string value, out HbrMemoryUpgradeStoryPerspective perspective)
    {
        perspective = default;
        switch (value?.ToLowerInvariant())
        {
            case "31o": perspective = HbrMemoryUpgradeStoryPerspective.ThirtyOneO; return true;
            case "30a": perspective = HbrMemoryUpgradeStoryPerspective.ThirtyA; return true;
            case "hotaru": perspective = HbrMemoryUpgradeStoryPerspective.Hotaru; return true;
            default: return false;
        }
    }
}

// Deliberately contains no choices, rewards, encounters or executable callbacks.
public sealed class HbrMemoryUpgradeStoryDefinition
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public required int SchemaVersion { get; init; }
    public required string CharacterId { get; init; }
    public required HbrStoryPresentation Presentation { get; init; }
    public required IReadOnlyList<HbrMemoryUpgradeStorySegment> Segments { get; init; }

    public static HbrMemoryUpgradeStoryDefinition Parse(string json, string source = "upgrade memory")
    {
        using JsonDocument document = JsonDocument.Parse(json);
        RejectDuplicates(document.RootElement, source);
        var definition = JsonSerializer.Deserialize<HbrMemoryUpgradeStoryDefinition>(json, Options)
            ?? throw new InvalidOperationException(source + ": missing story object.");
        definition.Validate(source);
        return definition;
    }
    private static void RejectDuplicates(JsonElement value, string source)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidOperationException(source + ": duplicate property " + property.Name);
                RejectDuplicates(property.Value, source + "." + property.Name);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) RejectDuplicates(item, source);
    }
    public void Validate(string source = "upgrade memory")
    {
        if (SchemaVersion != 1 || !HbrStoryCharacterCatalog.IsKnown(CharacterId) || Presentation == null || Segments == null)
            throw new InvalidOperationException(source + ": schemaVersion=1, known characterId, presentation and segments are required.");
        var claimed = new HashSet<(int Level, HbrMemoryUpgradeStoryPerspective Perspective)>();
        foreach (HbrMemoryUpgradeStorySegment segment in Segments)
        {
            if (segment == null || segment.Level is < 1 or > 7 || segment.Perspectives == null || segment.Perspectives.Count == 0 || segment.Lines == null)
                throw new InvalidOperationException(source + ": segment requires level1–7, perspectives and lines.");
            foreach (string name in segment.Perspectives)
                if (!HbrMemoryUpgradeStoryPerspectives.TryParse(name, out var perspective) || !claimed.Add((segment.Level, perspective)))
                    throw new InvalidOperationException(source + ": unknown or overlapping segment perspective at level " + segment.Level);
            segment.EffectivePresentation = segment.Presentation ?? Presentation;
            // Reuse the real, pure line/image/layout validator without creating
            // an EventModel, entering a page or granting met/reward state.
            new HbrStoryDefinition
            {
                SchemaVersion = 1, Id = "UPGRADE_MEMORY_" + CharacterId.ToUpperInvariant(), Title = CharacterId,
                Conditions = new HbrStoryConditions { PartyCharacters = Array.Empty<string>(), Acts = Array.Empty<int>(), OncePerRun = false, AfterEventIds = Array.Empty<string>() },
                Presentation = segment.EffectivePresentation, StartPage = "MEMORY", Pages = new[] { segment.Page }
            }.Validate(source);
        }
    }
    public bool TryFind(HbrMemoryUpgradeStoryPerspective perspective, int level, out HbrMemoryUpgradeStorySegment segment)
    {
        segment = Segments.FirstOrDefault(candidate => candidate.Level == level && candidate.Lines.Count > 0 &&
            candidate.Perspectives.Any(value => HbrMemoryUpgradeStoryPerspectives.TryParse(value, out var parsed) && parsed == perspective));
        return segment != null;
    }
}

public sealed class HbrMemoryUpgradeStorySegment
{
    public required int Level { get; init; }
    public required IReadOnlyList<string> Perspectives { get; init; }
    public HbrStoryPresentation Presentation { get; init; }
    public required IReadOnlyList<HbrStoryLine> Lines { get; init; }
    [JsonIgnore] public HbrStoryPresentation EffectivePresentation { get; internal set; }
    [JsonIgnore] public HbrStoryPage Page => new() { Id = "MEMORY", Lines = Lines, Choices = Array.Empty<HbrStoryChoice>() };
    [JsonIgnore] public IEnumerable<string> AssetPaths => HbrStoryResources.Collect(EffectivePresentation, Lines)
        .Where(resource => resource.Kind == HbrStoryResourceKind.Image).Select(resource => resource.Key).Distinct(StringComparer.Ordinal);
}
