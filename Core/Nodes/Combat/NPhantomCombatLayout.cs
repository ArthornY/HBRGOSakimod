using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NPhantomCombatLayout : Node
{
    // SmallHopper's 190px artwork ends at -12: its intent marker is -226.
    // Use the unupgraded Hopper's upper HP bound (16 / 2) for the summon
    // offset, so HP gains and replacing the monster cannot move the HUD.
    private const float HopperReferenceMaxHp = 8f;
    // small_hopper.tscn uses a 190px-tall 1027x844 cutout. The unupgraded
    // phantom's 0.8 body scale and native 24px padding give this HUD width.
    // Keep that reference independent of its current HP and body bounds.
    internal const float HopperHealthBarWidth = 190f * 1027f / 844f * 0.8f + 24f;
    internal static readonly Vector2 HopperIntentCenter = new(0f, -226f);
    internal static readonly Vector2 HopperHealthBarTopCenter = new(0f, 7f);

    private sealed class PlayerAnchor
    {
        internal Transform2D LocalTransform;
        internal Vector2 HopperOffset;
    }

    private static readonly ConditionalWeakTable<NCreature, PlayerAnchor> PlayerAnchors = new();

    private NCombatRoom _room;

    public override void _Ready()
    {
        _room = GetParent() as NCombatRoom;
        // Run after native layout, deferred sizing and show/hide tweens. Their
        // opacity changes remain intact; only the final HUD position is fixed.
        RenderingServer.FramePreDraw += RefreshLayout;
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        _room = null;
    }

    private void RefreshLayout()
    {
        if (!IsUsable(_room)) return;
        foreach (NCreature creature in _room.CreatureNodes)
            RefreshCreature(creature);
    }

    internal static bool UsesHopperLayout(Creature creature) =>
        PhantomHelper.IsOwnedMainPhantom(creature, creature?.PetOwner) &&
        !NStaticHbrgoMonsterVisuals.IsSceneBoss(creature);

    internal static Vector2 GetHopperAnchorOffset(float ownerWidth) =>
        Vector2.Right * (ownerWidth * 0.5f) +
        Osty.MinOffset.Lerp(Osty.MaxOffset, HopperReferenceMaxHp / 150f);

    internal static void RememberPlayerAnchors(IEnumerable<NCreature> creatures)
    {
        foreach (NCreature creature in creatures)
        {
            if (creature.Entity?.IsPlayer != true || creature.Hitbox == null) continue;
            // Native player/pet placement has finished, but scene bosses have
            // not moved the bodies to their custom floor yet. Capture every
            // player now, including owners who summon their first phantom later.
            PlayerAnchor anchor = PlayerAnchors.GetValue(creature, _ => new PlayerAnchor());
            anchor.LocalTransform = creature.GetTransform();
            anchor.HopperOffset = GetHopperAnchorOffset(creature.Hitbox.Size.X);
        }
    }

    internal static Transform2D GetPlayerAnchorTransform(NCreature player)
    {
        if (PlayerAnchors.TryGetValue(player, out PlayerAnchor anchor) &&
            player.GetParent() is CanvasItem parent)
        {
            // Store the origin in the ally container's local space. Its current
            // transform still supplies camera, viewport and aspect-ratio scaling.
            return parent.GetGlobalTransform() * anchor.LocalTransform;
        }
        return player.GetGlobalTransform();
    }

    internal static Vector2 GetPlayerHopperAnchor(NCreature player) =>
        PlayerAnchors.TryGetValue(player, out PlayerAnchor anchor)
            ? anchor.HopperOffset
            : GetHopperAnchorOffset(player.Hitbox.Size.X);

    internal static void ResetStateDisplayForNativeBounds(NCreature creature)
    {
        if (!IsUsable(creature) || !UsesHopperLayout(creature.Entity)) return;
        NCreatureStateDisplay state = creature.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        if (!IsUsable(state)) return;
        // Native layout rewrites child global X coordinates. Restore the scene's
        // root X first, otherwise repeated Bounds updates accumulate opposite
        // offsets in the root and its children despite looking correct on screen.
        state.Position = new Vector2(0f, state.Position.Y);
    }

    internal void RefreshCreature(NCreature creature)
    {
        if (!IsUsable(_room) || !IsUsable(creature) ||
            !UsesHopperLayout(creature.Entity)) return;

        PhantomHealthScaleHelper.Refresh(creature);

        Player owner = creature.Entity.PetOwner;
        NCreature ownerNode = _room.GetCreatureNode(owner.Creature);
        if (!IsUsable(ownerNode) || !IsUsable(ownerNode.Hitbox) ||
            ownerNode.Hitbox.Size.X <= 0f) return;

        NCreatureStateDisplay state = creature.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        if (!IsUsable(state)) return;
        NHealthBar healthBar = state.GetNodeOrNull<NHealthBar>("%HealthBar");
        if (!IsUsable(healthBar)) return;
        Control hpBar = healthBar.HpBarContainer;
        Control intents = creature.IntentContainer;
        if (!IsUsable(hpBar) || !IsUsable(intents)) return;

        Vector2 anchor = GetPlayerHopperAnchor(ownerNode);
        Transform2D ownerTransform = GetPlayerAnchorTransform(ownerNode);
        Vector2 desiredHp = ownerTransform * (anchor + HopperHealthBarTopCenter);
        Vector2 currentHp = hpBar.GetGlobalTransform() * new Vector2(hpBar.Size.X * 0.5f, 0f);
        // Moving the whole display also moves DP, powers, nameplate and block.
        // The bar retains its reference Hopper width independently of the
        // growing body; the hitbox and artwork keep their native geometry.
        state.GlobalPosition += desiredHp - currentHp;
        AlignPowerContainerToHealthBar(state, hpBar,
            (24f - creature.Entity.Monster.HpBarSizeReduction) * 0.5f);
        // Native bounds refreshes place the name against the moving/resized
        // body. Restore its rectangle against the final, fixed HP anchor too.
        CombatHudLayoutHelper.AlignNameplateToHealthBar(state, hpBar, verticalGap: 8f);

        Vector2 desiredIntent = ownerTransform * (anchor + HopperIntentCenter);
        Vector2 currentIntent = intents.GetGlobalTransform() * (intents.Size * 0.5f);
        intents.GlobalPosition += desiredIntent - currentIntent;
    }

    internal static void AlignPowerContainerToHealthBar(
        NCreatureStateDisplay state, Control hpBar, float horizontalInset)
    {
        // Match the native 18px row position below its 16px HP bar.
        CombatHudLayoutHelper.AlignPowerContainerToHealthBar(
            state, hpBar, horizontalInset, verticalGap: 2f);
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}
