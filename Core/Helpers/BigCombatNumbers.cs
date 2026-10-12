using System;
using System.Globalization;

namespace HBRGOSakiMod.Core.Helpers;

internal static class BigCombatNumbers
{
    // 2^64 itself is one above UInt64.MaxValue. Decimal stores it exactly.
    internal const decimal MaximumCreatureStat = 18_446_744_073_709_551_616m;
    internal const decimal MaximumCardDamage = MaximumCreatureStat;
    internal const int NativeStatCap = 999_999_999;

    internal static decimal ClampStat(decimal amount) =>
        decimal.Floor(Math.Clamp(amount, 0m, MaximumCreatureStat));

    internal static decimal ReadSigned(string saved, decimal legacy) =>
        decimal.TryParse(saved, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? decimal.Truncate(Math.Clamp(value, -MaximumCreatureStat, MaximumCreatureStat))
            : legacy;

    internal static int ToNativeSigned(decimal value) =>
        (int)Math.Clamp(value, -NativeStatCap, NativeStatCap);

    internal static string Save(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

    internal static decimal ScaleHealth(decimal amount, decimal multiplier) =>
        Math.Max(1m, decimal.Floor(MultiplyPositive(amount, multiplier)));

    // Check before multiplication, including exponentiation of huge growth
    // factors, so reaching the limit cannot overflow Decimal's wider range.
    internal static decimal MultiplyPositive(decimal amount, decimal multiplier)
    {
        if (amount <= 0m || multiplier <= 0m) return 0m;
        return amount >= MaximumCreatureStat / multiplier
            ? MaximumCreatureStat
            : amount * multiplier;
    }

    internal static decimal CalculateHealthMultiplier(
        int basePercent, int growthPercent, int actIndex)
    {
        decimal result = Math.Clamp(basePercent, 0, 1000) / 100m;
        if (result == 0m) return 0m;
        decimal growth = 1m + Math.Clamp(growthPercent, 0, 1000) / 100m;
        int exponent = Math.Max(0, actIndex);
        while (exponent > 0)
        {
            if ((exponent & 1) != 0) result = MultiplyPositive(result, growth);
            exponent >>= 1;
            if (exponent > 0) growth = MultiplyPositive(growth, growth);
        }
        return result;
    }
}
