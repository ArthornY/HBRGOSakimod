using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HBRGOSakiMod.Core.Helpers;

// Data-only selection: UI randomness never consumes a gameplay RNG stream.
internal sealed record ThirtyOneACutinLine(string Key, string Character, string Name,
    string Text, string Voice, string[] Expressions, string Role, string Condition,
    bool Enabled, bool ReplyOnly);
internal sealed record ThirtyOneACutinReply(string FirstKey, ThirtyOneACutinLine Reply, bool Enabled);

internal sealed class ThirtyOneACutinLibrary
{
    internal IReadOnlyList<ThirtyOneACutinLine> Lines { get; }
    internal IReadOnlyList<ThirtyOneACutinReply> Replies { get; }

    private ThirtyOneACutinLibrary(List<ThirtyOneACutinLine> lines, List<ThirtyOneACutinReply> replies)
    { Lines = lines; Replies = replies; }

    private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static bool Flag(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    private static string[] Strings(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrEmpty(item)).ToArray() : [];

    internal static ThirtyOneACutinLibrary Parse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        List<ThirtyOneACutinLine> lines = [];
        foreach (JsonElement item in doc.RootElement.GetProperty("lines").EnumerateArray())
        {
            string condition = Text(item, "cond");
            lines.Add(new(Text(item, "k"), Text(item, "c"), Text(item, "n"), Text(item, "cn"), Text(item, "voice"),
                Strings(item, "ec"), Text(item, "role"), NormalizeCondition(Text(item, "conditionKey"), condition),
                string.IsNullOrEmpty(Text(item, "drop")) && (string.IsNullOrEmpty(condition) || Flag(item, "condActive")), Flag(item, "replyOnly")));
        }
        Dictionary<string, ThirtyOneACutinLine> byKey = lines.ToDictionary(line => line.Key, StringComparer.Ordinal);
        List<ThirtyOneACutinReply> replies = [];
        foreach (JsonElement chain in doc.RootElement.GetProperty("chains").EnumerateArray())
        {
            JsonElement[] steps = chain.GetProperty("steps").EnumerateArray().ToArray();
            if (steps.Length != 2) continue;
            JsonElement reply = steps[1];
            string key = Text(reply, "k");
            ThirtyOneACutinLine linked = byKey.GetValueOrDefault(key);
            string voice = Text(reply, "voice"), name = Text(reply, "n"), expr = Text(reply, "expr");
            string character = Text(reply, "c");
            if (string.IsNullOrEmpty(character)) character = CharacterOf(name, voice);
            string identity = string.IsNullOrEmpty(key) ? "reply:" + voice : key;
            var line = new ThirtyOneACutinLine(identity, character, name, Text(reply, "cn"), voice,
                string.IsNullOrEmpty(expr) ? linked?.Expressions ?? ["Superiority"] : [expr], linked?.Role ?? string.Empty,
                linked?.Condition ?? string.Empty, linked?.Enabled ?? true, true);
            replies.Add(new(Text(steps[0], "k"), line, Flag(chain, "def")));
        }
        return new(lines, replies);
    }

    private static string CharacterOf(string name, string voice) => name switch
    {
        "月歌" => "RKayamori", "由希" => "YIzumi", "惠" => "MAikawa", "司" => "TTojo",
        "玉" => "TKunimi", "可镰" => "Karen", "可怜" => "KAsakura", _ => string.Empty
    };

    internal ThirtyOneACutinLine Pick(string character, string role, bool live, Func<string, bool> condition, Random random)
    {
        ThirtyOneACutinLine[] candidates = Lines.Where(line => line.Character == character && line.Enabled && !line.ReplyOnly &&
            (string.IsNullOrEmpty(line.Condition) || condition(line.Condition)) &&
            (live ? line.Role == "Live" : line.Role != "Live") &&
            (role == null || string.IsNullOrEmpty(line.Role) || line.Role == role || line.Role == "平时")).ToArray();
        return Weighted(candidates, random);
    }

    internal ThirtyOneACutinLine PickReply(ThirtyOneACutinLine previous, ISet<string> used, Func<string, bool> condition, Random random, bool live = false)
    {
        if (used.Count >= 4) return null;
        ThirtyOneACutinLine[] candidates = Replies.Where(reply => reply.Enabled && reply.FirstKey == previous.Key &&
            reply.Reply.Enabled && !used.Contains(reply.Reply.Key) && (live || reply.Reply.Role != "Live") &&
            (string.IsNullOrEmpty(reply.Reply.Condition) || condition(reply.Reply.Condition))).Select(reply => reply.Reply).ToArray();
        return Weighted(candidates, random);
    }

    private static string NormalizeCondition(string key, string description)
    {
        if (!string.IsNullOrEmpty(key) || string.IsNullOrEmpty(description)) return key;
        if (description.Contains("模组里暂无", StringComparison.Ordinal)) return "unavailable-original-encounter";
        if (description.Contains("SkullFeather", StringComparison.Ordinal))
            return description.Contains("放大招之前", StringComparison.Ordinal) ? "skullfeather-high-boost" :
                description.Contains("核心被毁", StringComparison.Ordinal) ? "skullfeather-phase-two" : "skullfeather-present";
        if (description.Contains("FlatHand", StringComparison.Ordinal)) return "flathand-before-chain-break";
        if (description.Contains("各幕 Boss", StringComparison.Ordinal)) return "boss-opening";
        return "unknown";
    }

    private static ThirtyOneACutinLine Weighted(ThirtyOneACutinLine[] candidates, Random random)
    {
        int total = candidates.Sum(line => string.IsNullOrEmpty(line.Condition) ? 1 : 5);
        if (total == 0) return null;
        int roll = random.Next(total);
        foreach (ThirtyOneACutinLine line in candidates)
        {
            roll -= string.IsNullOrEmpty(line.Condition) ? 1 : 5;
            if (roll < 0) return line;
        }
        return candidates[^1];
    }
}
