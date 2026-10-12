using System;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Values come from UIBattleOverdrive.prefab and its AnimationClips.
// Asset IDs, original keyframes and extraction hashes are recorded beside the sprites.
internal static class HbrOverdriveGaugeVisuals
{
    internal const float SourceWidth = 690.6f;
    internal const float SourceHeight = 192f;
    internal const float DisplayScale = 0.53f;
    internal const float BarX = 29.93f;
    internal const float BarY = 78f;
    internal const float BarWidth = 530f;
    internal const float BarHeight = 36f;
    internal const float FillInset = 5f;
    internal const float FillWidth = 520f;
    internal const float ButtonX = 498.6f;
    internal const float LevelUpDuration = 0.5f;
    internal const float FlashDuration = 0.75f;

    internal static float LevelFill(float charge, int chargePerLevel, int layer) =>
        chargePerLevel <= 0 ? 0f : Math.Clamp(charge / chargePerLevel - layer, 0f, 1f);

    internal static float Flash(float seconds)
    {
        float time = Math.Max(0f, seconds) % FlashDuration;
        return time <= 0.25f
            ? 0.5f * Smooth(time / 0.25f)
            : 0.5f * (1f - Smooth((time - 0.25f) / 0.5f));
    }

    internal static float LevelRingScale(float elapsed) =>
        1f + 2f * Smooth((elapsed - 5f / 12f) * 12f);

    internal static float LevelRingAlpha(float elapsed)
    {
        if (elapsed < 0f || elapsed >= LevelUpDuration) return 0f;
        return elapsed <= 5f / 12f
            ? Smooth((elapsed - 1f / 3f) * 12f)
            : 1f - Smooth((elapsed - 5f / 12f) * 12f);
    }

    internal static float LevelMarkerScale(float elapsed) => 1f + LevelRingAlpha(elapsed);
    internal static float LevelDetailsAlpha(float elapsed) => 1f - LevelRingAlpha(elapsed);

    // The extracted streamed keys all have zero endpoint tangents. This is
    // their cubic polynomial after normalizing each original key interval.
    private static float Smooth(float value)
    {
        float t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
