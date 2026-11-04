using System;

namespace HBRGOSakiMod.Core.Nodes.Combat;

// Sizing and motion for the selected-card portrait. Kept free of Godot types
// so the validation project links it directly.
internal static class SakiSelectionPortraitLayout
{
    internal const float ReferenceHeight = 1080f;
    internal const float BodyHeight = 700f;
    internal const float MaxWidthRatio = 0.3f;
    internal const float SlideDistance = 40f;
    internal const double CrossfadeDuration = 0.25;

    // The body keeps the same share of the UI height at every window size;
    // forms whose weapons widen the cutout are held to a share of the width.
    internal static float Scale(float uiWidth, float uiHeight, float regionWidth, float bodyReferenceHeight) =>
        Math.Min(BodyHeight * uiHeight / ReferenceHeight / bodyReferenceHeight,
            uiWidth * MaxWidthRatio / regionWidth);

    // Enters from the left with the fade (ease-out cubic) and leaves the same way.
    internal static float SlideOffset(float opacity, float uiHeight)
    {
        float remaining = 1f - Math.Clamp(opacity, 0f, 1f);
        return -SlideDistance * uiHeight / ReferenceHeight * remaining * remaining * remaining;
    }

    // Incoming form opacity after a form change; infinite elapsed time means none is pending.
    internal static float CrossfadeProgress(double elapsed) =>
        (float)Math.Clamp(elapsed / CrossfadeDuration, 0, 1);
}
