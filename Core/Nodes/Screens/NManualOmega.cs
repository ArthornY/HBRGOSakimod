using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The faint Ω watermark of the 31O manual, taken from the Ω mark of the original handbook.
public partial class NManualOmega : Control
{
    private Vector2[] _stroke;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        // The preview's 128-unit path: M22 100 H46 C30 88 25 70 30 54 C36 34 50 24 64 24
        // C78 24 92 34 98 54 C103 70 98 88 82 100 H106.
        Curve2D curve = new();
        curve.AddPoint(new Vector2(22, 100));
        curve.AddPoint(new Vector2(46, 100), Vector2.Zero, new Vector2(30, 88) - new Vector2(46, 100));
        curve.AddPoint(new Vector2(30, 54), new Vector2(25, 70) - new Vector2(30, 54), new Vector2(36, 34) - new Vector2(30, 54));
        curve.AddPoint(new Vector2(64, 24), new Vector2(50, 24) - new Vector2(64, 24), new Vector2(78, 24) - new Vector2(64, 24));
        curve.AddPoint(new Vector2(98, 54), new Vector2(92, 34) - new Vector2(98, 54), new Vector2(103, 70) - new Vector2(98, 54));
        curve.AddPoint(new Vector2(82, 100), new Vector2(98, 88) - new Vector2(82, 100));
        curve.AddPoint(new Vector2(106, 100));
        _stroke = curve.Tessellate(6, 3f);
    }

    public override void _Draw()
    {
        if (_stroke == null) return;
        float scale = Size.X / 128f;
        var points = new Vector2[_stroke.Length];
        for (int i = 0; i < points.Length; i++) points[i] = _stroke[i] * scale;
        DrawPolyline(points, new Color("a9c6f2"), 5f * scale, true);
    }
}
