using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The status pill under a memory card. Its origin is the card's bottom centre, in the card's own units.
public partial class NMemoryGardenRibbon : Control
{
    private const int FontSize = 26;
    private const float Height = 39;
    private const float Padding = 24;
    private string _text = string.Empty;
    private MemoryGardenSlot _slot;

    public NMemoryGardenRibbon()
    {
        Name = "Ribbon";
        MouseFilter = MouseFilterEnum.Ignore;
    }

    internal void Display(MemoryGardenSlot slot, string text)
    {
        text = ThirtyOneXLanguagePrankHelper.GetOriginalDisplay(text);
        if (_slot == slot && _text == text) return;
        (_slot, _text) = (slot, text);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Font font = GetThemeFont("font", "Label");
        if (font == null || string.IsNullOrEmpty(_text)) return;
        string displayText = _text;
        ThirtyOneXLanguagePrankHelper.PrepareDrawText(this, font, ref displayText);
        float width = font.GetStringSize(displayText, HorizontalAlignment.Left, -1, FontSize).X + 2 * Padding;
        var rect = new Rect2(-width / 2, 0, width, Height);
        (Color left, Color right, Color ink) = _slot switch
        {
            MemoryGardenSlot.Obtain => (new Color("ff3f9f"), new Color("ff8fc8"), Colors.White),
            MemoryGardenSlot.Upgrade => (new Color("8a5cff"), new Color("c59bff"), Colors.White),
            MemoryGardenSlot.Complete => (new Color("f1f4fb"), Colors.White, HbrMemoryGardenTheme.Ink2),
            MemoryGardenSlot.Ghost => (new Color(46 / 255f, 52 / 255f, 82 / 255f, .7f), new Color(46 / 255f, 52 / 255f, 82 / 255f, .7f), new Color("b9bfd6")),
            _ => (new Color(46 / 255f, 52 / 255f, 82 / 255f, .92f), new Color(46 / 255f, 52 / 255f, 82 / 255f, .92f), new Color("dfe3f0"))
        };
        DrawStyleBox(new StyleBoxFlat
        {
            BgColor = left,
            CornerRadiusTopLeft = (int)(Height / 2), CornerRadiusTopRight = (int)(Height / 2),
            CornerRadiusBottomLeft = (int)(Height / 2), CornerRadiusBottomRight = (int)(Height / 2),
            ShadowColor = new Color(60 / 255f, 80 / 255f, 130 / 255f, .2f),
            ShadowSize = 15,
            ShadowOffset = new Vector2(0, 4.5f),
            AntiAliasing = true
        }, rect);
        if (left != right) DrawGradientPill(rect, left, right);
        if (_slot == MemoryGardenSlot.Complete)
            DrawPolyline(Pill(rect, closed: true), HbrMemoryGardenTheme.Rim, 1.5f, true);
        float ascent = font.GetAscent(FontSize);
        DrawString(font, new Vector2(rect.Position.X + Padding, (Height - font.GetHeight(FontSize)) / 2 + ascent), displayText,
            HorizontalAlignment.Left, -1, FontSize, ink);
    }

    private void DrawGradientPill(Rect2 rect, Color left, Color right)
    {
        Vector2[] points = Pill(rect, closed: false);
        var colors = new Color[points.Length];
        for (int i = 0; i < points.Length; i++)
            colors[i] = left.Lerp(right, (points[i].X - rect.Position.X) / rect.Size.X);
        DrawPolygon(points, colors);
    }

    // Outline of a pill: two half circles joined by straight edges.
    private static Vector2[] Pill(Rect2 rect, bool closed)
    {
        const int steps = 12;
        float r = rect.Size.Y / 2;
        Vector2 leftCentre = rect.Position + new Vector2(r, r), rightCentre = rect.End - new Vector2(r, r);
        var points = new Vector2[2 * (steps + 1) + (closed ? 1 : 0)];
        for (int i = 0; i <= steps; i++)
        {
            float a = MathF.PI / 2 + i * MathF.PI / steps;
            points[i] = leftCentre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            points[steps + 1 + i] = rightCentre + new Vector2(MathF.Cos(a + MathF.PI), MathF.Sin(a + MathF.PI)) * r;
        }
        if (closed) points[^1] = points[0];
        return points;
    }
}
