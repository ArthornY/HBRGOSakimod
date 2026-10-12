using System;

namespace HBRGOSakiMod.Core.Helpers;

internal readonly record struct CardRewardPageLayout(
    int PageSize,
    int PageIndex,
    int PageCount,
    int StartIndex,
    int VisibleCount)
{
    internal const float CardSpacing = 350f;
    internal const float HoverCardWidth = 300f;
    internal const float SideMargin = 64f;

    internal static CardRewardPageLayout Create(int count, float width, int page)
    {
        int pageSize = Math.Clamp(
            (int)Math.Floor((Math.Max(0f, width) - SideMargin * 2f - HoverCardWidth) / CardSpacing) + 1,
            1,
            5);
        count = Math.Max(0, count);
        int pages = Math.Max(1, (int)(((long)count + pageSize - 1) / pageSize));
        page = Math.Clamp(page, 0, pages - 1);
        int start = page * pageSize;
        return new(pageSize, page, pages, start, Math.Min(pageSize, count - start));
    }

    internal float GetCardX(int slot) => (slot - (VisibleCount - 1) * 0.5f) * CardSpacing;
}
