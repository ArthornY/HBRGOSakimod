using System;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrDpRecoveryLimit
{
    internal const int DefaultPercent = 120;
    internal static int ToPercent(decimal ceilingRatio) => (int)Math.Clamp(decimal.Ceiling(
        DamageAmplificationLimits.MultiplySaturated(Math.Max(1.2m, ceilingRatio), 100m)), DefaultPercent, int.MaxValue);
    internal static decimal Ratio(int savedPercent) => Math.Max(DefaultPercent, savedPercent) / 100m;
}
