using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

/// <summary>
/// Moves the local player's actual creature once per layout change. The
/// battlefield transform remains available to independently placed summons.
/// </summary>
public partial class NSakiCornerCreatureLayout : Node
{
    internal const string WidgetName = "SakiCornerCreatureLayout";
    private static readonly ConditionalWeakTable<NCombatRoom, NSakiCornerCreatureLayout> Layouts = new();

    private NCombatRoom _room;
    private NCreature _creature;
    private NHBRGOSakiCreatureVisuals _visuals;
    private Node _nativeParent;
    private Transform2D _nativeTransform;
    private Vector2 _nativeHitboxSize;
    private Vector2 _cornerOffset;
    private Vector2 _battlefieldOffset;
    private Vector2 _roomSize;
    private bool _hasNativeLayout;
    private bool _cornerApplied;
    private bool _dirty = true;

    public override void _EnterTree()
    {
        _room = GetParent() as NCombatRoom;
        if (_room == null) return;
        Layouts.Remove(_room);
        Layouts.Add(_room, this);
    }

    public override void _Ready() => RefreshLayout();

    public override void _Process(double delta)
    {
        // Position belongs to native animations between these invalidations.
        // In particular, do not undo attacks, Sandpit movement or hurt shakes.
        if (Usable(_room) && !_roomSize.IsEqualApprox(_room.Size)) _dirty = true;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RestoreNativeLayout();
        if (_room != null) Layouts.Remove(_room);
        _creature = null;
        _visuals = null;
        _room = null;
    }

    internal static bool IsCornerCreature(NCreature creature) =>
        Find(creature) is { _cornerApplied: true } layout && ReferenceEquals(layout._creature, creature);

    internal static void NotifyPortraitChanged(NCreature creature)
    {
        NSakiCornerCreatureLayout layout = Find(creature);
        if (layout != null && ReferenceEquals(layout._creature, creature)) layout._dirty = true;
    }

    internal static void PrepareForNativePlacement(IEnumerable<NCreature> creatures)
    {
        foreach (NSakiCornerCreatureLayout layout in FindLayouts(creatures))
        {
            layout.RestoreNativeLayout();
            layout._dirty = true;
        }
    }

    internal static void FinishNativePlacement(IEnumerable<NCreature> creatures)
    {
        foreach (NSakiCornerCreatureLayout layout in FindLayouts(creatures))
        {
            // A postfix records only native placement. Applying the corner
            // here would contaminate PhantomHudAnchorPatch's native snapshot.
            if (layout._cornerApplied) layout.RestoreNativeLayout();
            layout.BindCreature();
            layout.CaptureNativeLayout();
            layout._dirty = true;
        }
    }

    internal static void RefreshAfterRoomReady(NCombatRoom room)
    {
        if (room == null || !Layouts.TryGetValue(room, out NSakiCornerCreatureLayout layout)) return;
        layout._dirty = true;
        // Camera scale and native aspect-ratio adjustments finish before this.
        Callable.From(layout.RefreshLayout).CallDeferred();
    }

    internal static bool TryGetBattlefieldTransform(NCreature creature, out Transform2D transform)
    {
        transform = default;
        NSakiCornerCreatureLayout layout = Find(creature);
        if (layout is not { _cornerApplied: true } || !ReferenceEquals(layout._creature, creature) ||
            creature.GetParent() is not CanvasItem parent) return false;
        Transform2D local = creature.GetTransform();
        local.Origin += layout._battlefieldOffset - layout._cornerOffset;
        transform = parent.GetGlobalTransform() * local;
        return true;
    }

    internal static bool TryGetBattlefieldVisualRect(NCreature creature, out Rect2 rect)
    {
        rect = default;
        NSakiCornerCreatureLayout layout = Find(creature);
        if (!TryGetBattlefieldTransform(creature, out Transform2D transform) ||
            layout?._visuals == null || !layout._visuals.TryGetNativePortraitRect(out Rect2 local)) return false;
        rect = transform * local;
        return true;
    }

    internal static bool TryGetBattlefieldHitboxSize(NCreature creature, out Vector2 size)
    {
        size = default;
        NSakiCornerCreatureLayout layout = Find(creature);
        if (layout is not { _cornerApplied: true, _hasNativeLayout: true } ||
            !ReferenceEquals(layout._creature, creature) || layout._nativeHitboxSize.X <= 0f ||
            layout._nativeHitboxSize.Y <= 0f) return false;
        size = layout._nativeHitboxSize;
        return true;
    }

    internal static void ApplyBattlefieldOffset(NCreature creature, Vector2 globalOffset)
    {
        NSakiCornerCreatureLayout layout = Find(creature);
        if (layout is not { _cornerApplied: true } || !ReferenceEquals(layout._creature, creature) ||
            creature.GetParent() is not CanvasItem parent || globalOffset.IsZeroApprox()) return;
        Transform2D inverse = parent.GetGlobalTransform().AffineInverse();
        // Boss floor changes belong to the virtual battlefield anchor only.
        // The visible corner root stays put, while the next boss delta is zero.
        layout._battlefieldOffset += inverse * globalOffset - inverse * Vector2.Zero;
    }

