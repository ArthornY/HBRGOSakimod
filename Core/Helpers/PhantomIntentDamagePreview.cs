using System;
using System.Collections.Generic;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomIntentDamagePreview
{
    // The caller supplies a calculation for one fixed preview context. Keep
    // segment order/count and exact decimal inputs; only identical inputs share
    // work, and each invocation owns its cache so targets/state cannot leak.
    internal static int[] Calculate(IReadOnlyList<decimal> segments,
        Func<decimal, decimal> calculateDamage)
    {
        if (segments.Count == 0) return Array.Empty<int>();
        if (segments.Count == 1)
            return [Math.Max(0, (int)calculateDamage(segments[0]))];

        var result = new int[segments.Count];
        var damageByBaseValue = new Dictionary<decimal, int>();
        for (int index = 0; index < segments.Count; index++)
        {
            decimal baseDamage = segments[index];
            if (!damageByBaseValue.TryGetValue(baseDamage, out int damage))
            {
                damage = Math.Max(0, (int)calculateDamage(baseDamage));
                damageByBaseValue.Add(baseDamage, damage);
            }
            result[index] = damage;
        }
        return result;
    }
}
