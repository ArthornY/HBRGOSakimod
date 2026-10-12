using System;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DamageLimitHelper
{
    internal const int MaximumSingleHitDamage = 2_000_000_000;

    // Apply after damage modifiers so reductions are calculated before the cap.
    internal static decimal CapSingleHit(decimal amount) =>
        Math.Min(amount, MaximumSingleHitDamage);
}
