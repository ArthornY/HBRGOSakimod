using System;
using System.Collections.Generic;
using System.Text;

namespace HBRGOSakiMod.Core.Helpers;

// Display-only, deterministic within a local battle. Numeric refreshes retain
// the same language choices because the key uses only visible Han characters.
internal sealed class ThirtyOneXGlyphTranslator
{
    private readonly Dictionary<string, string[]> _words;
    private readonly uint _seed;
    private readonly Dictionary<(ulong Surface, string Input), string> _cache = new();
    private readonly Dictionary<ulong, string> _lastOutputs = new();
    internal ThirtyOneXGlyphTranslator(Dictionary<string, string[]> words, uint seed)
    { _words = words ?? new(); _seed = seed; }
    internal static bool IsHan(int code) => code is >= 0x3400 and <= 0x4dbf or >= 0x4e00 and <= 0x9fff
        or >= 0xf900 and <= 0xfaff or >= 0x20000 and <= 0x323af;
    private static int ProtectedEnd(string text, int position)
    {
        char start = text[position];
        if (start == '[')
        {
            int end = text.IndexOf(']', position + 1);
            if (end < 0) return text.Length;
            string tag = text.Substring(position + 1, end - position - 1);
            if (tag.Equals("img", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("img=", StringComparison.OrdinalIgnoreCase)
                || tag.StartsWith("img ", StringComparison.OrdinalIgnoreCase))
            {
                int close = text.IndexOf("[/img]", end + 1, StringComparison.OrdinalIgnoreCase);
                if (close >= 0) return close + 6;
            }
            return end + 1;
        }
        if (start != '{') return position;
        int depth = 1;
        for (int index = position + 1; index < text.Length; index++)
        {
            if (text[index] == '{') depth++;
            if (text[index] == '}' && --depth == 0) return index + 1;
        }
        return text.Length;
    }
    private static uint Mix(uint hash, uint value)
    {
        hash ^= value;
        hash *= 16777619u;
        return hash;
    }
    private static uint Scramble(uint value)
    {
        value ^= value >> 16; value *= 0x7feb352du;
        value ^= value >> 15; value *= 0x846ca68bu;
        return value ^ (value >> 16);
    }
    private string Remember((ulong Surface, string Input) key, string result, bool trackOutput = false)
    {
        // Recalculation produces exactly the same result; the cache is only a
        // bounded optimization for frequently refreshed combat labels.
        if (_cache.Count >= 2048) _cache.Clear();
        if (trackOutput || _lastOutputs.ContainsKey(key.Surface)) _lastOutputs[key.Surface] = result;
        return _cache[key] = result;
    }
    internal string Translate(ulong surface, string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // UI controls sometimes assign their displayed text back to themselves.
        // Keep that readback stable even after the bounded input cache rolls over.
        if (_lastOutputs.TryGetValue(surface, out string output) && output == text) return text;
        var cacheKey = (surface, text);
        if (_cache.TryGetValue(cacheKey, out string cached))
        {
            if (cached != text || _lastOutputs.ContainsKey(surface)) _lastOutputs[surface] = cached;
            return cached;
        }
        uint hash = Mix(Mix(Mix(2166136261u, _seed), (uint)surface), (uint)(surface >> 32));
        bool hasHan = false;
        for (int index = 0; index < text.Length;)
        {
            int end = ProtectedEnd(text, index);
            if (end > index) { index = end; continue; }
            if (!Rune.TryGetRuneAt(text, index, out Rune rune)) return Remember(cacheKey, text);
            if (IsHan(rune.Value)) { hasHan = true; hash = Mix(hash, (uint)rune.Value); }
            index += rune.Utf16SequenceLength;
        }
        if (!hasHan) return Remember(cacheKey, text);
        StringBuilder result = new(text.Length);
        uint ordinal = 0;
        for (int index = 0; index < text.Length;)
        {
            int end = ProtectedEnd(text, index);
            if (end > index) { result.Append(text, index, end - index); index = end; continue; }
            Rune rune = Rune.GetRuneAt(text, index);
            string character = rune.ToString();
            if (IsHan(rune.Value))
            {
                uint choice = Scramble(Mix(hash, ordinal++)) % 5u;
                if (choice != 0 && _words.TryGetValue(character, out string[] words) && words.Length == 4 &&
                    !string.IsNullOrEmpty(words[choice - 1])) result.Append(words[choice - 1]);
                else result.Append(character);
            }
            else result.Append(character);
            index += rune.Utf16SequenceLength;
        }
        return Remember(cacheKey, result.ToString(), trackOutput: true);
    }
}
