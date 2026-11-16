using System;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The nemophila on the garden's memory counter: 34px inside a 40px slot, as in the preview.
public partial class NMemoryGardenFlowerIcon : Control
{
    public NMemoryGardenFlowerIcon()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float scale = Math.Min(Size.X, Size.Y) / 40f * 34f / 24f;
        DrawSetTransform(Size / 2, 0, Vector2.One * scale);
        NMemoryGardenParticles.DrawNemophila(this, 1f);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}
