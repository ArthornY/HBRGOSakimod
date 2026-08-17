using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
internal static class NHealthBarSetCreaturePatch
{
    private static void Postfix(NHealthBar __instance, Creature creature)
    {
        ModNodeAttachmentRegistry.EnsureReadyAttachments(__instance);

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.DeflectorProtectionDpBarAttachmentId,
                out NDeflectorProtectionDpBar dpBar))
        {
            dpBar.SetCreature(creature, __instance.HpBarContainer);
        }

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.RedCrimsonActionBarAttachmentId,
                out NRedCrimsonActionBar actionBar))
        {
            actionBar.SetCreature(creature, __instance.HpBarContainer);
        }
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
internal static class NHealthBarRefreshValuesPatch
{
    private static void Postfix(NHealthBar __instance)
    {
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.DeflectorProtectionDpBarAttachmentId,
                out NDeflectorProtectionDpBar dpBar))
        {
            dpBar.Refresh();
        }

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.RedCrimsonActionBarAttachmentId,
                out NRedCrimsonActionBar actionBar))
        {
            actionBar.Refresh();
        }
    }
}

[HarmonyPatch(
    typeof(NHealthBar),
    nameof(NHealthBar.UpdateLayoutForCreatureBounds))]
internal static class NHealthBarRedCrimsonLayoutPatch
{
    private const float DefaultWidthPadding = 24f;

    private readonly struct BoundsState
    {
        internal BoundsState(Vector2 position, Vector2 size)
        {
            Position = position;
            Size = size;
            ShouldRestore = true;
        }

        internal Vector2 Position { get; }

        internal Vector2 Size { get; }

        internal bool ShouldRestore { get; }
    }

    private static void Prefix(
        Control bounds,
        Creature ____creature,
        out BoundsState __state)
    {
        __state = default;
        if (bounds == null || ____creature?.Monster is not RedCrimson)
        {
            return;
        }

        Vector2 originalPosition = bounds.Position;
        Vector2 originalSize = bounds.Size;
        float widthPadding = DefaultWidthPadding
            - ____creature.Monster.HpBarSizeReduction;
        float fixedBoundsWidth = Mathf.Max(
            1f,
            NRedCrimsonCreatureVisuals.FixedHealthBarWidth - widthPadding);
        float centerX = originalPosition.X + originalSize.X * 0.5f;

        __state = new BoundsState(originalPosition, originalSize);
        bounds.Size = new Vector2(fixedBoundsWidth, originalSize.Y);
        bounds.Position = new Vector2(
            centerX - fixedBoundsWidth * 0.5f,
            originalPosition.Y);
    }

    private static void Postfix(Control bounds, BoundsState __state)
    {
        if (bounds == null || !__state.ShouldRestore)
        {
            return;
        }

        bounds.Size = __state.Size;
        bounds.Position = __state.Position;
    }
}

[HarmonyPatch(
    typeof(NHealthBar),
    "SetHpBarContainerSizeWithOffsetsImmediately")]
internal static class NHealthBarRedCrimsonFixedWidthPatch
{
    private static void Prefix(
        ref Vector2 size,
        Creature ____creature)
    {
        if (____creature?.Monster is not RedCrimson)
        {
            return;
        }

        size.X = NRedCrimsonCreatureVisuals.FixedHealthBarWidth;
    }
}
