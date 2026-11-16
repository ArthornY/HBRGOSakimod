using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The frosted terminal circle behind the home icons, and the page back button when Chevron is set.
public partial class NManualFrost : Control
{
    private bool _hovered;

    internal bool Chevron { get; set; }

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
        Vector2 centre = Size / 2f;
        HbrManualPhoneArt.DrawFrost(this, centre, Mathf.Min(Size.X, Size.Y) / 2f, _hovered);
        if (!Chevron) return;
        // .back::before: an 18px corner, 4px stroke, turned to point left.
        float arm = Size.X * 0.2f;
        Vector2 tip = centre + new Vector2(-arm * 0.45f, 0f);
        Color stroke = new("effeff");
        DrawPolyline(new[] { tip + new Vector2(arm, -arm), tip, tip + new Vector2(arm, arm) }, stroke, 4f, true);
    }
}
