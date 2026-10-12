using System;
using System.Collections.Generic;
using Godot;

namespace HBRGOSakiMod.Core.Helpers;

internal static class SixSenseRadarGeometry
{
    internal const int AxisCount = 6;

    internal static float Radius(Vector2 size) =>
        Math.Max(0, Math.Min((size.X - 300) / 2f, (size.Y - 112) / 2f));

    internal static Vector2 LabelCenter(Vector2 size, int index) =>
        size / 2f + Direction(index) * (Radius(size) + 30);

    internal static Vector2 Direction(int index)
    {
        float angle = -MathF.PI / 2f + index * MathF.PI / 3f;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    internal static Vector2[] Ring(Vector2 center, float radius)
    {
        Vector2[] points = new Vector2[AxisCount];
        for (int i = 0; i < AxisCount; i++) points[i] = center + Direction(i) * radius;
        return points;
    }

    internal static Vector2[] Values(Vector2 center, float radius, IReadOnlyList<int> values, int maximum)
    {
        Vector2[] points = new Vector2[AxisCount];
        for (int i = 0; i < AxisCount; i++)
        {
            float ratio = maximum > 0 ? Math.Clamp(values[i] / (float)maximum, 0f, 1f) : 0f;
            points[i] = center + Direction(i) * radius * ratio;
        }
        return points;
    }
}
