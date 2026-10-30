using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Re-skin the native bar in place. Native poison/doom previews cast the fill
// nodes to NinePatchRect; its tweens also retain those nodes and their offsets.
internal static class HbrHealthBarSkin
{
    private sealed class Skin
    {
        internal NinePatchRect Background;
        internal Control ForegroundContainer;
        internal NinePatchRect Fill;
        internal MegaLabel Label;
        internal NinePatchRect Frame;
        internal NinePatchRect BlockOutline;
        internal ShaderMaterial FillMaterial;
        internal TextureRect PlayerBackdrop;
        internal TextureRect EnemyBackdrop;
        internal NHbrDestructionRateCaption DestructionRate;
        internal NHbrEnemyStatusValueCaption EnemyStatusValue;
        internal float Thickness = -1f;
    }

    private static readonly ConditionalWeakTable<NHealthBar, Skin> Skins = new();

    internal static void Attach(NHealthBar bar)
    {
        if (Skins.TryGetValue(bar, out _)) return;
        Control container = bar.HpBarContainer;
        var background = container.GetNode<NinePatchRect>("HpBackground");
        var foregroundContainer = bar.GetNode<Control>("%HpForegroundContainer");
        var mask = bar.GetNode<NinePatchRect>("%Mask");
        var foreground = bar.GetNode<NinePatchRect>("%HpForeground");
        var middle = bar.GetNode<NinePatchRect>("%HpMiddleground");
        var poison = bar.GetNode<NinePatchRect>("%PoisonForeground");
        var doom = bar.GetNode<NinePatchRect>("%DoomForeground");
        var blockOutline = bar.GetNode<NinePatchRect>("%BlockOutline");
        var label = bar.GetNode<MegaLabel>("%HpLabel");
        // The compact DP gauge sits over the HP edge at Z=1. Keep the native
        // readout, infinity and block subtree (including attached effects)
        // above it without reparenting anything held by native tweens.
        label.ZIndex = 2;
        bar.GetNode<Control>("%InfinityTex").ZIndex = 2;
        bar.GetNode<Control>("%BlockContainer").ZIndex = 2;

        // Keep only UIBattleMember's upper-right readout backing. The lower
        // stepped region would darken HP/DP gaps instead of exposing the scene.
        var playerBackdrop = new TextureRect
        {
            Name = "HbrPlayerGaugeBaseFront",
            Texture = HbrBattleGaugeStyle.FrontReadoutBase,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = -4.4f / 225f,
            AnchorRight = 231.6f / 225f,
            OffsetLeft = 0f,
            OffsetRight = 0f,
            OffsetTop = -31f,
            OffsetBottom = NDeflectorProtectionDpBar.PlayerVerticalOffset,
            Visible = false
        };
        container.AddChild(playerBackdrop);
        container.MoveChild(playerBackdrop, 0);
        var enemyBackdrop = new TextureRect
        {
            Name = "HbrEnemyGaugeBase",
            Texture = HbrBattleGaugeStyle.EnemyBase,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = -2.1f / 230f,
            AnchorRight = 233.9f / 230f,
            OffsetLeft = 0f,
            OffsetRight = 0f,
            OffsetTop = -8.4f,
            OffsetBottom = 19.6f,
            Visible = false
        };
        container.AddChild(enemyBackdrop);
        container.MoveChild(enemyBackdrop, 0);

        HbrBattleGaugeStyle.StyleBackground(background);
        background.Modulate = HbrBattleGaugeStyle.BackgroundColor;
        HbrBattleGaugeStyle.FillRect(background);
        // Retain the native W-10 logical width for poison/doom and damage
        // tweens; a parent transform expands those coordinates to the frame.
        // Godot cannot nest alpha clipping nodes: the shaped mask below is
        // the sole clip owner for all native fill and damage-preview layers.
        foregroundContainer.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;
        foregroundContainer.OffsetTop = 0f;
        foregroundContainer.OffsetBottom = 0f;
        SyncForegroundLayout(container, foregroundContainer);
        HbrBattleGaugeStyle.StyleMask(mask, 10f);
        HbrBattleGaugeStyle.SetTexture(foreground, HbrBattleGaugeStyle.Hp);
        ShaderMaterial fillMaterial = HbrBattleGaugeStyle.CreateFillMaterial();
        foreground.Material = fillMaterial;
        HbrBattleGaugeStyle.SetTexture(middle, HbrBattleGaugeStyle.Damage);
        middle.Modulate = Colors.White;
        // The HBR damage-tail sprite is red; leave poison's neutral STS texture
        // in place so its native green tint and lethal preview remain readable.
        HbrBattleGaugeStyle.SetTexture(doom, HbrBattleGaugeStyle.Damage);
        foreach (NinePatchRect fill in new[] { foreground, middle, poison, doom })
        {
            fill.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;
            fill.OffsetTop = 0;
            fill.OffsetBottom = 0;
        }
        HbrBattleGaugeStyle.StyleFrame(blockOutline);
        HbrBattleGaugeStyle.FillRect(blockOutline);
        NinePatchRect frame = HbrBattleGaugeStyle.AddFrame(container, "HbrHpFrame");
        HbrBattleGaugeStyle.StyleNumber(label, fontColor: HbrBattleGaugeStyle.HpNumberColor);
        HbrBattleGaugeStyle.FillRect(label, 0, -5, 0, 5);
        var destructionRate = new NHbrDestructionRateCaption
        {
            Name = "HbrDestructionRate",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        container.AddChild(destructionRate);
        var enemyStatusValue = new NHbrEnemyStatusValueCaption
        {
            Name = "HbrEnemyStatusValue",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2,
            Visible = false
        };
        container.AddChild(enemyStatusValue);
        Skins.Add(bar, new Skin
        {
            Background = background, ForegroundContainer = foregroundContainer,
            Fill = foreground, Label = label,
            Frame = frame, BlockOutline = blockOutline, FillMaterial = fillMaterial,
            PlayerBackdrop = playerBackdrop, EnemyBackdrop = enemyBackdrop,
            DestructionRate = destructionRate, EnemyStatusValue = enemyStatusValue
        });
        bar.AddChildSafely(new NHbrBattleGaugeFrameOverlap
        {
            Name = "HbrGaugeFrameOverlap",
            HealthBar = bar
        });
    }

    internal static void Refresh(NHealthBar bar, Creature creature)
    {
        if (!Skins.TryGetValue(bar, out Skin skin)) return;
        SyncForegroundLayout(bar.HpBarContainer, skin.ForegroundContainer);
        skin.EnemyStatusValue.SetCreature(creature);
        if (creature == null) return;
        float thickness = HbrBattleGaugeSizing.GetThickness(creature);
        if (!Mathf.IsEqualApprox(skin.Thickness, thickness))
        {
            skin.Thickness = thickness;
            skin.PlayerBackdrop.OffsetTop = -31f * thickness;
            skin.PlayerBackdrop.OffsetBottom = NDeflectorProtectionDpBar.PlayerVerticalOffset * thickness;
            skin.EnemyBackdrop.OffsetTop = -8.4f * thickness;
            skin.EnemyBackdrop.OffsetBottom = 19.6f * thickness;
        }
        skin.PlayerBackdrop.Visible = creature.IsPlayer;
        // Monster backings cover the gauge interiors, including their gaps.
        // Borders, live fills and reserve layers supply their own visuals.
        skin.EnemyBackdrop.Visible = false;
        skin.DestructionRate.SetCreature(creature);
        HbrMultiHealthBar.Refresh(bar.HpBarContainer, creature);
        // Reserve layers are inserted at index zero on their first refresh.
        // Backing art belongs behind those layers as well as the live HP bar.
        if (skin.PlayerBackdrop.GetIndex() != 0 || skin.EnemyBackdrop.GetIndex() != 1)
        {
            bar.HpBarContainer.MoveChild(skin.EnemyBackdrop, 0);
            bar.HpBarContainer.MoveChild(skin.PlayerBackdrop, 0);
        }
        // HBR's gradient is already colored. Keep the block icon/outline and
        // native lethal-preview colors, rather than multiplying it by STS red.
        skin.Fill.SelfModulate = creature.HpDisplay.IsInfinite()
            ? new Color("C5BBED") : Colors.White;
        skin.Frame.Visible = !skin.BlockOutline.Visible;
        float fullWidth = bar.HpBarContainer.Size.X - 10f;
        skin.FillMaterial.SetShaderParameter("fill_span", fullWidth > 0f
            ? Mathf.Clamp((fullWidth + skin.Fill.OffsetRight - skin.Fill.OffsetLeft) / fullWidth, 0f, 1f)
            : 0f);
        skin.Background.Modulate = creature.IsEnemy
            ? HbrBattleGaugeStyle.BackgroundColor : new Color("464646");
        // Restore the DP-blue numbers after native block/poison/doom text
        // refreshes. The native fill previews still retain their warning colors.
        skin.Label.AddThemeColorOverride(ThemeConstants.Label.FontColor, HbrBattleGaugeStyle.HpNumberColor);
        skin.Label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, HbrBattleGaugeStyle.HpNumberColor);
        HbrBattleGaugeSizing.SyncRows(bar, creature);
    }

