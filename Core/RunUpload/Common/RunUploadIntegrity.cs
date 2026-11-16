using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace HBRGOSakiMod.Core.RunUpload;

internal sealed record RunModInfo(string Id, bool AffectsGameplay);

// run.integrity (schema-v1.md 3.2): known modifier labels and two flags.
// Never a mod name or a mod list.
internal sealed record RunIntegrity(IReadOnlyList<string> Modifiers, bool ConsoleUsed, bool OtherGameplayMods)
{
    internal static readonly RunIntegrity Clean = new(Array.Empty<string>(), false, false);

    internal bool HasModifiers => Modifiers.Count > 0;

    // From the mods loaded in this session.
    internal static RunIntegrity FromMods(IEnumerable<RunModInfo> mods)
    {
        var modifiers = new SortedSet<string>(StringComparer.Ordinal);
        bool other = false;
        foreach (RunModInfo mod in mods ?? Enumerable.Empty<RunModInfo>())
        {
            if (string.IsNullOrEmpty(mod?.Id)) continue;
            string label = ModifierLabel(mod.Id);
            if (label != null)
            {
                modifiers.Add(label);
                continue;
            }
            if (mod.AffectsGameplay && !RunUploadConstants.OwnModIds.Any(own =>
                    string.Equals(own, mod.Id, StringComparison.OrdinalIgnoreCase)))
                other = true;
        }
        return new RunIntegrity(modifiers.ToList(), false, other);
    }

    internal static string ModifierLabel(string modId) => RunUploadConstants.KnownModifiers
        .FirstOrDefault(entry => entry.ModIds.Any(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase)))
        .Label;

    internal JsonObject ToJson() => new()
    {
        ["modifiers"] = RunRecordBuilder.Strings(Modifiers),
        ["consoleUsed"] = ConsoleUsed,
        ["otherGameplayMods"] = OtherGameplayMods
    };
}

// Which developer console commands exclude the run in progress (schema-v1.md 3.2).
internal static class RunUploadConsole
{
    // True unless the name is one of the game's harmless commands. A missing
    // name counts too: only a known command is let through.
    internal static bool ChangesRun(string commandName)
    {
        string name = commandName?.Trim();
        return string.IsNullOrEmpty(name) || !RunUploadConstants.HarmlessConsoleCommands.Any(harmless =>
            string.Equals(harmless, name, StringComparison.OrdinalIgnoreCase));
    }
}

internal static class RunUploadFilter
{
    // null when the run may be uploaded; otherwise why it is dropped
    // (schema-v1.md 3.2). Dropped runs are not kept on this computer either.
    internal static string RejectReason(JsonObject run)
    {
        if (run == null) return "missing";
        JsonObject integrity = run["integrity"] as JsonObject;
        if (integrity?["modifiers"] is JsonArray { Count: > 0 }) return "modifier";
        if (integrity?["consoleUsed"] is JsonValue console && console.TryGetValue(out bool used) && used) return "console";
        string outcome = run["outcome"] is JsonValue o && o.TryGetValue(out string text) ? text : null;
        int floor = run["floorReached"] is JsonValue f && f.TryGetValue(out int value) ? value : 0;
        if (outcome is "loss" or "abandoned" && floor <= RunUploadConstants.EarlyEndFloors) return "early-end";
        return null;
    }
}
