using System;
using System.Globalization;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DpAmountCodec
{
    internal static int ToNative(decimal amount) =>
        (int)Math.Min(BigCombatNumbers.NativeStatCap, BigCombatNumbers.ClampStat(amount));

    internal static string Format(decimal amount) =>
        BigCombatNumbers.ClampStat(amount).ToString("0", CultureInfo.InvariantCulture);

    internal static decimal Parse(string value) =>
        decimal.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out decimal amount)
            ? BigCombatNumbers.ClampStat(amount) : 0m;

    internal static decimal AddOffset(decimal current, decimal offset)
    {
        // Native power counters truncate the signed offset before adding it.
        offset = decimal.Truncate(offset);
        if (offset >= 0m && offset >= BigCombatNumbers.MaximumCreatureStat - current)
            return BigCombatNumbers.MaximumCreatureStat;
        return BigCombatNumbers.ClampStat(current + offset);
    }
}
