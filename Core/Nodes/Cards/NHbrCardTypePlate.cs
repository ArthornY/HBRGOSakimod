using Godot;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// Dark slanted plate behind the type label, with a coloured bar on its left edge. Drawn as geometry
// rather than a nine-patch so it stays sharp at any card scale.
public partial class NHbrCardTypePlate : Control
{
    private static readonly Color Fill = new(HbrCardStyleLayout.PlateColor);

    private Color _accent;

    public NHbrCardTypePlate()
    {
        Name = "HbrTypePlate";
        MouseFilter = MouseFilterEnum.Ignore;
    }

    internal void Display(Color accent)
    {
        if (_accent != accent)
        {
            _accent = accent;
            QueueRedraw();
        }
        Visible = true;
    }

    public override void _Draw()
    {
        float width = Size.X, height = Size.Y, slant = HbrCardStyleLayout.PlateSlant;
        Vector2 top = new(width, 0), bottom = new(width - slant, height);
        DrawColoredPolygon(new[] { Vector2.Zero, top, bottom, new Vector2(0, height) }, Fill);
        // Polygons are not antialiased; trace the slanted edge so it does not stair-step when the card is zoomed.
        DrawLine(top, bottom, Fill, 1, antialiased: true);
        DrawRect(new Rect2(0, 0, HbrCardStyleLayout.PlateBarWidth, height), _accent);
    }
}
