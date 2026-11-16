using System;
using System.Threading.Tasks;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The garden's confirm effect (ui-concepts/memory-garden-20261104). The card flashes white; when a memory is
// sublimated it then breaks into 32 pieces of glass; a rainbow ring spreads and fades while a rainbow sheen
// sweeps the screen. The overlay swallows input until it ends.
public partial class NMemoryGardenSublimation : Control
{
    private const string RainbowShader = "res://shaders/hbr_memory_garden_rainbow.gdshader";
    private const int ShardColumns = 4;
    private const int ShardRows = 4;
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // `card` is the card node to hide while its glass flies; `frame` gives the card's on-screen rectangle.
    internal static Task Play(Control host, Control card, Control frame, bool shatter)
    {
        var overlay = new NMemoryGardenSublimation { Name = "MemoryGardenSublimation", MouseFilter = MouseFilterEnum.Stop };
        host.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Rect2 rect = overlay.GetGlobalTransform().AffineInverse() * frame.GetGlobalRect();
        overlay.Flash(rect);
        if (shatter) overlay.Shatter(rect, frame.GetGlobalRect(), card);
        overlay.Ring(rect, .2);
        overlay.Sheen(.2);
        double length = shatter ? 1.4 : 1.0;
        overlay.GetTree().CreateTimer(length).Timeout += () => overlay._done.TrySetResult();
        return overlay._done.Task;
    }

    public override void _ExitTree() => _done.TrySetResult();

    private void Flash(Rect2 rect)
    {
        var flash = new Panel { MouseFilter = MouseFilterEnum.Ignore, Position = rect.Position, Size = rect.Size };
        flash.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.White,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ShadowColor = Colors.White, ShadowSize = 50, AntiAliasing = true
        });
        flash.Modulate = Colors.Transparent;
        AddChild(flash);
        Tween tween = CreateTween();
        tween.TweenProperty(flash, "modulate:a", .95f, .1);
        tween.TweenProperty(flash, "modulate:a", 0f, .24).SetEase(Tween.EaseType.Out);
    }

    // Splits a screenshot of the card into a jittered 4x4 grid, two triangles per cell, and throws them outward.
    private void Shatter(Rect2 rect, Rect2 global, Control card)
    {
        Viewport viewport = GetViewport();
        Image shot = viewport.GetTexture().GetImage();
        Rect2 visible = viewport.GetVisibleRect();
        Vector2 toPixels = new Vector2(shot.GetWidth(), shot.GetHeight()) / visible.Size;
        var region = new Rect2I((Vector2I)((global.Position - visible.Position) * toPixels), (Vector2I)(global.Size * toPixels));
        region = region.Intersection(new Rect2I(Vector2I.Zero, shot.GetSize()));
        if (region.Size.X <= 0 || region.Size.Y <= 0) return;
        ImageTexture texture = ImageTexture.CreateFromImage(shot.GetRegion(region));
        Vector2 texScale = (Vector2)region.Size / rect.Size;
        card.Modulate = Colors.Transparent;

        var rng = new RandomNumberGenerator { Seed = 1405 };
        var grid = new Vector2[ShardColumns + 1, ShardRows + 1];
        for (int x = 0; x <= ShardColumns; x++)
        for (int y = 0; y <= ShardRows; y++)
        {
            Vector2 jitter = x > 0 && x < ShardColumns && y > 0 && y < ShardRows
                ? new Vector2(rng.RandfRange(-.3f, .3f), rng.RandfRange(-.3f, .3f)) : Vector2.Zero;
            grid[x, y] = new Vector2((x + jitter.X) / ShardColumns, (y + jitter.Y) / ShardRows) * rect.Size;
        }
        Vector2 centre = rect.Size / 2;
        for (int x = 0; x < ShardColumns; x++)
        for (int y = 0; y < ShardRows; y++)
        {
            Vector2 a = grid[x, y], b = grid[x + 1, y], c = grid[x + 1, y + 1], d = grid[x, y + 1];
            Throw(rect, texture, texScale, centre, rng, a, b, c);
            Throw(rect, texture, texScale, centre, rng, a, c, d);
        }
    }

    private void Throw(Rect2 rect, Texture2D texture, Vector2 texScale, Vector2 centre, RandomNumberGenerator rng,
        params Vector2[] corners)
    {
        Vector2 middle = (corners[0] + corners[1] + corners[2]) / 3;
        var shard = new Polygon2D
        {
            Texture = texture,
            Position = rect.Position + middle,
            Polygon = Array.ConvertAll(corners, p => p - middle),
            UV = Array.ConvertAll(corners, p => p * texScale)
        };
        AddChild(shard);
        Vector2 outward = (middle - centre).Normalized();
        if (outward == Vector2.Zero) outward = Vector2.Up;
        Vector2 target = shard.Position + outward.Rotated(rng.RandfRange(-.5f, .5f)) * rng.RandfRange(220, 520)
            + new Vector2(0, rng.RandfRange(40, 160));
        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(shard, "position", target, 1).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(shard, "rotation", rng.RandfRange(-3.5f, 3.5f), 1).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(shard, "scale", Vector2.One * .9f, 1);
        tween.TweenProperty(shard, "modulate:a", 0f, .45).SetDelay(.55);
    }

    private void Ring(Rect2 rect, double delay)
    {
        float side = Math.Max(rect.Size.X, rect.Size.Y) * 1.4f;
        var ring = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(RainbowShader) },
            Size = new Vector2(side, side),
            Position = rect.GetCenter() - new Vector2(side, side) / 2,
            PivotOffset = new Vector2(side, side) / 2,
            Scale = Vector2.One * .2f,
            Modulate = Colors.Transparent
        };
        AddChild(ring);
        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(ring, "scale", Vector2.One * 1.35f, 1).SetDelay(delay).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(ring, "modulate:a", .95f, .18).SetDelay(delay);
        tween.TweenProperty(ring, "modulate:a", 0f, .82).SetDelay(delay + .18).SetEase(Tween.EaseType.Out);
    }

    private void Sheen(double delay)
    {
        var sheen = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>(RainbowShader) },
            Modulate = Colors.Transparent
        };
        ((ShaderMaterial)sheen.Material).SetShaderParameter("sheen", true);
        AddChild(sheen);
        sheen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // This overlay has just been added and not laid out yet; the viewport gives its width.
        float width = GetViewportRect().Size.X;
        sheen.Position = new Vector2(-.55f * width, 0);
        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(sheen, "position:x", .55f * width, 1.05).SetDelay(delay).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(sheen, "modulate:a", 1f, .3).SetDelay(delay);
        tween.TweenProperty(sheen, "modulate:a", 0f, .75).SetDelay(delay + .3);
    }
}
