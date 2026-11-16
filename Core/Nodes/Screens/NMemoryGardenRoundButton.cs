using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Round header button on a navy disc: the original back arrow, or a white cross for closing.
public partial class NMemoryGardenRoundButton : Button
{
    private Texture2D _sprite;
    private Texture2D _hoverSprite;
    private bool _cross;

    internal static NMemoryGardenRoundButton Back() => new()
    {
        _sprite = HbrMemoryGardenTheme.Sprite("ButtonBackDefault")
    };

    internal static NMemoryGardenRoundButton Close() => new()
    {
        _sprite = HbrMemoryGardenTheme.Sprite("ButtonFourthCircleNormal"),
        _hoverSprite = HbrMemoryGardenTheme.Sprite("ButtonFourthCircleActive"),
        _cross = true
    };

    public NMemoryGardenRoundButton()
    {
        Flat = true;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(104, 104);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }

    public override void _Draw()
    {
        Vector2 centre = Size / 2;
        float unit = Size.X / 104f;
        bool lit = IsHovered() || HasFocus();
        if (lit && !_cross)
            DrawCircle(centre, 50 * unit, new Color(HbrMemoryGardenTheme.Pink, .35f), true, -1, true);
        DrawCircle(centre, 44 * unit, HbrMemoryGardenTheme.Navy, true, -1, true);
        Texture2D sprite = lit && _hoverSprite != null ? _hoverSprite : _sprite;
        if (sprite != null) DrawTextureRect(sprite, new Rect2(Vector2.Zero, Size), false);
        if (!_cross) return;
        Vector2 arm = new Vector2(18, 18) * unit;
        DrawLine(centre - arm, centre + arm, Colors.White, 4 * unit, true);
        DrawLine(centre + new Vector2(-arm.X, arm.Y), centre + new Vector2(arm.X, -arm.Y), Colors.White, 4 * unit, true);
    }
}
