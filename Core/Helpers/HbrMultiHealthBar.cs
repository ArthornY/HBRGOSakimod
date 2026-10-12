using System;
using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// The three reviving bosses expose actual phase receipts, not a combined HP
// pool. Reserve gauges show remaining lives without inventing their future HP.
internal static class HbrMultiHealthBar
{
    private sealed class Display
    {
        internal Creature Creature;
        internal Control Root;
        internal int Remaining = -1;
        internal Color BackgroundColor;
        internal float Thickness;
        internal readonly Control[] Reserves = new Control[3];
        internal readonly NinePatchRect[] Backgrounds = new NinePatchRect[3];
    }

    private static readonly ConditionalWeakTable<Control, Display> Displays = new();

    internal static bool HasMultipleHpPhases(Creature creature) =>
        creature?.Monster is FlatHand or SkullFeather or DeathSlug;

    internal static int GetRemainingBars(Creature creature) => creature?.Monster switch
    {
        FlatHand flatHand => Math.Clamp(3 - flatHand.Respawns, 1, 3),
        SkullFeather skullFeather => Math.Clamp(3 - skullFeather.Phase, 1, 2),
        DeathSlug deathSlug => Math.Clamp(3 - deathSlug.Phase, 1, 2),
        _ => 1
    };

    // UIBattleEnemy2's 230x16 hpGauge1..4 offsets, relative to hpGauge1.
    // Unity's higher-index sibling is in front. The native gauge represents
    // the highest visible index, with all coordinates relative to that gauge.
    internal static Vector2 GetReserveOffset(int layer)
    {
        Vector2 offset = layer switch
        {
            1 => new Vector2(8f, 9.7f),
            2 => new Vector2(15f, 19.8f),
            3 => new Vector2(22f, 30.5f),
            _ => Vector2.Zero
        };
        return offset;
    }

    internal static float GetExtraHeight(Creature creature) =>
        GetReserveOffset(GetRemainingBars(creature) - 1).Y * HbrBattleGaugeSizing.GetThickness(creature);

    internal static float GetExtraHeight(Control hpBar) =>
        Displays.TryGetValue(hpBar, out Display display)
            ? GetExtraHeight(display.Creature)
            : 0f;

    internal static Vector2 GetDisplayedReserveOffset(int reserveIndex, int remaining) =>
        GetReserveOffset(reserveIndex) - GetReserveOffset(remaining - 1);

    internal static void Refresh(Control hpBar, Creature creature)
    {
        int remaining = GetRemainingBars(creature);
        if (!Displays.TryGetValue(hpBar, out Display display))
        {
            if (remaining <= 1) return;
            display = Build(hpBar);
            Displays.Add(hpBar, display);
        }

        Color backgroundColor = creature.IsEnemy
            ? HbrBattleGaugeStyle.BackgroundColor : new Color("464646");
        float thickness = HbrBattleGaugeSizing.GetThickness(creature);
        if (ReferenceEquals(display.Creature, creature) &&
            display.Remaining == remaining && display.BackgroundColor == backgroundColor &&
            Mathf.IsEqualApprox(display.Thickness, thickness)) return;

        display.Creature = creature;
        display.Remaining = remaining;
        display.BackgroundColor = backgroundColor;
        display.Thickness = thickness;
        display.Root.Visible = remaining > 1;
        for (int i = 0; i < display.Reserves.Length; i++)
        {
            display.Reserves[i].Visible = i < remaining - 1;
            Vector2 offset = GetDisplayedReserveOffset(i, remaining);
            offset.Y *= thickness;
            HbrBattleGaugeStyle.FillRect(display.Reserves[i], offset.X, offset.Y, offset.X, offset.Y);
            display.Backgrounds[i].Modulate = backgroundColor;
        }
    }

    private static Display Build(Control hpBar)
    {
        var display = new Display
        {
            Root = new Control { Name = "HbrHpReserveLayers", MouseFilter = Control.MouseFilterEnum.Ignore }
        };
        hpBar.AddChild(display.Root);
        hpBar.MoveChild(display.Root, 0);
        HbrBattleGaugeStyle.FillRect(display.Root);

        // Preserve source low-to-high draw order behind the native current
        // gauge. At three lives, the reserves are (-15,-19.8) and (-7,-10.1).
        for (int i = 0; i < display.Reserves.Length; i++)
        {
            var layer = new Control { Name = $"ReserveHp{i + 1}", MouseFilter = Control.MouseFilterEnum.Ignore };
            display.Root.AddChild(layer);
            HbrBattleGaugeStyle.FillRect(layer);
            display.Reserves[i] = layer;

            var background = new NinePatchRect { Name = "Background" };
            HbrBattleGaugeStyle.StyleBackground(background);
            layer.AddChild(background);
            HbrBattleGaugeStyle.FillRect(background);
            display.Backgrounds[i] = background;

            var mask = new NinePatchRect { Name = "Mask", ClipChildren = CanvasItem.ClipChildrenMode.Only };
            HbrBattleGaugeStyle.StyleMask(mask);
            layer.AddChild(mask);
            HbrBattleGaugeStyle.FillRect(mask);
            var fill = new NinePatchRect { Name = "Hp", SelfModulate = Colors.White };
            HbrBattleGaugeStyle.SetTexture(fill, HbrBattleGaugeStyle.Hp);
            mask.AddChild(fill);
            HbrBattleGaugeStyle.FillRect(fill);
            HbrBattleGaugeStyle.AddFrame(layer, "Frame");
        }
        return display;
    }
}