    private void RefreshLayout()
    {
        if (!Usable(_room) || !_room.IsNodeReady()) return;
        BindCreature();
        if (!Usable(_creature) || !Usable(_visuals)) return;
        bool enabled = HBRGOSakiConfig.ModUI && _room.Mode == CombatRoomMode.ActiveCombat;
        if (!enabled)
        {
            if (_cornerApplied && !IsBusy()) RestoreNativeLayout();
            return;
        }
        if (!_cornerApplied) _dirty = true;
        if (!_dirty || IsBusy() || _room.Size.X <= 0f || _room.Size.Y <= 0f) return;
        ApplyCornerLayout();
    }

    private void BindCreature()
    {
        if (Usable(_creature) && ReferenceEquals(_creature.GetParent(), _nativeParent)) return;
        RestoreNativeLayout();
        _creature = null;
        _visuals = null;
        _hasNativeLayout = false;
        foreach (NCreature creature in _room.CreatureNodes)
        {
            if (!Usable(creature) || creature.Entity?.IsPlayer != true ||
                !LocalContext.IsMe(creature.Entity) || creature.Entity.Player?.Character is not HbrgoSaki ||
                creature.Visuals is not NHBRGOSakiCreatureVisuals visuals) continue;
            _creature = creature;
            _visuals = visuals;
            CaptureNativeLayout();
            _dirty = true;
            break;
        }
    }

    private void CaptureNativeLayout()
    {
        if (!Usable(_creature) || _cornerApplied) return;
        _nativeParent = _creature.GetParent();
        _nativeTransform = _creature.GetTransform();
        _nativeHitboxSize = _creature.Hitbox?.Size ?? Vector2.Zero;
        _cornerOffset = Vector2.Zero;
        _battlefieldOffset = Vector2.Zero;
        _hasNativeLayout = true;
    }

    private void ApplyCornerLayout()
    {
        if (_creature.GetParent() is not CanvasItem parent) return;
        if (!_hasNativeLayout) CaptureNativeLayout();
        Transform2D roomCanvas = _room.GetGlobalTransformWithCanvas();
        Transform2D visualCanvas = _visuals.GetGlobalTransformWithCanvas();
        float unitHeight = visualCanvas.Y.Length();
        if (unitHeight < 0.001f) return;
        // These are fixed logical UI units, just like the energy/status HUD.
        // Taller 4:3 and 16:10 logical viewports must not enlarge this cluster.
        float height = 430f * roomCanvas.Y.Length() / unitHeight;
        _visuals.SetCornerPresentation(true, height);

        // Corner geometry puts the real body's visible bottom at its root.
        // Convert the desired UI location through the actual camera/canvas.
        float halfWidth = _visuals.Bounds.Size.X * visualCanvas.X.Length() * 0.5f;
        Vector2 desired = roomCanvas * new Vector2(30f,
            _room.Size.Y - 125f) + Vector2.Right * halfWidth;
        Vector2 local = parent.GetGlobalTransformWithCanvas().AffineInverse() * desired;
        Vector2 nextOffset = local - _nativeTransform.Origin;
        _creature.Position += nextOffset - _cornerOffset;
        _cornerOffset = nextOffset;
        _cornerApplied = true;
        _roomSize = _room.Size;
        _dirty = false;
        NSakiCornerStatusHud.RefreshNow(_room.Ui);
    }

    private void RestoreNativeLayout()
    {
        if (!_cornerApplied) return;
        if (Usable(_creature) && ReferenceEquals(_creature.GetParent(), _nativeParent))
            _creature.Position += _battlefieldOffset - _cornerOffset;
        if (Usable(_visuals)) _visuals.SetCornerPresentation(false);
        _cornerOffset = Vector2.Zero;
        _battlefieldOffset = Vector2.Zero;
        _cornerApplied = false;
        _hasNativeLayout = false;
        _dirty = true;
    }

    private bool IsBusy() => _creature?.Entity?.Player is { } player &&
        CombatManager.Instance?.IsExecutingCardOrPotionEffect(player) == true;

    private static IEnumerable<NSakiCornerCreatureLayout> FindLayouts(IEnumerable<NCreature> creatures)
    {
        HashSet<NSakiCornerCreatureLayout> found = new();
        foreach (NCreature creature in creatures)
        {
            NSakiCornerCreatureLayout layout = Find(creature);
            if (layout != null && found.Add(layout)) yield return layout;
        }
    }

    private static NSakiCornerCreatureLayout Find(NCreature creature)
    {
        if (!Usable(creature)) return null;
        for (Node node = creature.GetParent(); node != null; node = node.GetParent())
            if (node is NCombatRoom room)
                return Layouts.TryGetValue(room, out NSakiCornerCreatureLayout layout) ? layout : null;
        return null;
    }

    private static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value) &&
        (value is not Node node || !node.IsQueuedForDeletion());
}
