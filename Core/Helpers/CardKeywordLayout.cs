using System.Text;
using System.Text.RegularExpressions;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CardKeywordLayout
{
    // Native and Ritsu keywords use gold titles followed by punctuation.
    // Match the entire line so highlighted words inside an effect stay untouched.
    private static readonly Regex ShortKeywordLine = new(
        @"\A(?:\[gold\][^\[\]\r\n]{1,24}\[/gold\][。.!?！？]?[ \t]*)+\z",
        RegexOptions.CultureInvariant);

    internal static string Compact(string description)
    {
        if (string.IsNullOrEmpty(description) || !description.Contains('\n'))
        {
            return description;
        }

        string[] lines = description.Split('\n');
        StringBuilder result = new(description.Length);
        bool previousIsKeyword = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            bool isKeyword = ShortKeywordLine.IsMatch(line.Trim());
            if (index > 0)
            {
                if (previousIsKeyword && isKeyword)
                {
                    if (result.Length > 0 && result[^1] == '\r')
                    {
                        result.Length--;
                    }

                    result.Append(' ');
                }
                else
                {
                    result.Append('\n');
                }
            }

            result.Append(line);
            previousIsKeyword = isKeyword;
        }

        return result.ToString();
    }
}
