using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The portrait window's circle: draws the #2a4c5c disc and clips the head crop inside it.
public partial class NManualPortraitMask : Control
{
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipChildren = ClipChildrenMode.AndDraw;
    }

    public override void _Draw() =>
        DrawCircle(Size / 2f, Mathf.Min(Size.X, Size.Y) / 2f, new Color("2a4c5c"), true, -1f, true);
}
