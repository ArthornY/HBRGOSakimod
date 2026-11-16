using System;
using System.Linq;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The manual's hexagonal radar, shared by the home overview and the six senses page: an upper dark
// half for black Saki's senses and a pale lower half for white Saki's, reference rings, a dashed
// threshold ring, the confirmed polygon in cyan and pending allocations in pink.
public partial class NManualSenseRadar : Control
{
    private static readonly Color Cyan = new(HbrManualPhoneLayout.Cyan);
    private static readonly Color Cyan2 = new(HbrManualPhoneLayout.Cyan2);
    private static readonly Color Pink = new(HbrManualPhoneLayout.Pink);
    private static readonly Color Muted = new(HbrManualPhoneLayout.Muted);

    internal bool Compact { get; set; }
    internal Vector2 Centre { get; set; }
    internal float Radius { get; set; }
    internal float HalfRadius { get; set; }
    internal float[] Values { get; set; } = new float[6];
    internal float[] Pending { get; set; } = new float[6];
    internal bool[] Drafted { get; set; } = new bool[6];
    internal float Max { get; set; } = HbrManualPhoneLayout.MaxLevel;
    internal float[] Rings { get; set; } = Array.Empty<float>();
    internal float Dashed { get; set; } = HbrManualPhoneLayout.Threshold;
    internal int Selected { get; set; } = -1;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    private Vector2 At(int index, float value)
    {
        var (x, y) = HbrManualPhoneLayout.Point(Centre.X, Centre.Y, index,
            HbrManualPhoneLayout.RadiusOf(value, Max, Radius));
        return new Vector2(x, y);
    }

    private Vector2[] Hexagon(Func<int, float> value) => Enumerable.Range(0, 6).Select(i => At(i, value(i))).ToArray();

    private static Vector2[] Closed(Vector2[] points) => points.Append(points[0]).ToArray();

    public override void _Draw()
    {
        DrawHalves();
        float[] rings = Rings;
        for (int k = 0; k < rings.Length; k++)
        {
            bool outer = k == rings.Length - 1;
            float ring = rings[k];
            DrawPolyline(Closed(Hexagon(_ => ring)), new Color(Cyan2, outer ? 0.55f : 0.22f), outer ? 2f : 1f, true);
        }
        Vector2[] dashed = Closed(Hexagon(_ => Dashed));
        for (int i = 0; i < 6; i++)
        {
            DrawDashedLine(dashed[i], dashed[i + 1], new Color(Cyan, Compact ? 0.65f : 0.7f), Compact ? 1.5f : 2f,
                Compact ? 4f : 6f, true, true);
        }
        for (int i = 0; i < 6; i++)
        {
            bool grey = Compact && HbrManualPhoneLayout.Tiers[i] == HbrSenseTier.Grey;
            Color axis = grey ? new Color(new Color("b9bdcf"), 0.7f) : new Color(Cyan2, Compact ? 0.22f : 0.25f);
            DrawLine(Centre, At(i, Max), axis, 1f, true);
        }

        bool anyPending = Drafted.Any(drafted => drafted);
        if (anyPending || !Compact)
        {
            Vector2[] pending = Hexagon(i => Pending[i]);
            if (anyPending) FillPolygon(pending, new Color(Pink, Compact ? 0.16f : 0.14f));
            if (anyPending) DrawPolyline(Closed(pending), new Color(Pink, Compact ? 1f : 0.9f), 2f, true);
        }
        Vector2[] values = Hexagon(i => Values[i]);
        FillPolygon(values, new Color(Cyan2, Compact ? 0.3f : 0.28f));
        DrawPolyline(Closed(values), Cyan, Compact ? 2.5f : 3f, true);

        if (Compact) return;
        for (int i = 0; i < 6; i++)
            DrawCircle(At(i, Pending[i]), i == Selected ? 8f : 5f, Drafted[i] ? Pink : Colors.White);
        Font font = HbrManualPhoneArt.Font;
        foreach (float ring in rings)
        {
            float radius = HbrManualPhoneLayout.RadiusOf(ring, Max, Radius);
            DrawString(font, Centre + new Vector2(6f, -radius + 18f), Math.Round(ring).ToString(),
                HorizontalAlignment.Left, -1, 16, Muted);
        }
    }

    // Upper half #1a0712 at .6, lower half #eef1fb at .13, split by a dashed line.
    private void DrawHalves()
    {
        float r = HalfRadius;
        Vector2[] upper = Enumerable.Range(0, 33).Select(i => Centre + Vector2.FromAngle(Mathf.Pi + i * Mathf.Pi / 32f) * r).ToArray();
        Vector2[] lower = Enumerable.Range(0, 33).Select(i => Centre + Vector2.FromAngle(i * Mathf.Pi / 32f) * r).ToArray();
        DrawColoredPolygon(upper, new Color(new Color("1a0712"), 0.6f));
        DrawColoredPolygon(lower, new Color(new Color("eef1fb"), 0.13f));
        DrawDashedLine(Centre - new Vector2(r, 0f), Centre + new Vector2(r, 0f), new Color(new Color("c9cbe0"), 0.35f),
            1f, 4f, true, true);
    }

    // Sectors from the centre, so collapsed axes never make a degenerate polygon.
    private void FillPolygon(Vector2[] points, Color color)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 a = points[i] - Centre, b = points[(i + 1) % 6] - Centre;
            if (Math.Abs(a.Cross(b)) > 0.01f) DrawColoredPolygon(new[] { Centre, points[i], points[(i + 1) % 6] }, color);
        }
    }
}
