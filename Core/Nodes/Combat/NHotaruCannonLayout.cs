using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NHotaruCannonLayout : Node
{
    // 31O normal artwork displays at 1700 * 0.17 = 289 pixels.
    // Keep the formation's fixed distances proportional to Hotaru's old 500.
    internal const float VisualScaleRatio = 289f / 500f;
    internal const float CannonVisualScaleRatio = 0.75f;
    internal const float FloatingAmplitude = 3f;
    internal const double FloatingPeriodSeconds = 3.4;
    private NCombatRoom _room;
    private double _floatingTime;

    public override void _Ready()
    {
        _room = GetParent() as NCombatRoom;
        RenderingServer.FramePreDraw += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        _room = null;
    }

    public override void _Process(double delta) =>
        _floatingTime = (_floatingTime + delta) % FloatingPeriodSeconds;

    internal static void Install(NCombatRoom room)
    {
        if (room?.GetNodeOrNull<NHotaruCannonLayout>("HotaruCannonLayout") == null)
            room?.AddChild(new NHotaruCannonLayout { Name = "HotaruCannonLayout" });
    }

    internal static Vector2 GetOffset(HotaruCannonKind kind, Vector2 ownerSize, float scale)
    {
        float height = Mathf.Max(250f * VisualScaleRatio, ownerSize.Y);
        float side = Mathf.Max(120f * VisualScaleRatio, ownerSize.X * 0.5f) + 72f * CannonVisualScaleRatio * scale;
        return kind switch
        {
            HotaruCannonKind.Happiness => new Vector2(-side, -height * 0.28f),
            HotaruCannonKind.Absolution => new Vector2(-side, -height * 0.76f),
            HotaruCannonKind.Longing => new Vector2(0f, -height - 32f * CannonVisualScaleRatio * scale),
            HotaruCannonKind.Coffin => new Vector2(side, -height * 0.76f),
            HotaruCannonKind.Contest => new Vector2(side, -height * 0.28f),
            _ => Vector2.Zero
        };
    }

    internal static Vector2 GetFloatingOffset(HotaruCannonKind kind, double time, float scale)
    {
        double phase = time % FloatingPeriodSeconds * Math.Tau / FloatingPeriodSeconds +
            (int)kind * Math.Tau / 5d;
        return new Vector2(0f, (float)Math.Sin(phase) * FloatingAmplitude * scale);
    }

    internal static Vector2 GetRecallOffset(Vector2 ownerSize, float scale)
    {
        Vector2 upper = GetOffset(HotaruCannonKind.Coffin, ownerSize, scale);
        Vector2 lower = GetOffset(HotaruCannonKind.Contest, ownerSize, scale);
        // The terminal cannon keeps its historical first-retirement slot.
        // A recalled occupant gets a sixth slot beyond the right-hand pair;
        // 190 pixels clears the cannon's maximum 165-pixel portrait width.
        return new Vector2(upper.X + 190f * scale, (upper.Y + lower.Y) * 0.5f);
    }

    internal void RefreshLayout()
    {
        if (!Usable(_room)) return;
        foreach (NCreature cannon in _room.CreatureNodes)
        {
            if (!Usable(cannon) || cannon.Entity?.Monster is not HotaruCannonModel model ||
                cannon.Entity.PetOwner is not { } owner) continue;
            NCreature player = _room.GetCreatureNode(owner.Creature);
            if (!Usable(player) || !Usable(player.Visuals) || !Usable(cannon.Visuals)) continue;
            float scale = owner.RunState.Players.Count > 1 ? 0.7f : 1f;
            // All owner-relative offsets inherit the room's camera/aspect-ratio
            // transform. Each multiplayer owner has a separate formation anchor.
            cannon.Scale = player.Scale * scale;
            // Move the entity as a whole so its intents and targeting hitbox
            // follow the same gentle motion as the cannon artwork.
            Millsage relic = owner.GetRelic<Millsage>();
            HotaruCannonKind slot = model.Kind == HotaruCannonKind.Endgame
                ? relic?.FirstRetiredCannonKind ?? HotaruCannonKind.Happiness : model.Kind;
            bool recalledOverlap = relic?.IsCannonRecalledThisCombat(model.Kind) == true &&
                model.Kind == relic.FirstRetiredCannonKind && relic.GetCannon(HotaruCannonKind.Endgame) != null;
            Vector2 offset = (recalledOverlap ? GetRecallOffset(player.Visuals.Bounds.Size, scale)
                : GetOffset(slot, player.Visuals.Bounds.Size, scale)) +
                GetFloatingOffset(slot, _floatingTime, scale);
            cannon.GlobalPosition = player.GetGlobalTransform() * offset;
            cannon.Visuals.Modulate = player.Visuals.Modulate;
            cannon.ZIndex = player.ZIndex + 1;
            HideHealth(cannon);
            StackIntents(cannon);
            NHotaruCannonMultiplierStatus.Refresh(cannon);
        }
    }

    private static void StackIntents(NCreature cannon)
    {
        Control container = cannon.IntentContainer;
        if (!Usable(container)) return;
        var children = container.GetChildren();
        NIntent bottom = null;
        int count = 0;
        foreach (Node child in children)
        {
            if (child is not NIntent intent || !Usable(intent) || intent.IsQueuedForDeletion()) continue;
            bottom = intent;
            count++;
        }
        if (count < 2) return;

        // Keep the last intent at its native X/Y offset. Run after the HBox
        // layout and leave its size intact so repeated refreshes cannot drift.
        // Move each whole intent, including its label, particles and hover area.
        Vector2 position = bottom.Position;
        float lowerHeight = Mathf.Max(64f, bottom.Size.Y);
        for (int index = children.Count - 1; index >= 0; index--)
        {
            if (children[index] is not NIntent intent || !Usable(intent) ||
                intent.IsQueuedForDeletion() || ReferenceEquals(intent, bottom)) continue;
            float height = Mathf.Max(64f, intent.Size.Y);
            position.Y -= Mathf.Max(lowerHeight, height) + 16f;
            intent.Position = position;
            lowerHeight = height;
        }
    }

    internal static void HideHealth(NCreature cannon)
    {
        // Preserve the nameplate, intent, hitbox and target reticle. Only the
        // engine sentinel's health/block display is hidden.
        cannon.GetNodeOrNull<Control>("HealthBar/HealthBar")?.Hide();
    }

    private static bool Usable(GodotObject instance) => instance != null && GodotObject.IsInstanceValid(instance);
}
