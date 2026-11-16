using System.Globalization;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.Helpers;

// Text that writes a fixed resource amount literally is adapted only when the
// audited native amount appears exactly once outside BBCode tags, colors and IDs.
internal static class HbrFixedAmountText
{
    private static readonly Regex Number = new(@"(?<![0-9A-Za-z#.])\d+(?![0-9A-Za-z.])", RegexOptions.CultureInvariant);

    // Index of that single standalone occurrence, or -1 when it is absent or ambiguous.
    internal static int FindSingle(string text, int nativeAmount)
    {
        if (string.IsNullOrEmpty(text)) return -1;
        string digits = nativeAmount.ToString(CultureInfo.InvariantCulture);
        int found = -1;
        bool insideTag = false;
        int start = 0;
        for (int cursor = 0; cursor <= text.Length; cursor++)
        {
            if (cursor < text.Length && text[cursor] == '[')
            {
                if (!insideTag && !Find(start, cursor - start)) return -1;
                insideTag = true;
            }
            else if (cursor < text.Length && text[cursor] == ']')
            { insideTag = false; start = cursor + 1; }
            else if (cursor == text.Length && !insideTag && !Find(start, cursor - start)) return -1;
        }
        return found;

        bool Find(int offset, int length)
        {
            foreach (Match match in Number.Matches(text.Substring(offset, length)))
            {
                if (match.Value != digits) continue;
                if (found >= 0) return false;
                found = offset + match.Index;
            }
            return true;
        }
    }

    internal static string Replace(string text, int nativeAmount, string replacement)
    {
        int found = FindSingle(text, nativeAmount);
        return found < 0 ? text : text[..found] + replacement +
            text[(found + nativeAmount.ToString(CultureInfo.InvariantCulture).Length)..];
    }
}
