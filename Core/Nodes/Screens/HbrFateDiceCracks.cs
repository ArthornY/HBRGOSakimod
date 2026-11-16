using Godot;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The fate dice's cracks, drawn in code over the dice icon (design v3, section 5: 「裂痕：在骰子图标上叠一道
// 深红裂纹，用程序叠，不另画」). Crack i is the same jagged line every time: it starts at the rim of the dice and
// runs inward with one short branch, each new crack turned a golden angle from the last so they spread around
// the dice. Points are in a unit square, so the dice badge and a cracked slot draw it at any size. The shape
// comes from a fixed integer hash, so drawing never touches the run's random numbers.
internal static class HbrFateDiceCracks
{
    // Cracks drawn on the dice; more than this read as one shattered face, and the page states the count.
    internal const int MaxDrawn = 9;

    private const int Segments = 5;
    private const float GoldenAngle = 137.50776f;
    private static readonly Vector2 Centre = new(0.5f, 0.52f);
    private const float Rim = 0.36f;

    // Deep red with a near-black shadow and a thin lit core, so the crack reads on the light blue dice.
    internal static readonly Color Shadow = new(0.07f, 0.01f, 0.02f, 0.9f);
    internal static readonly Color Body = new("8E1620");
    internal static readonly Color Glint = new(0.94f, 0.33f, 0.33f, 0.8f);

    internal static (Vector2[] Line, Vector2[] Branch) Path(int index)
    {
        float angle = Mathf.DegToRad(-100f + index * GoldenAngle);
        Vector2 outward = new(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 start = Centre + outward * Rim;
        Vector2 heading = (-outward).Rotated(Mathf.DegToRad(Lerp(-18f, 18f, Hash(index, 1))));
        float length = 0.27f + 0.06f * Hash(index, 2);

        var line = new Vector2[Segments + 1];
        line[0] = start;
        for (int k = 0; k < Segments; k++)
        {
            float turn = Lerp(14f, 32f, Hash(index, 10 + k)) * (k % 2 == 0 ? 1f : -1f);
            float step = length / Segments * Lerp(0.8f, 1.2f, Hash(index, 20 + k));
            line[k + 1] = line[k] + heading.Rotated(Mathf.DegToRad(turn)) * step;
        }

        Vector2 fork = line[2];
        Vector2 along = (line[3] - line[2]).Normalized();
        float side = Hash(index, 3) < 0.5f ? -1f : 1f;
        Vector2 branchHeading = along.Rotated(Mathf.DegToRad(side * Lerp(35f, 50f, Hash(index, 4))));
        Vector2 mid = fork + branchHeading * 0.045f;
        Vector2 end = mid + branchHeading.Rotated(Mathf.DegToRad(-side * 20f)) * 0.04f;
        return (line, new[] { fork, mid, end });
    }

    // Draws crack i inside area from a node's _Draw, tapering from width at the rim to about half at the tip.
    internal static void Draw(CanvasItem canvas, Rect2 area, int index, float width)
    {
        (Vector2[] line, Vector2[] branch) = Path(index);
        Stroke(canvas, area, line, width);
        Stroke(canvas, area, branch, width * 0.6f);
    }

    private static void Stroke(CanvasItem canvas, Rect2 area, Vector2[] points, float width)
    {
        int last = points.Length - 1;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int k = 0; k < last; k++)
            {
                float taper = 1f - 0.5f * k / last;
                Vector2 a = area.Position + points[k] * area.Size;
                Vector2 b = area.Position + points[k + 1] * area.Size;
                switch (pass)
                {
                    case 0: canvas.DrawLine(a, b, Shadow, width * taper * 1.9f, true); break;
                    case 1: canvas.DrawLine(a, b, Body, width * taper, true); break;
                    default: canvas.DrawLine(a, b, Glint, Mathf.Max(1f, width * taper * 0.38f), true); break;
                }
            }
        }
    }

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;

    // A small integer hash in [0, 1).
    private static float Hash(int index, int salt)
    {
        uint x = unchecked((uint)(index * 73856093) ^ (uint)(salt * 19349663) ^ 0x9E3779B9u);
        x ^= x >> 16;
        x = unchecked(x * 0x7FEB352Du);
        x ^= x >> 15;
        x = unchecked(x * 0x846CA68Bu);
        x ^= x >> 16;
        return (x & 0xFFFFFF) / 16777216f;
    }
}
