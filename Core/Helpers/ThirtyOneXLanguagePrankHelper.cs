using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LocalRandom = System.Security.Cryptography.RandomNumberGenerator;
using System.Text.Json;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneXLanguagePrankHelper
{
    private sealed class Glossary
    {
        public Glossary() { }
        public int Schema { get; set; }
        public Dictionary<string, string[]> Entries { get; set; }
    }
    private sealed class Session
    {
        internal ICombatState Combat;
        internal ThirtyOneXGlyphTranslator Translator;
        internal Dictionary<string, LocTable> Chinese;
        internal readonly Dictionary<string, string> RawDisplays = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> OriginalFormats = new(StringComparer.Ordinal);
        internal readonly ConditionalWeakTable<string, OriginalText> OriginalReferences = new();
        internal readonly HashSet<string> AmbiguousOriginals = new(StringComparer.Ordinal);
        internal readonly Dictionary<(ulong, string), DisplayReceipt> Displays = new();
        internal readonly Dictionary<ulong, WeakReference<CanvasItem>> Drawings = new();
        internal readonly Dictionary<Font, Godot.Collections.Array<Font>> Fonts = new();
        internal Font Latin, Devanagari;
    }
    private sealed class OriginalText
    { internal string Text; }
    private sealed class DisplayReceipt
    {
        internal WeakReference<GodotObject> Surface;
        internal string Property, Original, Output;
    }
    internal sealed class FormatScope
    {
        internal LocString Text;
        internal FormatScope Previous;
        internal bool Active = true;
        internal bool RawChanged;
    }
    private static readonly MethodInfo LoadChinese = AccessTools.Method(typeof(LocManager), "LoadTablesFromPath",
        new[] { typeof(string), typeof(bool) });
    private static readonly FieldInfo EnglishOverride = AccessTools.Field(typeof(LocManager), "_stateBeforeOverridingWithEnglish");
    private static readonly FieldInfo CurrentTables = AccessTools.Field(typeof(LocManager), "_tables");
    private static readonly Dictionary<string, WeakReference<LocString>> FormattedSources = new(StringComparer.Ordinal);
    private static ConditionalWeakTable<string, WeakReference<LocString>> _formattedReferences = new();
    private static readonly Dictionary<(Type, string), PropertyInfo> DisplayProperties = new();
    private static Session _session;
    private static ICombatState _checkedCombat;
    private static readonly string[] NormalFonts = { "font" };
    private static readonly string[] RichFonts = { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" };
    [ThreadStatic] private static FormatScope _format;
    [ThreadStatic] private static bool _originalFormat;
    internal static bool IsActive => _session != null;
    internal static bool ShouldTrigger(bool hasMember, int permille, Func<int, int> roll)
    {
        int chance = Math.Clamp(permille, 0, 1000);
        return hasMember && chance > 0 && (chance == 1000 || roll(1000) < chance);
    }
    internal static void TryBegin(ICombatState combat)
    {
        if (combat == null || ReferenceEquals(_checkedCombat, combat)) return;
        Clear(false);
        _checkedCombat = combat;
        var local = LocalContext.GetMe(combat.Players);
        bool hasMember = local != null && ThirtyOneXKeywordRules.GetPermanentDistinctCount(local) > 0;
        if (!ShouldTrigger(hasMember, HBRGOSakiConfig.ThirtyOneXHiddenEffectPermille, LocalRandom.GetInt32)) return;
        try
        {
            string raw = FileAccess.GetFileAsString("res://data/thirty_one_x_character_glossary.json");
            Glossary glossary = JsonSerializer.Deserialize<Glossary>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (glossary?.Schema != 1 || glossary.Entries == null || glossary.Entries.Count == 0)
                throw new InvalidOperationException("Missing 31X character glossary.");
            object loaded = LoadChinese?.Invoke(null, new object[] { "zhs", false }) ??
                throw new MissingMethodException("LocManager.LoadTablesFromPath");
            Dictionary<string, LocTable> tables = (Dictionary<string, LocTable>)loaded.GetType().GetField("Item1").GetValue(loaded);
            Session session = new()
            {
                Combat = combat, Chinese = tables,
                Translator = new(glossary.Entries, (uint)LocalRandom.GetInt32(int.MaxValue)),
                Latin = ResourceLoader.Load<FontFile>("res://fonts/noto_sans.ttf"),
                Devanagari = ResourceLoader.Load<FontFile>("res://fonts/noto_sans_devanagari.ttf")
            };
            if (session.Latin == null || session.Devanagari == null) throw new InvalidOperationException("Missing 31X font fallback.");
            _session = session;
            BuildPlainDisplayLookup(session);
            RefreshDisplay();
        }
        catch (Exception error)
        {
            Clear(false);
            _checkedCombat = combat;
            Log.Warn("[HBRGOSakiMod] Could not initialize local31X display effect: " + error.Message);
        }
    }
    internal static FormatScope BeginFormat(LocString text)
    {
        if (_session == null || _originalFormat || EnglishOverride?.GetValue(LocManager.Instance) != null) return null;
        FormatScope scope = new() { Text = text, Previous = _format };
        _format = scope;
        return scope;
    }
    internal static void EndFormat(FormatScope scope)
    {
        if (scope == null || !scope.Active) return;
        scope.Active = false;
        if (ReferenceEquals(_format, scope)) _format = scope.Previous;
    }
    internal static void ChineseRaw(LocString text, ref string result)
    {
        Session session = _session;
        if (session == null || _originalFormat || EnglishOverride?.GetValue(LocManager.Instance) != null || text == null ||
            !session.Chinese.TryGetValue(text.LocTable, out LocTable table) ||
            !table.HasEntry(text.LocEntryKey)) return;
        string chinese = table.GetRawText(text.LocEntryKey);
        if (_format is { Active: true } scope && ReferenceEquals(scope.Text, text))
        { scope.RawChanged |= result != chinese; result = chinese; }
        else if (!string.IsNullOrEmpty(result) && result != chinese)
        {
            if (session.RawDisplays.Count >= 2048) session.RawDisplays.Clear();
            session.RawDisplays[result] = chinese;
        }
    }
    // Remember only a bounded, weak provenance map outside the rare effect.
    // Existing labels can then be re-rendered from Chinese without changing the
    // real locale or retaining card/model objects after the battle.
    internal static void CaptureFormatted(LocString text, string result, FormatScope scope)
    {
        if (_originalFormat || string.IsNullOrEmpty(result) || text == null ||
            EnglishOverride?.GetValue(LocManager.Instance) != null) return;
        Session session = _session;
        if (session != null && scope != null)
        {
            LocString original = new(text.LocTable, text.LocEntryKey);
            bool changed = scope.RawChanged;
            foreach (var variable in text.Variables)
            {
                object value = variable.Value;
                if (value is string child)
                { string rawChild = OriginalDisplay(session, child); changed |= rawChild != child; value = rawChild; }
                else if (value is IList<string> children)
                {
                    string[] restored = children.Select(child => OriginalDisplay(session, child)).ToArray();
                    changed |= !children.SequenceEqual(restored);
                    value = restored;
                }
                original.AddObj(variable.Key, value);
            }
            string raw = result;
            if (changed)
            {
                _originalFormat = true;
                try { raw = original.GetFormattedText(); }
                finally { _originalFormat = false; }
            }
            session.OriginalReferences.Remove(result);
            session.OriginalReferences.Add(result, new() { Text = raw });
            if (session.OriginalFormats.Count >= 2048)
            { session.OriginalFormats.Clear(); session.AmbiguousOriginals.Clear(); }
            if (session.OriginalFormats.TryGetValue(result, out string earlier) && earlier != raw)
            { session.AmbiguousOriginals.Add(result); session.OriginalFormats.Remove(result); }
            if (!session.AmbiguousOriginals.Contains(result)) session.OriginalFormats[result] = raw;
            if (FormattedSources.Count >= 2048) FormattedSources.Clear();
            FormattedSources[raw] = new(text);
            _formattedReferences.Remove(raw);
            _formattedReferences.Add(raw, new(text));
        }
        else
        {
            if (FormattedSources.Count >= 2048) FormattedSources.Clear();
            FormattedSources[result] = new(text);
            _formattedReferences.Remove(result);
            _formattedReferences.Add(result, new(text));
        }
    }
    private static void BuildPlainDisplayLookup(Session session)
    {
        if (CurrentTables?.GetValue(LocManager.Instance) is not Dictionary<string, LocTable> current) return;
        foreach (var entry in current)
        {
            if (!session.Chinese.TryGetValue(entry.Key, out LocTable chinese)) continue;
            foreach (LocString key in entry.Value.GetLocStringsWithPrefix(string.Empty))
            {
                if (!chinese.HasEntry(key.LocEntryKey)) continue;
                string raw = entry.Value.GetRawText(key.LocEntryKey), translated = chinese.GetRawText(key.LocEntryKey);
                if (!string.IsNullOrEmpty(raw) && !raw.Contains('{') && !translated.Contains('{') && raw != translated)
                    session.RawDisplays[raw] = translated;
            }
        }
    }
    private static string ChineseDisplay(Session session, string text)
    {
        string plain = ChineseDisplayPlain(session, text);
        return plain != text ? plain : MapWrappedDisplay(session, text, chinese: true);
    }
    private static string ChineseDisplayPlain(Session session, string text)
    {
        if (session.OriginalReferences.TryGetValue(text, out _) || session.OriginalFormats.ContainsKey(text)) return text;
        if (_formattedReferences.TryGetValue(text, out var reference) && reference.TryGetTarget(out LocString referenced))
            return referenced.GetFormattedText();
        if (FormattedSources.TryGetValue(text, out var source) && source.TryGetTarget(out LocString loc))
            return loc.GetFormattedText();
        // Card titles append their upgrade outside the formatter.
        int suffix = text.LastIndexOf('+');
        if (suffix > 0 && text.AsSpan(suffix + 1).Length > 0 && text.AsSpan(suffix + 1).ToString().All(char.IsAsciiDigit))
        {
            string title = text[..suffix];
            if (FormattedSources.TryGetValue(title, out source) && source.TryGetTarget(out loc))
                return loc.GetFormattedText() + text[suffix..];
            if (session.RawDisplays.TryGetValue(title, out string chineseTitle)) return chineseTitle + text[suffix..];
        }
        return session.RawDisplays.GetValueOrDefault(text, text);
    }
    private static ulong SurfaceKey(ulong instance, string property)
    {
        ulong value = instance;
        foreach (char character in property) value = (value ^ character) * 1099511628211UL;
        return value;
    }
    private static string OriginalDisplay(Session session, string text)
    {
        string plain = OriginalDisplayPlain(session, text);
        return plain != text ? plain : MapWrappedDisplay(session, text, chinese: false);
    }
    private static string OriginalDisplayPlain(Session session, string text)
    {
        if (session.OriginalReferences.TryGetValue(text, out OriginalText reference)) return reference.Text;
        if (session.OriginalFormats.TryGetValue(text, out string original)) return original;
        int suffix = text.LastIndexOf('+');
        if (suffix > 0 && text.AsSpan(suffix + 1).Length > 0 && text.AsSpan(suffix + 1).ToString().All(char.IsAsciiDigit) &&
            session.OriginalFormats.TryGetValue(text[..suffix], out original)) return original + text[suffix..];
        return text;
    }
    private static string MapWrappedDisplay(Session session, string text, bool chinese)
    {
        // NCard wraps its already-formatted description in center/font-size
        // tags, losing the string reference that carries locale provenance.
        // Peel only matching outer tags, retaining every byte of the markup.
        int start = 0, end = text.Length;
        for (int depth = 0; depth < 16 && start < end && text[start] == '['; depth++)
        {
            int close = text.IndexOf(']', start + 1);
            if (close < 0 || close >= end) break;
            string opening = text.Substring(start + 1, close - start - 1);
            int arguments = opening.IndexOfAny(new[] { '=', ' ' });
            string name = arguments < 0 ? opening : opening[..arguments];
            if (name.Length == 0 || name.Equals("img", StringComparison.OrdinalIgnoreCase) ||
                name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')) break;
            string closing = "[/" + name + "]";
            if (!text.AsSpan(start, end - start).EndsWith(closing, StringComparison.OrdinalIgnoreCase)) break;
            start = close + 1;
            end -= closing.Length;
            if (end < start) break;
            string inner = text.Substring(start, end - start);
            string mapped = chinese ? ChineseDisplayPlain(session, inner) : OriginalDisplayPlain(session, inner);
            if (mapped != inner) return text[..start] + mapped + text[end..];
        }
        return text;
    }
    internal static string GetOriginalDisplay(string text) => _session == null || string.IsNullOrEmpty(text)
        ? text : OriginalDisplay(_session, text);
    internal static void PrepareDisplay(GodotObject surface, ref string text)
        => PreparePropertyDisplay(surface, ref text, "Text");
    internal static void PreparePropertyDisplay(GodotObject surface, ref string text, string property)
    {
        Session session = _session;
        if (session == null || surface == null) return;
        ulong id = surface.GetInstanceId();
        var key = (id, property);
        if (_originalFormat || EnglishOverride?.GetValue(LocManager.Instance) != null)
        {
            // Native metrics/English formatting must remain English at the
            // display boundary too. A replacement supersedes its old receipt.
            session.Displays.Remove(key);
            return;
        }
        if (string.IsNullOrEmpty(text)) { session.Displays.Remove(key); return; }
        if (!session.Displays.TryGetValue(key, out DisplayReceipt receipt) || receipt.Output != text)
        {
            string original = OriginalDisplay(session, text);
            text = session.Translator.Translate(SurfaceKey(id, property), ChineseDisplay(session, text));
            session.Displays[key] = new() { Surface = new(surface), Property = property, Original = original, Output = text };
        }
        if (surface is not Control control) return;
        foreach (string name in control is RichTextLabel ? RichFonts : NormalFonts)
            PrepareFont(session, control.GetThemeFont(name));
    }
    internal static void PrepareAppend(RichTextLabel surface, ref string text)
    {
        Session session = _session;
        if (session == null || surface == null || string.IsNullOrEmpty(text)) return;
        var key = (surface.GetInstanceId(), "Text");
        if (_originalFormat || EnglishOverride?.GetValue(LocManager.Instance) != null)
        {
            // Appending under a native English override must not leave the
            // already-rendered prefix mixed. Restore it before native append.
            if (session.Displays.Remove(key, out DisplayReceipt previous))
                SetDisplayProperty(surface, "Text", previous.Original);
            return;
        }
        string current = surface.Text;
        string original = session.Displays.TryGetValue(key, out DisplayReceipt prior) && prior.Output == current ? prior.Original : current;
        string appended = OriginalDisplay(session, text);
        PreparePropertyDisplay(surface, ref text, "Text");
        session.Displays[key].Original = original + appended;
    }
    internal static void CompleteAppend(RichTextLabel surface)
    {
        if (_session != null && _session.Displays.TryGetValue((surface.GetInstanceId(), "Text"), out DisplayReceipt receipt))
            receipt.Output = surface.Text;
    }
    internal static void ForgetText(RichTextLabel surface)
        => _session?.Displays.Remove((surface.GetInstanceId(), "Text"));
    internal static void PrepareDrawText(CanvasItem surface, Font font, ref string text)
    {
        Session session = _session;
        if (session == null || surface == null || string.IsNullOrEmpty(text)) return;
        if (_originalFormat || EnglishOverride?.GetValue(LocManager.Instance) != null) return;
        ulong id = surface.GetInstanceId();
        text = session.Translator.Translate(SurfaceKey(id, "DrawString"), ChineseDisplay(session, text));
        session.Drawings.TryAdd(id, new(surface));
        PrepareFont(session, font);
    }
    private static void PrepareFont(Session session, Font font)
    {
        if (font == null || session.Fonts.ContainsKey(font)) return;
        {
            Godot.Collections.Array<Font> previous = font.Fallbacks.Duplicate();
            session.Fonts.Add(font, previous);
            Godot.Collections.Array<Font> fallbacks = previous.Duplicate();
            if (font.GetInstanceId() != session.Latin.GetInstanceId() && !fallbacks.Contains(session.Latin)) fallbacks.Add(session.Latin);
            if (font.GetInstanceId() != session.Devanagari.GetInstanceId() && !fallbacks.Contains(session.Devanagari)) fallbacks.Add(session.Devanagari);
            font.Fallbacks = fallbacks;
        }
    }
    internal static void Clear(bool refresh = true)
    {
        _checkedCombat = null;
        Session previous = _session;
        _session = null;
        _format = null;
        if (previous == null) return;
        foreach (DisplayReceipt receipt in previous.Displays.Values)
            if (receipt.Surface.TryGetTarget(out GodotObject surface) && GodotObject.IsInstanceValid(surface))
                SetDisplayProperty(surface, receipt.Property, receipt.Original);
        foreach (var entry in previous.Fonts)
            if (GodotObject.IsInstanceValid(entry.Key)) entry.Key.Fallbacks = entry.Value;
        foreach (var drawing in previous.Drawings.Values)
            if (drawing.TryGetTarget(out CanvasItem surface) && GodotObject.IsInstanceValid(surface)) surface.QueueRedraw();
        FormattedSources.Clear();
        _formattedReferences = new();
        if (refresh) RefreshDisplay();
    }
    private static PropertyInfo DisplayProperty(GodotObject surface, string property)
    {
        var key = (surface.GetType(), property);
        if (!DisplayProperties.TryGetValue(key, out PropertyInfo info))
        {
            if (DisplayProperties.Count >= 128) DisplayProperties.Clear();
            DisplayProperties[key] = info = AccessTools.Property(key.Item1, property);
        }
        return info;
    }
    private static void SetDisplayProperty(GodotObject surface, string property, string value)
        => DisplayProperty(surface, property)?.SetValue(surface, value);
    internal static void RefreshNode(Node node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node)) return;
        foreach (string property in node switch
        {
            Label or RichTextLabel or Button => new[] { "Text", "TooltipText" },
            LineEdit => new[] { "PlaceholderText", "TooltipText" },
            Control => new[] { "TooltipText" }, Window => new[] { "Title" }, _ => Array.Empty<string>()
        })
        {
            if (DisplayProperty(node, property)?.GetValue(node) is string text && !string.IsNullOrEmpty(text))
                SetDisplayProperty(node, property, text);
        }
        if (node is CanvasItem drawing) drawing.QueueRedraw();
        foreach (Node child in node.GetChildren()) RefreshNode(child);
    }
    private static void RefreshDisplay()
    {
        NGame game = NGame.Instance;
        if (game != null && GodotObject.IsInstanceValid(game) && game.IsInsideTree())
        { game.Relocalize(); RefreshNode(game); }
    }
}
