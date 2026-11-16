using System;
using Godot;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// The summoning of Magic Sword Lucky Seven, drawn in code: 「黑色能量撕开天空」 as a jagged tear with a wormhole
// swirl, black lightning around it, and the shockwave where the sword lands. One instance draws the dark body
// with normal blending; a second draws rims, swirl and shock additively so they glow.
public partial class NHbrLuckySevenRift : Control
{
    // The tear's outline: angle steps around the centre with jagged radius factors, fixed so every machine
    // and every show draws the same shape without touching any random number generator. A tall narrow ellipse
    // with mild jags reads as a rip in the sky; stronger jags on a rounder one read as a star.
    private static readonly float[] Jag = { 1.0f, 0.8f, 1.1f, 0.84f, 0.97f, 0.76f, 1.12f, 0.82f, 1.03f, 0.78f, 1.08f, 0.86f };
    private const float TearHalfWidth = 95f;
    private const float TearHalfHeight = 220f;
    private static readonly float[] BoltAngles = { -150f, -118f, -64f, -32f, 160f, 22f };

    private readonly bool _glow;

    // Driven by NHbrLuckySevenFinisher each frame.
    internal float Open;        // 0..1 tear size
    internal float Fade = 1f;   // whole-layer alpha
    internal float Clock;       // show clock, for the swirl and flicker
    internal float Shock = -1f; // seconds since the sword landed, below 0 before
    internal Vector2 RiftCentre;
    internal Vector2 GroundPoint;
    internal float Scale1080 = 1f;

    public NHbrLuckySevenRift() : this(false) { }

    internal NHbrLuckySevenRift(bool glow)
    {
        _glow = glow;
        MouseFilter = MouseFilterEnum.Ignore;
        if (glow) Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    public override void _Draw()
    {
        if (Fade <= 0f) return;
        Color violet = new(HbrLuckySevenTimeline.VioletHex);
        Color crimson = new(HbrLuckySevenTimeline.CrimsonHex);
        if (Open > 0f) DrawTear(violet, crimson);
        if (Shock >= 0f) DrawShock(violet, crimson);
    }

    private void DrawTear(Color violet, Color crimson)
    {
        float s = Scale1080;
        float width = TearHalfWidth * s * Open, height = TearHalfHeight * s * Open;
        Vector2[] outline = new Vector2[Jag.Length + 1];
        for (int i = 0; i < Jag.Length; i++)
        {
            float a = Mathf.Tau * i / Jag.Length;
            // Taller than wide, and a slow ripple so the edge does not look pasted on.
            float ripple = 1f + 0.05f * Mathf.Sin(Clock * 9f + i * 1.7f);
            outline[i] = RiftCentre + new Vector2(Mathf.Cos(a) * width, Mathf.Sin(a) * height) * Jag[i] * ripple;
        }
        outline[Jag.Length] = outline[0];
        if (!_glow)
        {
            Color body = new(HbrLuckySevenTimeline.RiftHex) { A = Fade };
            DrawColoredPolygon(outline[..Jag.Length], body);
            // Black lightning crawling out of the tear.
            for (int b = 0; b < BoltAngles.Length; b++)
                DrawBolt(b, new Color(0.02f, 0.01f, 0.04f, 0.85f * Fade), 5f * s);
            return;
        }

        // Rim: a wide crimson haze under a violet edge.
        DrawPolyline(outline, crimson with { A = 0.45f * Fade }, 16f * s, true);
        DrawPolyline(outline, violet with { A = 0.9f * Fade }, 5f * s, true);
        // Wormhole swirl: rotating arcs inside the tear.
        for (int ring = 0; ring < 3; ring++)
        {
            float radius = (24f + ring * 18f) * s * Open;
            float start = Clock * (2.4f - ring * 0.6f) + ring * 1.3f;
            DrawArc(RiftCentre, radius, start, start + Mathf.Pi * 1.2f, 24, violet with { A = (0.75f - ring * 0.18f) * Fade },
                (4f - ring) * s, true);
        }
        for (int b = 0; b < BoltAngles.Length; b++)
            DrawBolt(b, violet with { A = 0.6f * Fade * (0.6f + 0.4f * Mathf.Sin(Clock * 23f + b)) }, 2f * s);
    }

    private void DrawBolt(int index, Color colour, float width)
    {
        float s = Scale1080;
        float angle = Mathf.DegToRad(BoltAngles[index]);
        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 normal = new(-direction.Y, direction.X);
        float length = (150f + 40f * (index % 3)) * s * Open;
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float along = length * i / (points.Length - 1);
            float zig = (i % 2 == 0 ? 1f : -1f) * (i == 0 ? 0f : 14f * s) * Mathf.Sin(Clock * 17f + index);
            points[i] = RiftCentre + direction * (60f * s * Open + along) + normal * zig;
        }
        DrawPolyline(points, colour, width, true);
    }

    private void DrawShock(Color violet, Color crimson)
    {
        float s = Scale1080;
        float p = Math.Clamp(Shock / 0.45f, 0f, 1f);
        if (p >= 1f) return;
        float rx = (60f + 380f * p) * s, ry = rx * 0.22f;
        float alpha = (1f - p) * Fade;
        Vector2[] ring = new Vector2[33];
        for (int i = 0; i < ring.Length; i++)
        {
            float a = Mathf.Tau * i / (ring.Length - 1);
            ring[i] = GroundPoint + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        if (!_glow)
        {
            DrawPolyline(ring, new Color(0f, 0f, 0f, 0.5f * alpha), 10f * s, true);
            return;
        }
        DrawPolyline(ring, crimson with { A = 0.7f * alpha }, 8f * s, true);
        DrawPolyline(ring, violet with { A = alpha }, 3f * s, true);
        // Sparks thrown up from the impact.
        for (int i = 0; i < 9; i++)
        {
            float a = Mathf.DegToRad(-160f + i * 17.5f);
            Vector2 direction = new(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 from = GroundPoint + direction * (30f + 160f * p) * s;
            DrawLine(from, from + direction * 70f * (1f - p) * s, (i % 2 == 0 ? crimson : violet) with { A = alpha }, 3f * s, true);
        }
    }
}
