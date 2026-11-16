using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Rounded pill behind the manual's text buttons: the cyan gradient of "确定" and the active toggle,
// or the translucent teal of the secondary buttons.
public partial class NManualPill : Control
{
    internal enum Look
    {
        Primary,
        PrimaryDisabled,
        Secondary,
        Clear
    }

    private Look _look = Look.Primary;
    private bool _hovered;

    internal Look Style
    {
        get => _look;
        set
        {
            if (_look == value) return;
            _look = value;
            QueueRedraw();
        }
    }

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

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        Rect2 rect = new(Vector2.Zero, Size);
        float radius = Size.Y / 2f;
        switch (_look)
        {
            case Look.Primary:
                // box-shadow 0 0 0 3px rgba(220,255,255,.5); hover: white ring and a cyan glow.
                if (_hovered) DrawRing(rect.Grow(10f), radius + 10f, new Color(0.55f, 0.94f, 0.98f, 0.25f));
                DrawRing(rect.Grow(3f), radius + 3f, _hovered ? Colors.White : new Color(0.86f, 1f, 1f, 0.5f));
                HbrManualPhoneArt.DrawPill(this, rect, new Color("8af1f6"), new Color("3fb7c7"), radius);
                break;
            case Look.PrimaryDisabled:
                DrawRing(rect.Grow(2f), radius + 2f, new Color(0.78f, 0.86f, 0.9f, 0.2f));
                HbrManualPhoneArt.DrawPill(this, rect, new Color("4b5a63"), new Color("33404a"), radius);
                break;
            case Look.Secondary:
                HbrManualPhoneArt.DrawPill(this, rect.Grow(1f), new Color(0.52f, 0.91f, 0.94f, 0.6f),
                    new Color(0.52f, 0.91f, 0.94f, 0.6f), radius + 1f);
                Color fill = _hovered ? new Color(0.47f, 0.78f, 0.84f, 0.55f) : new Color(0.37f, 0.66f, 0.73f, 0.35f);
                HbrManualPhoneArt.DrawPill(this, rect, fill, fill, radius);
                break;
            default:
                if (_hovered)
                {
                    Color hover = new(0.59f, 0.94f, 0.98f, 0.12f);
                    HbrManualPhoneArt.DrawPill(this, rect, hover, hover, radius);
                }
                break;
        }
    }

    private void DrawRing(Rect2 rect, float radius, Color color) =>
        HbrManualPhoneArt.DrawPill(this, rect, color, color, radius);
}
