using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class IntegralStatAmountHelper
{
    internal static decimal Normalize(decimal amount, PowerModel power) =>
        // Native ApplyInternal casts before its SetAmount cap. Keep the new
        // attribute-scaled counter safe after every application modifier, on
        // both first application and additional stacks.
        power is FallingContinuallyPower
            ? decimal.Truncate(Math.Clamp(amount, -BigCombatNumbers.NativeStatCap, BigCombatNumbers.NativeStatCap))
            : power is StrengthPower or DexterityPower or
            TemporaryStrengthPower or TemporaryDexterityPower
            ? decimal.Truncate(amount)
            : amount;
}
