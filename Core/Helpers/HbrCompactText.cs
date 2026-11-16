using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Swaps listed localization entries for their short hover versions while a
/// compact hover is being built. Outside a scope every text is formatted as
/// written, so menus, the manual, rewards and inspect screens keep the full
/// rules. The short text is formatted with the full text's own variables.
/// </summary>
internal static class HbrCompactText
{
    [ThreadStatic] private static int _depth;
    [ThreadStatic] private static int _suspended;
    [ThreadStatic] private static int _swapped;

    private static readonly IReadOnlyDictionary<string, HashSet<string>> Entries =
        HbrCompactTextKeys.Entries.ToDictionary(pair => pair.Key,
            pair => new HashSet<string>(pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    internal static bool IsActive => _depth > 0 && _suspended == 0;

    /// <summary>Number of texts swapped since the outermost scope began.</summary>
    internal static int Swapped => _swapped;

    /// <summary>Short hover texts exist for the current language (Chinese for now).</summary>
    internal static bool IsAvailable => LocString.Exists("card_keywords", HbrCompactTextKeys.UiPrefix + "holdKeyboard");

    internal static Scope Enter()
    {
        if (_depth++ == 0)
        {
            _swapped = 0;
            _suspended = 0;
        }
        return new Scope();
    }

    /// <summary>
    /// Text built here may be cached for later full-text use (keyword
    /// tooltips are), so it is built with the full text.
    /// </summary>
    internal static bool Suspend()
    {
        if (_depth == 0) return false;
        _suspended++;
        return true;
    }

    internal static void Resume()
    {
        if (_suspended > 0) _suspended--;
    }

    internal readonly struct Scope : IDisposable
    {
        public void Dispose()
        {
            if (_depth > 0) _depth--;
        }
    }

    /// <summary>
    /// The short entry to read instead of <paramref name="key"/>, if the entry
    /// is listed and the current language has a short text for it.
    /// </summary>
    internal static bool TryGetCompactKey(string table, string key, Func<string, bool> exists, out string compactKey)
    {
        compactKey = null;
        if (table == null || key == null || !Entries.TryGetValue(table, out HashSet<string> keys)) return false;
        string normalized = HbrCompactTextKeys.Normalize(key);
        if (!keys.Contains(normalized)) return false;
        string candidate = HbrCompactTextKeys.CompactKey(key);
        if (!exists(candidate))
        {
            candidate = HbrCompactTextKeys.CompactKey(normalized);
            if (!exists(candidate)) return false;
        }
        compactKey = candidate;
        _swapped++;
        return true;
    }

    /// <summary>Formats an interface entry of the compact hover.</summary>
    internal static string Ui(string name, params (string Name, string Value)[] variables)
    {
        LocString text = new("card_keywords", HbrCompactTextKeys.UiPrefix + name);
        foreach ((string variable, string value) in variables) text.Add(variable, value ?? string.Empty);
        return text.GetFormattedText();
    }

    /// <summary>A title or description that is already formatted text.</summary>
    internal static LocString Literal(string name, string value)
    {
        LocString text = new("card_keywords", HbrCompactTextKeys.UiPrefix + name);
        text.Add(name, value ?? string.Empty);
        return text;
    }
}