    private static void SyncForegroundLayout(Control container, Control foregroundContainer)
    {
        float logicalWidth = container.Size.X - 10f;
        if (logicalWidth <= 0f)
        {
            foregroundContainer.PivotOffset = Vector2.Zero;
            foregroundContainer.Scale = Vector2.One;
            return;
        }

        // With the original five-pixel offsets, scaling about the midpoint
        // maps the logical [0,W-10] span onto the full frame's [0,W] span.
        foregroundContainer.PivotOffset = new Vector2(logicalWidth * 0.5f, 0f);
        foregroundContainer.Scale = new Vector2(container.Size.X / logicalWidth, 1f);
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar._Ready))]
internal static class HbrHealthBarReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance) => HbrHealthBarSkin.Attach(__instance);
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
internal static class HbrHealthBarRefreshPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix(NHealthBar __instance, Creature ____creature) =>
        HbrHealthBarSkin.Refresh(__instance, ____creature);
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.SetCreature))]
internal static class HbrHealthBarSetCreaturePatch
{
    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance, Creature creature)
    {
        HbrBattleGaugeSizing.Refresh(__instance);
        HbrHealthBarSkin.Refresh(__instance, creature);
    }
}

[HarmonyPatch(typeof(NHealthBar), "SetHpBarContainerSizeWithOffsetsImmediately")]
internal static class HbrHealthBarResizePatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void Prefix(ref Vector2 size, Creature ____creature)
    {
        if (NPhantomCombatLayout.UsesHopperLayout(____creature))
            size.X = NPhantomCombatLayout.HopperHealthBarWidth;
        if (____creature?.Monster != null)
            size.Y = HbrBattleGaugeSizing.BaseHeight * HbrBattleGaugeSizing.Thickness;
    }

    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance, Creature ____creature) =>
        HbrHealthBarSkin.Refresh(__instance, ____creature);
}
