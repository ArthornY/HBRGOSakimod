using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Patches;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NTopBarPowerLayout : Node
{
    internal const string WidgetName = "HBRGOTopBarPowerLayout";
    private const float GaugeGap = 16f;
    private const float GoldGap = 24f;
    private const float RowTop = 8f;
    private static readonly AccessTools.FieldRef<NPowerContainer, Vector2?> OriginalPosition =
        AccessTools.FieldRefAccess<NPowerContainer, Vector2?>("_originalPosition");
    private static readonly AccessTools.FieldRef<NPowerContainer, List<NPower>> PowerNodes =
        AccessTools.FieldRefAccess<NPowerContainer, List<NPower>>("_powerNodes");
    private static readonly Action<NPowerContainer> UpdateNativePositions =
        PrivateAccess.MethodDelegate<NPowerContainer, Action<NPowerContainer>>("UpdatePositions");
    private readonly List<NPower> _visiblePowers = new();
    private readonly List<NPower> _watchedPowers = new();
    private NGlobalUi _ui;
    private Player _player;
    private NCombatRoom _room;
    private NCreature _ownerNode;
    private NPowerContainer _powers;
    private Node _sourceParent;
    private NTopBarHealthHud _healthHud;
    private NRightTopBarInfoLayout _rightInfo;
    private bool _sourceDirty = true;
    private bool _layoutDirty = true;
    private bool _positionsDirty = true;
    private float _cellWidth = 48f;
    private float _rowHeight = 48f;
    private int _lastColumns = -1;
    private bool _moved;
    private bool _suspended;
    private bool _exiting;
    private bool _originalTopLevel;
    private bool _originalRelativeZ;
    private bool _originalVisible;
    private int _originalZ;
    private Vector2 _originalLocalPosition;
    private Vector2? _originalBaseline;
    private Vector2 _originalSize;
    private Vector2 _originalScale;
    private float _originalRotation;
    private Control.MouseFilterEnum _originalMouseFilter;

    internal static NTopBarPowerLayout Current =>
        NRun.Instance?.GlobalUi?.TopBar?.GetNodeOrNull<NTopBarPowerLayout>(WidgetName);

    internal static void Install(NGlobalUi ui, RunState runState)
    {
        if (!Usable(ui?.TopBar) || ui.TopBar.GetNodeOrNull<NTopBarPowerLayout>(WidgetName) != null) return;
        Player player = LocalContext.GetMe(runState);
        if (player?.Creature == null) return;
        ui.TopBar.AddChild(new NTopBarPowerLayout { Name = WidgetName, _ui = ui, _player = player });
    }

    public override void _Ready()
    {
        RenderingServer.FramePreDraw += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        _exiting = true;
        RestoreNativeLayout();
        BindOwnerNode(null);
        _room = null;
    }

    internal bool Owns(NPowerContainer container) => !_exiting && !_suspended && _moved &&
        HBRGOSakiConfig.ModUI && Usable(container) && ReferenceEquals(container, _powers);

    private void CaptureGeometry()
    {
        _originalTopLevel = _powers.TopLevel;
        _originalRelativeZ = _powers.ZAsRelative;
        _originalZ = _powers.ZIndex;
        _originalLocalPosition = _powers.Position;
        _originalBaseline = OriginalPosition(_powers);
        _originalSize = _powers.Size;
        _originalScale = _powers.Scale;
        _originalRotation = _powers.Rotation;
        _originalMouseFilter = _powers.MouseFilter;
        _originalVisible = _powers.Visible;
    }

    private void RestoreGeometry()
    {
        _moved = false;
        if (!Usable(_powers)) return;
        _powers.TopLevel = _originalTopLevel;
        _powers.ZAsRelative = _originalRelativeZ;
        _powers.ZIndex = _originalZ;
        _powers.Scale = _originalScale;
        _powers.Rotation = _originalRotation;
        _powers.Size = _originalSize;
        _powers.Position = _originalLocalPosition;
        OriginalPosition(_powers) = _originalBaseline;
        _powers.MouseFilter = _originalMouseFilter;
        _powers.Visible = _originalVisible;
    }

    private void RestoreNativeLayout()
    {
        if (Usable(_powers)) _powers.ChildOrderChanged -= InvalidateLayout;
        if (Usable(_sourceParent)) _sourceParent.ChildOrderChanged -= InvalidateSource;
        ClearPowerSubscriptions();
        if (Usable(_powers) && _moved)
        {
            TopBarPowerHoverRedirect.ClearForContainer(_powers);
            RestoreGeometry();
            UpdateNativePositions(_powers);
        }
        _powers = null;
        _sourceParent = null;
        _sourceDirty = true;
        _layoutDirty = _positionsDirty = true;
        _lastColumns = -1;
        _visiblePowers.Clear();
    }

    // Bounds changes still run the native body layout with its original
    // coordinates, then capture that fresh baseline before moving it again.
    internal void RestoreForNativeBounds()
    {
        _suspended = true;
        RestoreGeometry();
    }

    internal void ResumeAfterNativeBounds()
    {
        _suspended = false;
        InvalidateLayout();
        RefreshLayout();
    }

    internal void InvalidateLayout() => _layoutDirty = _positionsDirty = true;

    private void InvalidateSource() => _sourceDirty = true;

    internal void OnCreatureNodeRemoved(NCombatRoom room, NCreature node)
    {
        if (!ReferenceEquals(room, _room) || !ReferenceEquals(node, _ownerNode)) return;
        RestoreNativeLayout();
        BindOwnerNode(null);
    }

    private void BindOwnerNode(NCreature node)
    {
        if (ReferenceEquals(node, _ownerNode)) return;
        if (Usable(_ownerNode)) _ownerNode.ChildOrderChanged -= InvalidateSource;
        _ownerNode = node;
        if (Usable(_ownerNode)) _ownerNode.ChildOrderChanged += InvalidateSource;
    }

    private void RefreshSource()
    {
        NCombatRoom room = NCombatRoom.Instance;
        if (!ReferenceEquals(room, _room))
        {
            RestoreNativeLayout();
            BindOwnerNode(null);
            _room = room;
        }
        if (!_sourceDirty && Usable(_ownerNode) && _ownerNode.IsInsideTree() &&
            Usable(_powers) && _powers.IsInsideTree()) return;
        BindOwnerNode(Usable(_room) ? _room.GetCreatureNode(_player.Creature) : null);
        NPowerContainer source = Usable(_ownerNode)
            ? _ownerNode.GetNodeOrNull<NPowerContainer>("HealthBar/PowerContainer") : null;
        if (!ReferenceEquals(source, _powers))
        {
            RestoreNativeLayout();
            _powers = source;
            if (Usable(_powers))
            {
                _powers.ChildOrderChanged += InvalidateLayout;
                _sourceParent = _powers.GetParent();
                if (Usable(_sourceParent)) _sourceParent.ChildOrderChanged += InvalidateSource;
            }
        }
        // The native room/health nodes can finish entering the tree later.
        _sourceDirty = !Usable(_ownerNode) || !Usable(_powers) ||
            !_ownerNode.IsInsideTree() || !_powers.IsInsideTree();
    }

    private void ClearPowerSubscriptions()
    {
        foreach (NPower power in _watchedPowers)
        {
            if (!Usable(power)) continue;
            power.VisibilityChanged -= InvalidateLayout;
            power.Resized -= InvalidateLayout;
        }
        _watchedPowers.Clear();
    }

    private void RefreshPowerMetrics()
    {
        if (!_layoutDirty) return;
        _layoutDirty = false;
        _positionsDirty = true;
        ClearPowerSubscriptions();
        _visiblePowers.Clear();
        _cellWidth = _rowHeight = 48f;
        foreach (NPower power in PowerNodes(_powers))
        {
            if (!Usable(power)) continue;
            _watchedPowers.Add(power);
            power.VisibilityChanged += InvalidateLayout;
            power.Resized += InvalidateLayout;
            if (!power.Visible) continue;
            _visiblePowers.Add(power);
            _cellWidth = Math.Max(_cellWidth, power.Size.X);
            _rowHeight = Math.Max(_rowHeight, power.Size.Y + 4f);
        }
    }

    internal void RefreshLayout()
    {
        if (_exiting || _suspended || !Usable(_ui?.TopBar)) return;
        if (!HBRGOSakiConfig.ModUI) { RestoreNativeLayout(); return; }
        RefreshSource();
        if (!Usable(_powers)) return;
        if (!Usable(_healthHud) || !ReferenceEquals(_healthHud.GetParent(), _ui.TopBar))
            _healthHud = _ui.TopBar.GetNodeOrNull<NTopBarHealthHud>(NTopBarHealthHud.WidgetName);
        Control gauge = Usable(_healthHud) ? _healthHud.GaugeControl : null;
        if (!Usable(gauge)) { RestoreNativeLayout(); return; }
        if (!_moved) CaptureGeometry();
        RefreshPowerMetrics();

        Transform2D topBarTransform = _ui.TopBar.GetGlobalTransform();
        Transform2D inverse = topBarTransform.AffineInverse();
        Rect2 gaugeBounds = (inverse * gauge.GetGlobalTransform()) * new Rect2(Vector2.Zero, gauge.Size);
        float left = gaugeBounds.End.X + GaugeGap;
        if (!Usable(_rightInfo) || !ReferenceEquals(_rightInfo.GetParent(), _ui.TopBar))
            _rightInfo = _ui.TopBar.GetNodeOrNull<NRightTopBarInfoLayout>(NRightTopBarInfoLayout.WidgetName);
        float goldLeft = (Usable(_rightInfo) ? _rightInfo.GetGoldLeftEdge(_ui.TopBar) : null) ??
            (inverse * _ui.TopBar.Gold.GetGlobalTransform().Origin).X;
        float availableWidth = Math.Max(0f, goldLeft - GoldGap - left);

        // Keep the same live nodes and model subscriptions. TopLevel only
        // detaches their transform; tree ownership and combat lifetime stay.
        Vector2 parentScale = (_powers.GetParent() as CanvasItem)?.GetGlobalTransform().Scale.Abs() ?? Vector2.One;
        Vector2 scale = parentScale * _originalScale.Abs();
        _moved = true;
        _powers.TopLevel = true;
        _powers.ZAsRelative = false;
        _powers.ZIndex = _ui.TopBar.ZIndex + 1;
        _powers.MouseFilter = Control.MouseFilterEnum.Ignore;
        _powers.Rotation = topBarTransform.Rotation;
        _powers.GlobalPosition = topBarTransform * new Vector2(left, RowTop);

        float availableGlobalWidth = availableWidth * Math.Abs(topBarTransform.Scale.X);
        float renderedCell = _cellWidth * Math.Max(0.001f, scale.X);
        // A very narrow viewport may leave less than one badge's width. Fit
        // that one column inside the interval rather than covering the gold.
        if (availableGlobalWidth > 0f && availableGlobalWidth < renderedCell)
            scale *= availableGlobalWidth / renderedCell;
        _powers.Scale = scale;
        _powers.Visible = _originalVisible && availableGlobalWidth > 0f && _ui.TopBar.IsVisibleInTree() &&
            _ui.TopBar.FocusBehaviorRecursive != Control.FocusBehaviorRecursiveEnum.Disabled;
        float localWidth = availableGlobalWidth / Math.Max(0.001f, scale.X);
        int columns = Math.Max(1, (int)Math.Floor(localWidth / _cellWidth));
        if (_positionsDirty || columns != _lastColumns)
        {
            _positionsDirty = false;
            _lastColumns = columns;
            for (int i = 0; i < _visiblePowers.Count; i++)
                _visiblePowers[i].Position = new Vector2(i % columns * _cellWidth, i / columns * _rowHeight);
        }
        int rows = (_visiblePowers.Count + columns - 1) / columns;
        Vector2 size = new(localWidth, rows * _rowHeight);
        if (_powers.Size != size) _powers.Size = size;
    }

    private static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value) &&
        (value is not Node node || !node.IsQueuedForDeletion());
}
