using System;

namespace HBRGOSakiMod.Core.Helpers;

// A count snapshot keeps newest-first pages stable while more rolls are appended.
// Only the selected page is read/rendered; the complete history stays untouched.
internal readonly record struct SoulRollHistoryPage(
    int PageIndex,
    int PageCount,
    int NewestIndex,
    int EntryCount)
{
    internal const int EntriesPerPage = 50;

    internal static SoulRollHistoryPage Create(int totalCount, int requestedPage)
    {
        totalCount = Math.Max(0, totalCount);
        int pageCount = totalCount == 0 ? 1 : (totalCount - 1) / EntriesPerPage + 1;
        int pageIndex = Math.Clamp(requestedPage, 0, pageCount - 1);
        int skipped = pageIndex * EntriesPerPage;
        return new SoulRollHistoryPage(
            pageIndex,
            pageCount,
            totalCount - 1 - skipped,
            Math.Min(EntriesPerPage, totalCount - skipped));
    }
}
