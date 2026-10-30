using System;

namespace HBRGOSakiMod.Core.Helpers;

// Pure local-space geometry: the text uses the DP row's free right half while
// staying above every occupied HP reserve. Its right edge is the HP frame edge.
internal static class HbrEnemyStatusValueLayout
{
    internal const float HorizontalGap = 6f;
    // Native HP numbers extend five pixels above their frame.
    internal const float HpClearance = 6f;

    internal static (float Left, float Top, float Width, float Height) GetRect(
        float hpWidth, float hpHeight, float dpWidth, float dpTop, float reserveHpHeight)
    {
        hpWidth = Math.Max(0f, hpWidth);
        float height = Math.Clamp(hpHeight, 16f, 28f);
        // Reserve DP pools fan to the right behind the active pool. Their
        // common right boundary is the unshifted half-width plus 0.9 pixels.
        float left = Math.Min(hpWidth, Math.Max(0f, dpWidth) + 0.9f + HorizontalGap);
        float top = Math.Min(dpTop, -Math.Max(0f, reserveHpHeight) - height - HpClearance);
        return (left, top, Math.Max(0f, hpWidth - left), height);
    }
}
