#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Story;

/// <summary>Declarative instruction only. No script, callback, arbitrary property path or executable expression.</summary>
[JsonConverter(typeof(HbrStoryDirectionJsonConverter))]
public sealed class HbrStoryDirection
{
    public string Op { get; init; }
    public string Target { get; init; }
    public double? Duration { get; init; }
    public bool? NoWait { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? Scale { get; init; }
    public double? Opacity { get; init; }
    public double? EaseIn { get; init; }
    public double? EaseOut { get; init; }
    public double? Blur { get; init; }
    public double? DisappearDuration { get; init; }
    public double? Amplitude { get; init; }
    public double? AmplitudeX { get; init; }
    public double? AmplitudeY { get; init; }
    public string Position { get; init; }
    public double? Offset { get; init; }
    public double? Gray { get; init; }
    public double? Sepia { get; init; }
    public string Color { get; init; }
    public HbrStoryRgb Rgb { get; init; }
    public double? Intensity { get; init; }
    public string Scope { get; init; }
    public string Name { get; init; }
    public string Id { get; init; }
    public string Layer { get; init; }
    public string Character { get; init; }
    public string Outfit { get; init; }
    public string Expression { get; init; }
    public string Text { get; init; }
    public string Voice { get; init; }
    public string Cue { get; init; }
    public double? FadeOut { get; init; }
    public double? FadeIn { get; init; }
    public double? Volume { get; init; }
    public string Image { get; init; }
    public string Direction { get; init; }
    public string Motion { get; init; }
    public int? Index { get; init; }
    public string Bubble { get; init; }
    public bool? Enabled { get; init; }

    internal static readonly IReadOnlyDictionary<string, PropertyInfo> Properties = typeof(HbrStoryDirection)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), StringComparer.Ordinal);

    public object GetValue(string name) => Properties.TryGetValue(name, out var property) ? property.GetValue(this) : null;
    public double GetNumber(string name, double fallback = 0)
    {
        if (GetValue(name) is { } value) return Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture);
        if (Op=="bgm" && name=="fadeIn") return FadeOut ?? .5;
        if (Op=="swelling" && name=="disappearDuration") return Duration ?? .3;
        return HbrStoryDirectionCatalog.DefaultValue(Op,name) is { } defaultValue
            ? Convert.ToDouble(defaultValue,System.Globalization.CultureInfo.InvariantCulture) : fallback;
    }
    public string GetText(string name, string fallback = "") => GetValue(name) as string ??
        HbrStoryDirectionCatalog.DefaultValue(Op, name) as string ?? fallback;
    public bool GetBool(string name, bool fallback = false) => GetValue(name) is bool value ? value :
        HbrStoryDirectionCatalog.DefaultValue(Op, name) is bool defaultValue ? defaultValue : fallback;
}

public sealed record HbrStoryRgb(double R, double G, double B);

/// <summary>Enforces each operation's exact field set even if a disallowed known field is explicitly null.</summary>
public sealed class HbrStoryDirectionJsonConverter : JsonConverter<HbrStoryDirection>
{
    public override HbrStoryDirection Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("direction entries must be objects.");
        if (!root.TryGetProperty("op", out var operation) || operation.ValueKind != JsonValueKind.String)
            throw new JsonException("direction.op must be a known instruction.");
        var descriptor = HbrStoryDirectionCatalog.Find(operation.GetString()) ?? throw new JsonException("Unknown direction: " + operation.GetString());
        var result = new HbrStoryDirection();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in root.EnumerateObject())
        {
            if (!seen.Add(field.Name)) throw new JsonException("Duplicate direction field: " + field.Name);
            if (field.Name != "op" && !descriptor.Parameters.Any(p => p.Name == field.Name))
                throw new JsonException(descriptor.Op + " does not accept " + field.Name);
            if (!HbrStoryDirection.Properties.TryGetValue(field.Name, out var property))
                throw new JsonException("Unknown direction field: " + field.Name);
            if (field.Value.ValueKind == JsonValueKind.Null) throw new JsonException("Omit optional direction fields instead of null: " + field.Name);
            if (field.Name == "rgb" && (field.Value.ValueKind != JsonValueKind.Object ||
                field.Value.EnumerateObject().Count() != 3 || new[] { "r", "g", "b" }.Any(name =>
                    !field.Value.TryGetProperty(name, out var component) || component.ValueKind != JsonValueKind.Number)))
                throw new JsonException("rgb must contain exactly r, g and b numbers.");
            property.SetValue(result, JsonSerializer.Deserialize(field.Value.GetRawText(), property.PropertyType, options));
        }
        HbrStoryDirectionCatalog.Validate(result, "direction");
        return result;
    }

    public override void Write(Utf8JsonWriter writer, HbrStoryDirection value, JsonSerializerOptions options)
    {
        HbrStoryDirectionCatalog.Validate(value, "direction");
        writer.WriteStartObject();
        foreach (var pair in HbrStoryDirection.Properties)
        {
            object field = pair.Value.GetValue(value);
            if (field == null) continue;
            writer.WritePropertyName(pair.Key);
            JsonSerializer.Serialize(writer, field, pair.Value.PropertyType, options);
        }
        writer.WriteEndObject();
    }
}
