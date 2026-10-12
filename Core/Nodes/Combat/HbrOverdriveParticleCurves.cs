using System;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// UIBattleOverdrive's seven button-local particle systems. Times are seconds,
// particle distances are Unity units (UIParticle uses a 100-pixel scale).
internal static class HbrOverdriveParticleCurves
{
    internal readonly record struct Rgba(float R, float G, float B, float A);

    private static readonly float[] ColorTimes = { 0f, 7325f / 65535f, 17348f / 65535f, 32575f / 65535f, 1f };
    private static readonly Rgba[] Colors =
    {
        new(0.528301895f, 0.528301895f, 0f, 1f),
        new(0.192906708f, 0.801886797f, 0.471828848f, 1f),
        new(0.438679218f, 0.703069448f, 1f, 1f),
        new(0.642019331f, 0.448113203f, 1f, 1f),
        new(0.303196847f, 0.037610650f, 0.058709294f, 1f)
    };
    private static readonly float[] AlphaTimes = { 0f, 16191f / 65535f, 32382f / 65535f, 1f };
    private static readonly float[] Alphas = { 60f / 255f, 1f, 176f / 255f, 0f };

    internal static Rgba Color(float normalizedAge)
    {
        float t = Math.Clamp(normalizedAge, 0f, 1f);
        int colorIndex = Segment(ColorTimes, t);
        float blend = (t - ColorTimes[colorIndex]) / (ColorTimes[colorIndex + 1] - ColorTimes[colorIndex]);
        Rgba a = Colors[colorIndex], b = Colors[colorIndex + 1];
        int alphaIndex = Segment(AlphaTimes, t);
        float alphaBlend = (t - AlphaTimes[alphaIndex]) / (AlphaTimes[alphaIndex + 1] - AlphaTimes[alphaIndex]);
        return new Rgba(Lerp(a.R, b.R, blend), Lerp(a.G, b.G, blend), Lerp(a.B, b.B, blend),
            Lerp(Alphas[alphaIndex], Alphas[alphaIndex + 1], alphaBlend));
    }

    internal static float Contract(float normalizedAge)
    {
        float t = Math.Clamp(normalizedAge, 0f, 1f);
        return 1f - t * t;
    }

    internal static float ActivateRing(float normalizedAge)
    {
        float t = Math.Clamp(normalizedAge, 0f, 1f);
        return t <= 0.25f
            ? Hermite(t / 0.25f, 0f, 1f, 7.300744057f * 0.25f, 0.279151708f * 0.25f)
            : Hermite((t - 0.25f) / 0.75f, 1f, 1f, 0.279151708f * 0.75f, 0f);
    }

    internal static float ActivateVelocity(float normalizedAge)
    {
        float t = Math.Clamp(normalizedAge, 0f, 1f);
        return t <= 0.5f
            ? Hermite(t * 2f, 1f, 0.05f, -4.532127380f * 0.5f, -0.331235707f * 0.5f)
            : Hermite((t - 0.5f) * 2f, 0.05f, 0f, -0.331235707f * 0.5f, 0f);
    }

    internal static float ActivateDistance(float normalizedAge)
    {
        float t = Math.Clamp(normalizedAge, 0f, 1f);
        float first = 0.5f * IntegrateHermite(Math.Min(t * 2f, 1f), 1f, 0.05f,
            -4.532127380f * 0.5f, -0.331235707f * 0.5f);
        return t <= 0.5f ? first : first + 0.5f * IntegrateHermite((t - 0.5f) * 2f,
            0.05f, 0f, -0.331235707f * 0.5f, 0f);
    }

    internal static float PushDistance(float normalizedAge) =>
        7f * 0.25f * (normalizedAge * normalizedAge * normalizedAge / 3f - normalizedAge);

    internal static float StandbyDistance(float normalizedAge) => -0.5f * 1.5f * normalizedAge * normalizedAge / 2f;

    private static int Segment(float[] times, float t)
    {
        for (int i = 0; i < times.Length - 2; i++)
            if (t <= times[i + 1]) return i;
        return times.Length - 2;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Hermite(float t, float a, float b, float outgoing, float incoming) =>
        ((2f * a - 2f * b + outgoing + incoming) * t + (-3f * a + 3f * b - 2f * outgoing - incoming)) * t * t + outgoing * t + a;

    private static float IntegrateHermite(float t, float a, float b, float outgoing, float incoming) =>
        (2f * a - 2f * b + outgoing + incoming) * t * t * t * t / 4f +
        (-3f * a + 3f * b - 2f * outgoing - incoming) * t * t * t / 3f + outgoing * t * t / 2f + a * t;
}
