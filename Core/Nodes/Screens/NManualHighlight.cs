using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The soft rounded highlight behind clickable manual entries (stat dock, radar vertices):
// faint on hover, brighter with a ring when selected.
public partial class NManualHighlight : Control
{
    private bool _hovered;
    private bool _selected;

    internal float Radius { get; set; } = 18f;

    internal bool Hovered
    {
        get => _hovered;
        set
        {
            if (_hovered == value) return;
            _hovered = value;
            QueueRedraw();
        }
    }

    internal bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            QueueRedraw();
        }
    }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (!_hovered && !_selected) return;
        Rect2 rect = new(Vector2.Zero, Size);
        if (_selected)
        {
            Color ring = new(0.59f, 0.94f, 0.98f, 0.7f);
            HbrManualPhoneArt.DrawPill(this, rect, ring, ring, Radius);
            Color fill = new(0.08f, 0.2f, 0.27f, 1f);
            HbrManualPhoneArt.DrawPill(this, rect.Grow(-2f), fill, fill, Radius - 2f);
            Color tint = new(0.59f, 0.94f, 0.98f, 0.16f);
            HbrManualPhoneArt.DrawPill(this, rect.Grow(-2f), tint, tint, Radius - 2f);
            return;
        }
        Color hover = new(0.59f, 0.94f, 0.98f, 0.1f);
        HbrManualPhoneArt.DrawPill(this, rect, hover, hover, Radius);
    }
}
