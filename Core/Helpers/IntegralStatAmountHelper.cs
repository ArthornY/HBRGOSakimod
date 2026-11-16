using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class IntegralStatAmountHelper
{
    internal static decimal Normalize(decimal amount, PowerModel power)
    {
        // Exact Faith owns its bounded storage proxy; do not cap the logical
        // delta before its existing native storage adapter can preserve it.
        if (power is HbrFaithPower) return decimal.Truncate(amount);
        // Native ApplyInternal and ModifyAmount cast the amount to int before SetAmount
        // applies its ±999,999,999 cap; ModifyAmount also adds it to the current stacks as an
        // int. Bound every power's amount to that cap after every application modifier, on
        // both first application and additional stacks: the cast and that sum (at most twice
        // the cap) then stay in int range, and SetAmount stores the same stacks as before.
        // Double Powers under a replay reaches this range within one turn.
        if (power is FallingContinuallyPower or StrengthPower or DexterityPower or
            TemporaryStrengthPower or TemporaryDexterityPower)
            amount = decimal.Truncate(amount);
        return Math.Clamp(amount, -BigCombatNumbers.NativeStatCap, BigCombatNumbers.NativeStatCap);
    }
}
