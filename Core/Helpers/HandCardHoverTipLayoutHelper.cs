using System;
using Godot;

namespace HBRGOSakiMod.Core.Helpers;

internal readonly record struct HandCardHoverTipPlacement(Rect2 Bounds, float Scale);

// Geometry only. The caller keeps one native-width column in a scrolling
// viewport; long content changes its scroll range, never the font scale.
internal static class HandCardHoverTipLayoutHelper
{
    internal static HandCardHoverTipPlacement ComputePlacement(
        Rect2 viewport, Rect2 card, float contentWidth, float gap = 12f, float margin = 16f)
    {
        if (!ValidRect(viewport) || !ValidRect(card) ||
            !float.IsFinite(contentWidth) || contentWidth <= 0f ||
            !float.IsFinite(gap) || !float.IsFinite(margin))
            return new(default, 1f);

        gap = Math.Max(0f, gap);
        margin = Math.Max(0f, margin);
        Rect2 safe = viewport.Grow(-margin);
        Rect2 reserved = card.Grow(gap);
        if (!ValidRect(safe) || !ValidRect(reserved)) return new(default, 1f);

        Rect2 blocked = reserved.Intersection(viewport);
        if (!blocked.HasArea())
            return Place(safe, contentWidth, card.Position.X - gap - contentWidth);

        float leftEnd = Math.Clamp(blocked.Position.X, safe.Position.X, safe.End.X);
        float rightStart = Math.Clamp(blocked.End.X, safe.Position.X, safe.End.X);
        float topEnd = Math.Clamp(blocked.Position.Y, safe.Position.Y, safe.End.Y);
        float bottomStart = Math.Clamp(blocked.End.Y, safe.Position.Y, safe.End.Y);
        Rect2 left = new(safe.Position, new Vector2(leftEnd - safe.Position.X, safe.Size.Y));
        Rect2 right = new(new Vector2(rightStart, safe.Position.Y), new Vector2(safe.End.X - rightStart, safe.Size.Y));
        Rect2 above = new(safe.Position, new Vector2(safe.Size.X, topEnd - safe.Position.Y));
        Rect2 below = new(new Vector2(safe.Position.X, bottomStart), new Vector2(safe.Size.X, safe.End.Y - bottomStart));

        // Sidebars can use the whole screen height even when the card sits at
        // the bottom. Prefer left whenever both sides preserve the native size.
        if (left.Size.X >= contentWidth) return Place(left, contentWidth, left.End.X - contentWidth);
        if (right.Size.X >= contentWidth) return Place(right, contentWidth, right.Position.X);

        float readingHeight = Math.Min(160f, safe.Size.Y * 0.4f);
        bool fitsAbove = above.Size.X >= contentWidth && above.Size.Y >= readingHeight;
        bool fitsBelow = below.Size.X >= contentWidth && below.Size.Y >= readingHeight;
        float centeredX = card.Position.X + card.Size.X * 0.5f - contentWidth * 0.5f;
        if (fitsAbove || fitsBelow)
            return Place(fitsAbove && (!fitsBelow || above.Size.Y >= below.Size.Y) ? above : below,
                contentWidth, centeredX);

        // Only genuinely constrained windows reach width scaling. Prefer the
        // widest usable reading area, then the taller one, avoiding tiny side
        // strips when a larger area above or below the card remains available.
        Rect2 best = default;
        foreach (Rect2 area in new[] { left, right, above, below })
        {
            if (!ValidRect(area) || area.Size.Y < readingHeight) continue;
            if (area.Size.X > best.Size.X || area.Size.X == best.Size.X && area.Size.Y > best.Size.Y)
                best = area;
        }
        if (!ValidRect(best)) return new(default, 1f);
        float preferredX = best == left ? best.End.X - contentWidth : best == right ? best.Position.X : centeredX;
        return Place(best, contentWidth, preferredX);
    }

    // Convert an absolute placement in the tip parent's frame to this tip
    // root's local space. This cancels any native re-alignment, including one
    // with unchanged viewport/card geometry after a Shift rebuild.
    internal static Transform2D InParentFrame(Transform2D tipsToFrame, Vector2 position, float scale) =>
        tipsToFrame.AffineInverse() * new Transform2D(0f, Vector2.One * scale, 0f, position);

    internal static void WrapText(Control entry)
    {
        if (entry.GetNodeOrNull<Label>("%Title") is { } title)
        {
            title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            title.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        }
        if (entry.GetNodeOrNull<RichTextLabel>("%Description") is { } body)
            body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private static HandCardHoverTipPlacement Place(Rect2 area, float contentWidth, float preferredX)
    {
        float width = Math.Min(contentWidth, area.Size.X);
        float scale = width / contentWidth;
        if (!float.IsFinite(scale) || scale <= 0f) return new(default, 1f);
        float x = Math.Clamp(preferredX, area.Position.X, Math.Max(area.Position.X, area.End.X - width));
        return new(new Rect2(x, area.Position.Y, width, area.Size.Y), scale);
    }

    private static bool ValidRect(Rect2 rect) =>
        rect.Position.IsFinite() && rect.Size.IsFinite() && rect.End.IsFinite() &&
        rect.Size.X > 0f && rect.Size.Y > 0f;
}
