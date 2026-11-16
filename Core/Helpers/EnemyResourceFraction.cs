using System;
using System.Numerics;

namespace HBRGOSakiMod.Core.Helpers;

// Resource profiles are rational coefficients. Multiply their numerators
// before the final divide/floor: pre-dividing a repeating share can lose one
// HP, which then also changes DP. Ordinary resources stay on the decimal fast
// path; only an overflowing intermediate uses the exact integer fallback.
internal static class EnemyResourceFraction
{
    internal static decimal Floor(decimal amount, decimal numerator, decimal multiplier, decimal denominator)
    {
        if (amount <= 0m || numerator <= 0m || multiplier <= 0m || denominator <= 0m) return 0m;
        try { return BigCombatNumbers.ClampStat(checked(amount * numerator * multiplier) / denominator); }
        catch (OverflowException)
        {
            var a = Ratio(amount); var n = Ratio(numerator); var m = Ratio(multiplier); var d = Ratio(denominator);
            BigInteger value = a.Number * n.Number * m.Number * d.Denominator /
                (a.Denominator * n.Denominator * m.Denominator * d.Number);
            return value >= new BigInteger(BigCombatNumbers.MaximumCreatureStat)
                ? BigCombatNumbers.MaximumCreatureStat : (decimal)value;
        }
    }

    private static (BigInteger Number, BigInteger Denominator) Ratio(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger number = new BigInteger((uint)bits[0]) + (new BigInteger((uint)bits[1]) << 32) +
            (new BigInteger((uint)bits[2]) << 64);
        return (number, BigInteger.Pow(10, (bits[3] >> 16) & 0xff));
    }
}
