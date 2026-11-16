using System;
using HBRGOSakiMod.Core.Compatibility;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NPhantomMinionPlacement : Node
{
    private const float DragThreshold = 6f;

    private sealed class Placement
    {
        internal NCreature Node;
        internal NCreature OwnerNode;
        internal Player Owner;
        internal string ModelId;
        internal int Ordinal;
        internal int DefaultSlot;
        internal Vector2 RelativePosition;
        internal Control.GuiInputEventHandler GuiHandler;
        internal Control.CursorShape OriginalCursor;
    }

    private readonly Dictionary<Creature, Placement> _placements = new();
    private NCombatRoom _room;
    private Placement _drag;
    private bool _hasDragged;
    private Vector2 _pointerDown;
    private Vector2 _grabOffset;
    private Vector2 _dragOrigin;
    private int _dragZIndex;
    private Vector2 _viewportSize;

    public override void _Ready()
    {
        _room = GetParent() as NCombatRoom;
        RefreshPlacements();
    }

    internal static void NotifyCreatureAdded(NCombatRoom room, Creature creature)
    {
        if (creature?.PetOwner != null && IsUsable(room))
            room.GetNodeOrNull<NPhantomMinionPlacement>("PhantomMinionPlacement")?.RefreshPlacements();
    }

    internal static bool IsMinion(Creature creature) =>
        creature?.PetOwner != null && creature.IsAlive &&
        creature.GetPower<PhantomPower>() is { IsSummonedMinion: true };

    public override void _Process(double delta)
    {
        RefreshPlacements();
        if (_drag != null && (!CanDrag(_drag) || !Input.IsMouseButtonPressed(MouseButton.Left)))
            EndDrag(save: false);
    }

    private void RefreshPlacements()
    {
        if (!IsUsable(_room)) return;
        foreach (var pair in _placements.ToArray())
        {
            if (!IsUsable(pair.Value.Node) || !IsMinion(pair.Key) ||
                !_room.CreatureNodes.Contains(pair.Value.Node))
            {
                if (_drag == pair.Value) EndDrag(save: false);
                Unbind(pair.Value);
                _placements.Remove(pair.Key);
            }
        }

        foreach (NCreature node in _room.CreatureNodes.OrderBy(node => node.Entity.CombatId ?? uint.MaxValue))
        {
            if (!IsMinion(node.Entity) || _placements.ContainsKey(node.Entity) ||
                !IsUsable(node) || !IsUsable(node.Hitbox) || node.Hitbox.Size.X <= 0f) continue;
            NCreature ownerNode = _room.GetCreatureNode(node.Entity.PetOwner.Creature);
            if (!IsUsable(ownerNode)) continue;
            Register(node, ownerNode);
        }

        Vector2 size = _room.GetViewportRect().Size;
        if (size != _viewportSize)
        {
            // Parent transforms handle normal camera scaling. Re-clamp only on a
            // viewport change, never pin the root through an attack animation.
            _viewportSize = size;
            foreach (Placement placement in _placements.Values)
                if (placement != _drag) ApplyPosition(placement);
        }
    }

    private void Register(NCreature node, NCreature ownerNode)
    {
        Player owner = node.Entity.PetOwner;
        string modelId = node.Entity.Monster.Id.ToString();
        int[] occupied = _placements.Values
            .Where(entry => entry.Owner == owner && entry.ModelId == modelId)
            .Select(entry => entry.Ordinal).ToArray();
        int ordinal = LocalContext.IsMe(owner) && node.Entity.CombatId is uint combatId
            ? PhantomMinionPositionStore.ResolveOrdinal(GetBattleKey(node.Entity), modelId, combatId, occupied)
            : FindFreeOrdinal(occupied);
        int slot = FindFreeOrdinal(_placements.Values
            .Where(entry => entry.Owner == owner).Select(entry => entry.DefaultSlot));
        Vector2 position = DefaultPosition(slot);
        if (LocalContext.IsMe(owner) && PhantomMinionPositionStore.TryGet(modelId, ordinal, out Vector2 saved))
            position = saved;
        Placement placement = new()
        {
            Node = node, OwnerNode = ownerNode, Owner = owner, ModelId = modelId,
            Ordinal = ordinal, DefaultSlot = slot, RelativePosition = position,
            OriginalCursor = node.Hitbox.MouseDefaultCursorShape
        };
        placement.GuiHandler = input => OnMinionGuiInput(placement, input);
        _placements.Add(node.Entity, placement);
        node.Hitbox.GuiInput += placement.GuiHandler;
        if (LocalContext.IsMe(owner)) node.Hitbox.MouseDefaultCursorShape = Control.CursorShape.Move;
        ApplyPosition(placement);
    }

    internal static int FindFreeOrdinal(IEnumerable<int> occupied)
    {
        HashSet<int> used = new(occupied);
        int ordinal = 0;
        while (used.Contains(ordinal)) ordinal++;
        return ordinal;
    }

    internal static string GetBattleKey(Creature creature)
    {
        var combat = creature?.CombatState;
        var run = combat?.RunState;
        if (run?.Rng == null || creature.PetOwner == null) return null;
        // Native room IDs change on load. History identifies the encounter while
        // CombatId identifies its deterministic spawn order, without editing saves.
        return FormattableString.Invariant($"{run.Rng.StringSeed}|{run.CurrentActIndex}|{run.TotalFloor}|{run.CurrentMapPointHistoryEntry?.Rooms.Count ?? 0}|{combat.Encounter?.Id}|{creature.PetOwner.NetId}");
    }

    internal static Vector2 DefaultPosition(int slot) =>
        new(-150f + slot % 3 * 150f, -180f - slot / 3 * 180f);

    internal static Vector2 ToOwnerRelative(Transform2D ownerTransform, Vector2 globalPosition) =>
        ownerTransform.AffineInverse() * globalPosition;

    private static void ApplyPosition(Placement placement)
    {
        if (!IsUsable(placement.Node) || !IsUsable(placement.OwnerNode)) return;
        // A hand-edited config must not produce infinities under camera scaling.
        Vector2 relative = placement.RelativePosition.Clamp(-32768f, 32768f);
        placement.Node.GlobalPosition = placement.OwnerNode.GetGlobalTransform() * relative;
        ClampIntoViewport(placement.Node);
    }

    private void OnMinionGuiInput(Placement placement, InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } ||
            _drag != null || !CanDrag(placement) || placement.Node.GetParent() is not CanvasItem parent) return;

        _drag = placement;
        _hasDragged = false;
        _pointerDown = GetViewport().GetMousePosition();
        _grabOffset = parent.GetGlobalTransformWithCanvas().AffineInverse() * _pointerDown - placement.Node.Position;
        _dragOrigin = ToOwnerRelative(placement.OwnerNode.GetGlobalTransform(), placement.Node.GlobalPosition);
        _dragZIndex = placement.Node.ZIndex;
        placement.Node.Hitbox.AcceptEvent();
    }

    public override void _Input(InputEvent input)
    {
        if (_drag == null) return;
        if (!CanDrag(_drag))
        {
            EndDrag(save: false);
            return;
        }
        if (input is InputEventMouseMotion motion)
        {
            if (!_hasDragged && motion.Position.DistanceTo(_pointerDown) >= DragThreshold)
            {
                _hasDragged = true;
                _drag.Node.HideHoverTips();
                _drag.Node.ZIndex = CombatHudLayerHelper.GetDragLayer(_dragZIndex);
            }
            if (_hasDragged) MoveDrag(motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } released)
        {
            if (_hasDragged) MoveDrag(released.Position);
            EndDrag(save: _hasDragged);
            GetViewport().SetInputAsHandled();
        }
    }

    private bool CanDrag(Placement placement)
    {
        if (!IsUsable(_room) || !IsUsable(placement.Node) || !IsUsable(placement.OwnerNode) ||
            !placement.Node.IsVisibleInTree() || !placement.Node.IsInteractable ||
            !IsMinion(placement.Node.Entity) || !LocalContext.IsMe(placement.Owner) ||
            ActiveScreenContext.Instance?.IsCurrent(_room) != true) return false;

        CombatManager combat = CombatManager.Instance;
        return combat.IsInProgress && !combat.IsOverOrEnding && !combat.IsPaused &&
            !combat.PlayerActionsDisabled && !combat.IsExecutingCardOrPotionEffect(placement.Owner) &&
            placement.Node.Entity.CombatState?.CurrentSide == CombatSide.Player &&
            NRun.Instance?.GlobalUi?.TargetManager?.IsInSelection != true &&
            _room.Ui?.Hand is { InCardPlay: false, IsInCardSelection: false };
    }

    private void MoveDrag(Vector2 pointer)
    {
        if (_drag.Node.GetParent() is not CanvasItem parent) return;
        _drag.Node.Position = parent.GetGlobalTransformWithCanvas().AffineInverse() * pointer - _grabOffset;
        ClampIntoViewport(_drag.Node);
    }

    private void EndDrag(bool save)
    {
        Placement placement = _drag;
        if (placement == null) return;
        _drag = null;
        if (IsUsable(placement.Node) && IsUsable(placement.OwnerNode))
        {
            placement.Node.ZIndex = _dragZIndex;
            if (save && _hasDragged)
            {
                placement.RelativePosition = ToOwnerRelative(placement.OwnerNode.GetGlobalTransform(),
                    placement.Node.GlobalPosition);
                PhantomMinionPositionStore.Save(placement.ModelId, placement.Ordinal, placement.RelativePosition);
                // Rebuild controller/keyboard target navigation for the new visual order.
                _room.SetCreatureIsInteractableCompat(placement.Node.Entity, placement.Node.IsInteractable);
            }
            else if (_hasDragged)
            {
                placement.Node.GlobalPosition = placement.OwnerNode.GetGlobalTransform() * _dragOrigin;
            }
        }
        _hasDragged = false;
    }

    internal static Vector2 GetClampDelta(Rect2 bounds, Rect2 visible)
    {
        static float Axis(float start, float size, float minimum, float available) =>
            size > available ? minimum + (available - size) * 0.5f - start :
            Math.Clamp(start, minimum, minimum + available - size) - start;
        return new(Axis(bounds.Position.X, bounds.Size.X, visible.Position.X, visible.Size.X),
            Axis(bounds.Position.Y, bounds.Size.Y, visible.Position.Y, visible.Size.Y));
    }

    private static void ClampIntoViewport(NCreature node)
    {
        if (node.GetParent() is not CanvasItem parent || !IsUsable(node.Hitbox)) return;
        Rect2 viewport = node.GetViewportRect();
        if (viewport.Size.X <= 40f || viewport.Size.Y <= 40f) return;
        // Include room for intent and status rows as well as the clickable body.
        Rect2 footprint = (node.Hitbox.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, node.Hitbox.Size))
            .GrowIndividual(0f, 60f, 0f, 90f);
        Vector2 delta = GetClampDelta(footprint, viewport.Grow(-20f));
        if (delta.IsZeroApprox()) return;
        Vector2 viewportOrigin = node.GetGlobalTransformWithCanvas().Origin;
        node.Position = parent.GetGlobalTransformWithCanvas().AffineInverse() * (viewportOrigin + delta);
    }

    private static void Unbind(Placement placement)
    {
        if (!IsUsable(placement.Node) || !IsUsable(placement.Node.Hitbox)) return;
        placement.Node.Hitbox.GuiInput -= placement.GuiHandler;
        placement.Node.Hitbox.MouseDefaultCursorShape = placement.OriginalCursor;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationApplicationFocusOut)
            EndDrag(save: false);
    }

    public override void _ExitTree()
    {
        EndDrag(save: false);
        foreach (Placement placement in _placements.Values) Unbind(placement);
        _placements.Clear();
        _room = null;
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}
