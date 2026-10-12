using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatHudLayoutHelper
{
    private sealed class LayoutState
    {
        internal Vector2 Baseline;
        internal Vector2 Position;
        internal Vector2 Size;
        internal int PowerCount;
        internal float IconWidth;
    }

    private static readonly ConditionalWeakTable<NPowerContainer, LayoutState> Layouts = new();
    private static readonly AccessTools.FieldRef<NPowerContainer, Vector2?> PowerContainerOriginalPosition =
        AccessTools.FieldRefAccess<NPowerContainer, Vector2?>("_originalPosition");
    private static readonly AccessTools.FieldRef<NPowerContainer, List<NPower>> PowerNodes =
        AccessTools.FieldRefAccess<NPowerContainer, List<NPower>>("_powerNodes");
    private static readonly Action<NPowerContainer> UpdatePowerPositions =
        PrivateAccess.MethodDelegate<NPowerContainer, Action<NPowerContainer>>("UpdatePositions");

    internal static void AlignNameplateToHealthBar(
        NCreatureStateDisplay state, Control hpBar, float verticalGap)
    {
        if (!IsUsable(state) || !IsUsable(hpBar)) return;
        Control nameplate = state.GetNodeOrNull<Control>("%NameplateContainer");
        if (!IsUsable(nameplate) || nameplate.GetParent() is not CanvasItem parent) return;

        Transform2D barToParent = parent.GetGlobalTransform().AffineInverse() * hpBar.GetGlobalTransform();
        Rect2 row = GetNameplateRect(barToParent, hpBar.Size, nameplate.Size.Y, verticalGap);
        if (!nameplate.Size.IsEqualApprox(row.Size)) nameplate.Size = row.Size;
        if (!nameplate.Position.IsEqualApprox(row.Position)) nameplate.Position = row.Position;
        // Native hover tweens still own Modulate. Only the row's geometry is
        // tied to the visible bar, never to a resized monster's hitbox.
    }

    internal static void AlignRowsVerticallyToHealthBar(
        NCreatureStateDisplay state, Control hpBar)
    {
        if (!IsUsable(state) || !IsUsable(hpBar)) return;
        NPowerContainer powers = state.GetNodeOrNull<NPowerContainer>("%PowerContainer");
        if (IsUsable(powers) && powers.GetParent() is CanvasItem powerParent)
        {
            Vector2 bottom = powerParent.GetGlobalTransform().AffineInverse() *
                (hpBar.GetGlobalTransform() * new Vector2(0f, hpBar.Size.Y + 2f));
            Vector2 original = PowerContainerOriginalPosition(powers) ?? powers.Position;
            if (!Mathf.IsEqualApprox(original.Y, bottom.Y) ||
                !Mathf.IsEqualApprox(powers.Position.Y, bottom.Y))
            {
                original.Y = bottom.Y;
                PowerContainerOriginalPosition(powers) = original;
                powers.Position = new Vector2(powers.Position.X, bottom.Y);
                UpdatePowerPositions(powers);
            }
        }
        Control nameplate = state.GetNodeOrNull<Control>("%NameplateContainer");
        if (IsUsable(nameplate) && nameplate.GetParent() is CanvasItem nameParent)
        {
            Vector2 bottom = nameParent.GetGlobalTransform().AffineInverse() *
                (hpBar.GetGlobalTransform() * new Vector2(0f, hpBar.Size.Y + 8f));
            if (!Mathf.IsEqualApprox(nameplate.Position.Y, bottom.Y))
                nameplate.Position = new Vector2(nameplate.Position.X, bottom.Y);
        }
    }

    internal static Rect2 GetNameplateRect(
        Transform2D barToParent, Vector2 barSize, float nameplateHeight, float verticalGap)
    {
        Vector2 left = barToParent * new Vector2(0f, barSize.Y + verticalGap);
        Vector2 right = barToParent * new Vector2(barSize.X, barSize.Y + verticalGap);
        return new Rect2(new Vector2(Mathf.Min(left.X, right.X), (left.Y + right.Y) * 0.5f),
            new Vector2(Mathf.Abs(right.X - left.X), nameplateHeight));
    }

    internal static void AlignPowerContainerToHealthBar(
        NCreatureStateDisplay state, Control hpBar,
        float horizontalInset, float verticalGap)
    {
        if (!IsUsable(state) || !IsUsable(hpBar)) return;
        NPowerContainer powers = state.GetNodeOrNull<NPowerContainer>("%PowerContainer");
        if (!IsUsable(powers) || powers.GetParent() is not CanvasItem parent) return;

        Vector2 globalBaseline = hpBar.GetGlobalTransform() *
            new Vector2(horizontalInset, hpBar.Size.Y + verticalGap);
        Vector2 baseline = parent.GetGlobalTransform().AffineInverse() * globalBaseline;
        Vector2 size = powers.Size;
        float width = hpBar.Size.X;
        List<NPower> nodes = PowerNodes(powers);
        int count = nodes.Count;
        float iconWidth = count == 0 ? 0f : nodes[0].Size.X;
        if (Layouts.TryGetValue(powers, out LayoutState layout) &&
            layout.Baseline.IsEqualApprox(baseline) &&
            layout.Size == size && size.X == width &&
            layout.Position.IsEqualApprox(powers.Position) &&
            PowerContainerOriginalPosition(powers) == layout.Baseline &&
            layout.PowerCount == count && layout.IconWidth == iconWidth)
        {
            return;
        }

        powers.Size = new Vector2(width, size.Y);
        // Native additions/removals restore this baseline before centering an
        // overflowing row. Keep both axes in sync with the displayed HP bar.
        PowerContainerOriginalPosition(powers) = baseline;
        powers.Position = baseline;
        UpdatePowerPositions(powers);

        layout ??= Layouts.GetValue(powers, static _ => new LayoutState());
        layout.Baseline = baseline;
        // Overflow may move the container left. Cache the native result, not
        // the baseline, and notice external/native resets before skipping work.
        layout.Position = powers.Position;
        layout.Size = powers.Size;
        layout.PowerCount = count;
        layout.IconWidth = iconWidth;
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}
