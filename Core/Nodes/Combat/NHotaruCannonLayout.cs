using System;
using System.Collections.Generic;
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
    private readonly Dictionary<NCreature, CannonNodes> _cannonNodes = new();
    private NCombatRoom _room;
    private double _floatingTime;

    private sealed class CannonNodes
    {
        private readonly NHotaruCannonLayout _layout;
        private readonly NCreature _cannon;
        private readonly List<NIntent> _intents = new();
        private Control _healthBar;
        private Control _intentContainer;
        private NHotaruCannonMultiplierStatus _status;
        private bool _intentsDirty = true;

        internal CannonNodes(NHotaruCannonLayout layout, NCreature cannon)
        {
            _layout = layout;
            _cannon = cannon;
            cannon.TreeExiting += OnTreeExiting;
        }

        private void OnTreeExiting()
        {
            _layout._cannonNodes.Remove(_cannon);
            Dispose();
        }

        internal void Dispose()
        {
            if (GodotObject.IsInstanceValid(_cannon)) _cannon.TreeExiting -= OnTreeExiting;
            if (GodotObject.IsInstanceValid(_intentContainer))
                _intentContainer.ChildOrderChanged -= InvalidateIntents;
            _intents.Clear();
        }

        private void InvalidateIntents() => _intentsDirty = true;

        internal void Refresh()
        {
            // These are fixed scene children. Re-resolve only after a cached
            // node leaves the tree, including the native node's initial setup.
            if (!Usable(_healthBar)) _healthBar = _cannon.GetNodeOrNull<Control>("HealthBar/HealthBar");
            if (Usable(_healthBar)) _healthBar.Hide();
            StackIntents();
            _status = NHotaruCannonMultiplierStatus.Refresh(_cannon, _status);
        }

        private void StackIntents()
        {
            Control container = _cannon.IntentContainer;
            if (!ReferenceEquals(container, _intentContainer))
            {
                if (GodotObject.IsInstanceValid(_intentContainer))
                    _intentContainer.ChildOrderChanged -= InvalidateIntents;
                _intentContainer = container;
                if (GodotObject.IsInstanceValid(container)) container.ChildOrderChanged += InvalidateIntents;
                _intentsDirty = true;
            }
            if (!Usable(container)) return;
            if (_intentsDirty)
            {
                _intentsDirty = false;
                _intents.Clear();
                for (int index = 0; index < container.GetChildCount(); index++)
                    if (container.GetChild(index) is NIntent intent) _intents.Add(intent);
            }

            int bottomIndex = _intents.Count - 1;
            while (bottomIndex >= 0 && !Usable(_intents[bottomIndex])) bottomIndex--;
            if (bottomIndex < 1) return;
            NIntent bottom = _intents[bottomIndex];
            // Native HBox layout can overwrite positions without changing its
            // children, so keep correcting geometry before every drawn frame.
            Vector2 position = bottom.Position;
            float lowerHeight = Mathf.Max(64f, bottom.Size.Y);
            for (int index = bottomIndex - 1; index >= 0; index--)
            {
                NIntent intent = _intents[index];
                if (!Usable(intent)) continue;
                float height = Mathf.Max(64f, intent.Size.Y);
                position.Y -= Mathf.Max(lowerHeight, height) + 16f;
                intent.Position = position;
                lowerHeight = height;
            }
        }
    }

    public override void _Ready()
    {
        _room = GetParent() as NCombatRoom;
        RenderingServer.FramePreDraw += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        foreach (CannonNodes nodes in _cannonNodes.Values) nodes.Dispose();
        _cannonNodes.Clear();
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
            if (!_cannonNodes.TryGetValue(cannon, out CannonNodes nodes))
            {
                nodes = new CannonNodes(this, cannon);
                _cannonNodes.Add(cannon, nodes);
            }
            nodes.Refresh();
        }
    }

    internal static void HideHealth(NCreature cannon)
    {
        // Preserve the nameplate, intent, hitbox and target reticle. Only the
        // engine sentinel's health/block display is hidden.
        cannon.GetNodeOrNull<Control>("HealthBar/HealthBar")?.Hide();
    }

    private static bool Usable(Node instance) => instance != null && GodotObject.IsInstanceValid(instance) &&
        instance.IsInsideTree() && !instance.IsQueuedForDeletion();
}
