using System;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// Glints on the water, denser towards the horizon, and flowers drifting down across the garden.
// Nemophila by default; cherry petals while Anon's memory is chosen. Everything is a function of time,
// so the drawing needs no per-particle state.
public partial class NMemoryGardenParticles : Control
{
    private const int GlintCount = 36;
    private const int PetalCount = 16;
    private static readonly Color NemophilaFill = new("a3d0f4");
    private static readonly Color NemophilaEdge = new("7aaedc");
    private static readonly Color NemophilaEye = new("5d86c0");
    private static readonly Color SakuraFill = new(242 / 255f, 167 / 255f, 195 / 255f, .85f);
    private static readonly Vector2[] PetalShape = Ellipse(new Vector2(0, -6), 4.2f, 5.6f);
    private readonly float[] _glints = Seeds(7, GlintCount * 5);
    private readonly float[] _petals = Seeds(11, PetalCount * 4);
    private double _time;

    internal float Horizon { get; set; } = 551;
    internal bool Sakura { get; set; }

    public NMemoryGardenParticles()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float width = Size.X, height = Size.Y;
        for (int i = 0; i < GlintCount; i++)
        {
            float[] s = _glints;
            int k = i * 5;
            // 7s ease-in-out: at the midpoint a glint dims to .15 and drifts 18px right.
            float phase = 0.5f - 0.5f * MathF.Cos((float)((_time + s[k] * 7) / 7 * Math.Tau));
            float y = Horizon + 10 + MathF.Pow(s[k + 1], 2.4f) * Math.Max(0, height - Horizon - 30);
            var rect = new Rect2(s[k + 2] * width + 18 * phase, y, 6 + s[k + 3] * 22, 2 + s[k + 4] * 1.5f);
            DrawRect(rect, new Color(1, 1, 1, Mathf.Lerp(1f, .15f, phase)));
        }
        for (int i = 0; i < PetalCount; i++)
        {
            float[] s = _petals;
            int k = i * 4;
            float zoom = .6f + s[k] * .8f;
            float duration = 9 + s[k + 1] * 8;
            float t = (float)((_time + s[k + 2] * duration) % duration / duration);
            // From 60px above the top to past the bottom, drifting 160px left and turning one and a half times.
            Vector2 at = new(s[k + 3] * (width + 160) - 160 * t, -60 + (height + 120) * t);
            float alpha = t < .1f ? t / .1f * .9f : .9f * (1 - (t - .1f) / .9f);
            DrawSetTransform(at, t * MathF.PI * 3, Vector2.One * zoom * 20 / 24f);
            if (Sakura) DrawSakura(alpha); else DrawNemophila(this, alpha);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    // Five pale-blue petals round a white eye, after the preview's 24x24 SVG, centred on the canvas's origin.
    internal static void DrawNemophila(CanvasItem canvas, float alpha)
    {
        for (int p = 0; p < 5; p++)
        {
            float angle = p * MathF.Tau / 5;
            Vector2[] petal = Array.ConvertAll(PetalShape, v => v.Rotated(angle));
            canvas.DrawColoredPolygon(petal, new Color(NemophilaFill, alpha));
            canvas.DrawPolyline(Close(petal), new Color(NemophilaEdge, alpha), .6f, true);
        }
        canvas.DrawCircle(Vector2.Zero, 3, new Color(1, 1, 1, alpha), true, -1, true);
        canvas.DrawCircle(Vector2.Zero, 1, new Color(NemophilaEye, alpha), true, -1, true);
    }

    // A pointed petal on a 14x10 box: sharp top-right and bottom-left corners, the other two rounded
    // with radii of 70% (border-radius: 70% 0 70% 0).
    private void DrawSakura(float alpha)
    {
        Vector2[] leaf = new Vector2[22];
        leaf[0] = new Vector2(-7, 5);
        leaf[11] = new Vector2(7, -5);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 9f * MathF.PI / 2;
            leaf[1 + i] = new Vector2(2.8f - 9.8f * MathF.Cos(a), 2 - 7 * MathF.Sin(a));
            leaf[12 + i] = new Vector2(-2.8f + 9.8f * MathF.Cos(a), -2 + 7 * MathF.Sin(a));
        }
        DrawColoredPolygon(leaf, new Color(SakuraFill, SakuraFill.A * alpha));
    }

    private static Vector2[] Ellipse(Vector2 centre, float rx, float ry)
    {
        var points = new Vector2[16];
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * MathF.Tau / points.Length;
            points[i] = centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
        }
        return points;
    }

    private static Vector2[] Close(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        points.CopyTo(closed, 0);
        closed[^1] = points[0];
        return closed;
    }

    private static float[] Seeds(ulong seed, int count)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        var values = new float[count];
        for (int i = 0; i < count; i++) values[i] = rng.Randf();
        return values;
    }
}
