using System;
using Godot;

namespace HBRGOSakiMod.Core.Helpers;

internal readonly record struct InspectCardHoverTipPlacement(Rect2 Bounds, float Scale);

// Geometry only: the caller keeps complete native tips in a scrolling sidebar.
// Inputs and the returned rectangle use the same viewport coordinate system.
internal static class InspectCardHoverTipLayoutHelper
{
    internal static InspectCardHoverTipPlacement ComputePlacement(
        Rect2 viewport, Rect2 card, float contentWidth, float gap = 12f, float margin = 16f)
    {
        if (!ValidRect(viewport) || !ValidRect(card) ||
            !float.IsFinite(contentWidth) || contentWidth <= 0f ||
            !float.IsFinite(gap) || !float.IsFinite(margin))
            return new(default, 1f);

        gap = Math.Max(0f, gap);
        margin = Math.Max(0f, margin);
        Rect2 safe = viewport.Grow(-margin);
        if (!ValidRect(safe))
            return new(default, 1f);

        float leftEnd = Math.Clamp(card.Position.X - gap, safe.Position.X, safe.End.X);
        float rightStart = Math.Clamp(card.End.X + gap, safe.Position.X, safe.End.X);
        float leftWidth = leftEnd - safe.Position.X;
        float rightWidth = safe.End.X - rightStart;

        // Preserve the normal font size whenever either side can hold one tip.
        // Prefer left; if neither side fits, use the wider side and scale only
        // to its width. Height is handled by scrolling instead of tiny fonts.
        bool useLeft = leftWidth >= contentWidth ||
            rightWidth < contentWidth && leftWidth >= rightWidth;
        float width = Math.Min(contentWidth, useLeft ? leftWidth : rightWidth);
        if (width <= 0f)
            return new(default, 1f);

        float x = useLeft ? leftEnd - width : rightStart;
        return new(new Rect2(x, safe.Position.Y, width, safe.Size.Y), width / contentWidth);
    }

    private static bool ValidRect(Rect2 rect) =>
        rect.Position.IsFinite() && rect.Size.IsFinite() && rect.End.IsFinite() &&
        rect.Size.X > 0f && rect.Size.Y > 0f;
}
