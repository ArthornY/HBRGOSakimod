using System;
using System.Globalization;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// These values are quoted by the shop service. The view never recomputes a
// discount or writes a candidate item while rendering a pending transaction.
internal readonly record struct HbrManualShopPriceView(int Original, int Final, bool Discounted)
{
    internal static HbrManualShopPriceView From(int original, int final) =>
        new(Math.Max(0, original), Math.Max(0, final), original > final && final >= 0);
    internal string OriginalBbCode => Discounted ? "[s]" + Original.ToString(CultureInfo.InvariantCulture) + "[/s]" : string.Empty;
    internal string Badge => Discounted ? "佐月的优惠" : string.Empty;
}

internal readonly record struct HbrManualShopComparisonLayout(HbrPhoneRect Panel, float ColumnWidth, float TextWidth);

internal static class HbrManualShopPresentation
{
    internal const float ComparisonGap = 12f;
    internal const float ComparisonPanelMargin = 12f;
    internal const float ComparisonInnerMargin = 14f;
    internal const float ComparisonScrollWidth = 18f;
    internal const float ComparisonCardMargin = 12f;
    internal static float ListWidth(float width) => Math.Max(0f,
        width - (width >= 760f ? width * .4f : 12f) - 12f);
    internal static bool CompactOffers(float pageWidth) => ListWidth(pageWidth) < 480f;
    internal static HbrManualShopComparisonLayout Comparison(float width, float height)
    {
        float panelWidth = Math.Max(0f, width - ComparisonPanelMargin * 2f);
        float panelHeight = Math.Max(0f, height - ComparisonPanelMargin * 2f);
        float column = Math.Max(0f, (panelWidth - ComparisonInnerMargin * 2f - ComparisonScrollWidth - ComparisonGap) / 2f);
        return new(new HbrPhoneRect(ComparisonPanelMargin, ComparisonPanelMargin, panelWidth, panelHeight),
            column, Math.Max(1f, column - ComparisonCardMargin * 2f));
    }
}
