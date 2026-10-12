using System;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrBattleGaugeSizing
{
    internal const float BaseHeight = 16f;
    internal static float Thickness => HBRGOSakiConfig.HpDpThicknessPercent / 100f;
    internal static float GetThickness(Creature creature) =>
        creature?.Monster != null ? Thickness : 1f;

    private static readonly AccessTools.FieldRef<NHealthBar, Creature> BarCreature =
        AccessTools.FieldRefAccess<NHealthBar, Creature>("_creature");
    private static readonly Action<NHealthBar, Vector2> SetSize =
        PrivateAccess.MethodDelegate<NHealthBar, Action<NHealthBar, Vector2>>(
            "SetHpBarContainerSizeWithOffsetsImmediately", typeof(Vector2));

    internal static int GetAdjustedThickness(int current, int direction, float factor)
    {
        if (direction == 0) return Math.Clamp(current, 50, 400);
        if (!float.IsFinite(factor) || factor <= 0f) factor = 1f;
        int step = Math.Max(1, (int)MathF.Round(Math.Min(factor, 10f) * 10f));
        return (int)Math.Clamp((long)current + Math.Sign(direction) * step, 50L, 400L);
    }

    internal static void AdjustThickness(int direction, float factor)
    {
        int current = HBRGOSakiConfig.HpDpThicknessPercent;
        int next = GetAdjustedThickness(current, direction, factor);
        if (current == next) return;
        HBRGOSakiConfig.HpDpThicknessPercent = next;
        // Coalesce a scrolling burst; saving must not stall damage/UI callbacks.
        HBRGOSakiConfig.SaveGaugeSizing();
    }

    internal static void Refresh(NHealthBar bar)
    {
        if (!IsUsable(bar) || BarCreature(bar)?.Monster == null || bar.HpBarContainer == null) return;
        // Native bounds refreshes copy the current height. Always derive it
        // from the source's 16px gauge, so repeated callbacks cannot enlarge it.
        SetSize(bar, new Vector2(bar.HpBarContainer.Size.X, BaseHeight * Thickness));
        bar.RefreshValues();
    }

    internal static void SyncRows(NHealthBar bar, Creature creature)
    {
        if (creature?.Monster == null || bar.GetParent() is not NCreatureStateDisplay state) return;
        // Scene bosses own a compact 4px gap; ordinary phantom HUDs retain
        // Hopper layout. Player rows keep their existing scene/native owner.
        if (NStaticHbrgoMonsterVisuals.IsSceneBoss(creature))
        {
            CombatHudLayoutHelper.AlignPowerContainerToHealthBar(
                state, bar.HpBarContainer, 0f, NStaticHbrgoMonsterVisuals.StateDisplayContentGap);
            CombatHudLayoutHelper.AlignNameplateToHealthBar(
                state, bar.HpBarContainer, NStaticHbrgoMonsterVisuals.StateDisplayContentGap);
            return;
        }
        if (!NPhantomCombatLayout.UsesHopperLayout(creature))
        {
            // Keep native row width, wrapping and X positioning for ordinary
            // creatures; thickness changes only their vertical clearance.
            CombatHudLayoutHelper.AlignRowsVerticallyToHealthBar(state, bar.HpBarContainer);
            return;
        }
        float inset = (24f - (creature.Monster?.HpBarSizeReduction ?? 24f)) * 0.5f;
        CombatHudLayoutHelper.AlignPowerContainerToHealthBar(
            state, bar.HpBarContainer, inset, verticalGap: 2f);
        CombatHudLayoutHelper.AlignNameplateToHealthBar(
            state, bar.HpBarContainer, verticalGap: 8f);
    }

    internal static bool HitTest(NHealthBar bar, Vector2 pointer)
    {
        Creature creature = BarCreature(bar);
        if (!IsVisible(bar) || creature?.Monster == null || !creature.IsAlive) return false;
        Control hp = bar.HpBarContainer;
        if (HitGauge(hp, pointer)) return true;
        Control reserves = hp?.GetNodeOrNull<Control>("HbrHpReserveLayers");
        if (reserves != null)
        {
            for (int i = 1; i <= 3; i++)
                if (HitGauge(reserves.GetNodeOrNull<Control>($"ReserveHp{i}"), pointer)) return true;
        }
        return bar.GetNodeOrNull<NDeflectorProtectionDpBar>("DeflectorProtectionDpBar")
            ?.HitTestGauge(pointer) == true;
    }

    internal static bool HitGauge(Control gauge, Vector2 pointer) =>
        IsVisible(gauge) && ContainsPoint(gauge.GetGlobalTransformWithCanvas(), gauge.Size, pointer);

    internal static bool ContainsPoint(Transform2D transform, Vector2 size, Vector2 pointer)
    {
        if (size.X <= 0f || size.Y <= 0f || !float.IsFinite(pointer.X) ||
            !float.IsFinite(pointer.Y) || Mathf.IsZeroApprox(transform.Determinant())) return false;
        return new Rect2(Vector2.Zero, size).HasPoint(transform.AffineInverse() * pointer);
    }

    private static bool IsVisible(Control control)
    {
        if (!IsUsable(control) || !control.IsVisibleInTree()) return false;
        // Spawn/death/menu fades often leave Visible=true at alpha zero.
        for (Node node = control; node != null; node = node.GetParent())
            if (node is CanvasItem item && item.Modulate.A <= 0.01f) return false;
        return true;
    }

    private static bool IsUsable(Control control) =>
        control != null && GodotObject.IsInstanceValid(control) &&
        control.IsInsideTree() && !control.IsQueuedForDeletion();
}
