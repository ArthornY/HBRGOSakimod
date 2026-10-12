using System;
using System.Globalization;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DamageAmplificationLimits
{
    // Percentage points, not a native PowerModel layer count.
    internal const decimal MaximumTemporaryPercent = 2_147_483_648m;
    internal const decimal MaximumTemporaryOffset = MaximumTemporaryPercent / 100m;

    internal static decimal Clamp(decimal percent) =>
        Math.Clamp(percent, 0m, MaximumTemporaryPercent);

    internal static decimal Parse(string data) =>
        decimal.TryParse(data, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? Clamp(value) : 0m;

    internal static string Format(decimal percent) =>
        Clamp(percent).ToString(CultureInfo.InvariantCulture);

    internal static int ToNative(decimal percent) =>
        (int)Math.Min(999_999_999m, Clamp(percent));

    internal static decimal AddSaturated(decimal left, decimal right)
    {
        if (right > 0m && left > decimal.MaxValue - right) return decimal.MaxValue;
        if (right < 0m && left < decimal.MinValue - right) return decimal.MinValue;
        return left + right;
    }

    internal static decimal MultiplySaturated(decimal left, decimal right)
    {
        if (left == 0m || right == 0m) return 0m;
        // Damage factors are nonnegative; this also handles signed additive ledgers.
        if (left == decimal.MinValue || right == decimal.MinValue)
        {
            try { return left * right; }
            catch (OverflowException) { return Math.Sign(left) == Math.Sign(right) ? decimal.MaxValue : decimal.MinValue; }
        }
        decimal absoluteLeft = Math.Abs(left);
        decimal absoluteRight = Math.Abs(right);
        if (absoluteRight > 1m && absoluteLeft > decimal.MaxValue / absoluteRight)
            return Math.Sign(left) == Math.Sign(right) ? decimal.MaxValue : decimal.MinValue;
        return left * right;
    }
}
