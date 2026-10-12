using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class IntegralStatAmountHelper
{
    internal static decimal Normalize(decimal amount, PowerModel power) =>
        power is StrengthPower or DexterityPower or
            TemporaryStrengthPower or TemporaryDexterityPower
            ? decimal.Truncate(amount)
            : amount;
}
