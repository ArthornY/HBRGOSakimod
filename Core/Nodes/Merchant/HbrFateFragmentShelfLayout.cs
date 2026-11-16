using System;
using HBRGOSakiMod.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Merchant;

// Where the merchant's fate fragment stand sits on the shop rug: the vanilla %SlotsContainer of
// scenes/merchant/merchant_inventory.tscn, 1747x978, whose children are placed in these coordinates. The rug
// has a free column right of the card removal service; the stand stacks this shop's one or two fragments
// there, an icon over its price like the vanilla relic and potion slots. hbr-battle-hud reads the vanilla
// slots from the recovered scene and checks these boxes against them. Pure: no Godot types.
internal static class HbrFateFragmentShelfLayout
{
    internal const float RugWidth = 1747f;
    internal const float RugHeight = 978f;

    internal const float ColumnX = 1548f;
    internal const float SingleY = 680f;
    internal const float PairTopY = 590f;
    internal const float PairGap = 180f;

    internal const float Icon = 92f;
    internal const float SlotWidth = 100f;
    internal const float PriceGap = 4f;
    internal const float PriceHeight = 34f;
    internal const float CoinSize = 34f;
    internal const int PriceFontSize = 25;
    internal const int PriceOutline = 10;

    // Vanilla slots grow from 0.65 to 0.8 when hovered, about 1.23 times; the stand matches.
    internal const float HoverScale = 1.2f;

    internal static float CentreY(int index, int count) =>
        count <= 1 ? SingleY : PairTopY + Math.Clamp(index, 0, 1) * PairGap;

    // A slot's own rectangle at rest, in rug coordinates: the icon centred on the column, the price below.
    internal static HbrHudRect Slot(int index, int count) =>
        new(ColumnX - SlotWidth / 2f, CentreY(index, count) - Icon / 2f, SlotWidth, Icon + PriceGap + PriceHeight);

    // What a slot covers at the given scale, which grows around the icon's centre.
    internal static HbrHudRect Footprint(int index, int count, float scale)
    {
        HbrHudRect slot = Slot(index, count);
        float pivotX = ColumnX;
        float pivotY = CentreY(index, count);
        return new HbrHudRect(pivotX + (slot.X - pivotX) * scale, pivotY + (slot.Y - pivotY) * scale,
            slot.Width * scale, slot.Height * scale);
    }
}
