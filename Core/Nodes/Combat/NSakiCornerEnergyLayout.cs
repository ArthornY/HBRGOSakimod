using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Nodes.Combat;

/// <summary>
/// Docks the local Saki's original energy orb to the corner battle HUD
/// without changing its live values, visuals, input, or in/out animations.
/// </summary>
public partial class NSakiCornerEnergyLayout : Node
{
    internal const string WidgetName = "SakiCornerEnergyLayout";

    // The custom counter scene retains the native 128px energy root. The
    // shared corner HUD gives its center in CombatUi's local coordinates.
    private static readonly Vector2 CounterHalfSize = new(64f, 64f);

    private NCombatUi _ui;
    private Player _player;
    private Control _container;
    private Node _originalParent;
    private bool _hasOriginalLayout;
    private Vector4 _originalAnchors;
    private Vector4 _originalOffsets;
    private Vector2 _originalSize;

    public override void _EnterTree()
    {
        _ui = GetParent() as NCombatUi;
        RenderingServer.FramePreDraw += RefreshLayout;
    }

    public override void _Ready() => RefreshLayout();

    // Call after NCombatUi.Activate has created the native energy counter.
    // Rebinding is safe when the same combat UI is used for another combat.
    public void BindPlayer(Player player)
    {
        if (!ReferenceEquals(_player, player)) RestoreLayout();
        _player = LocalContext.IsMe(player) && player?.Character is HbrgoSaki
            ? player : null;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        RestoreLayout();
        _container = null;
        _player = null;
        _ui = null;
    }

    private void RefreshLayout()
    {
        Control nextContainer = Usable(_ui) ? _ui.EnergyCounterContainer : null;
        if (!ReferenceEquals(_container, nextContainer))
        {
            RestoreLayout();
            _container = nextContainer;
        }

        // The native wrapper is a direct child of CombatUi. Do not assume
        // its coordinate space if another mod has moved it to a new parent.
        if (!HBRGOSakiConfig.ModUI || !LocalContext.IsMe(_player) ||
            _player?.Character is not HbrgoSaki || !Usable(_container) ||
            !ReferenceEquals(_container.GetParent(), _ui) ||
            !NSakiCornerStatusHud.TryGetEnergyAnchor(_ui, out Vector2 center))
        {
            RestoreLayout();
            return;
        }

        if (!_hasOriginalLayout)
        {
            _originalAnchors = new Vector4(_container.AnchorLeft, _container.AnchorTop,
                _container.AnchorRight, _container.AnchorBottom);
            _originalOffsets = new Vector4(_container.OffsetLeft, _container.OffsetTop,
                _container.OffsetRight, _container.OffsetBottom);
            _originalSize = _container.Size;
            _originalParent = _container.GetParent();
            _hasOriginalLayout = true;
        }

        // Do not move NEnergyCounter itself: its native in/out tween still
        // owns Position. Preserve the complete orb, 3/3 label and particles.
        Vector2 offset = center - CounterHalfSize;
        SetLayout(Vector4.Zero,
            new Vector4(offset.X, offset.Y,
                offset.X + _originalSize.X, offset.Y + _originalSize.Y));
    }

    private void RestoreLayout()
    {
        if (!_hasOriginalLayout) return;
        // Reparenting establishes another coordinate space and may belong to
        // another mod. Do not overwrite that owner's new layout with offsets
        // captured against CombatUi; restore only while the parent is intact.
        if (Usable(_container) && ReferenceEquals(_container.GetParent(), _originalParent))
            SetLayout(_originalAnchors, _originalOffsets);
        _hasOriginalLayout = false;
        _originalParent = null;
    }

    private void SetLayout(Vector4 anchors, Vector4 offsets)
    {
        if (!Mathf.IsEqualApprox(_container.AnchorLeft, anchors.X)) _container.AnchorLeft = anchors.X;
        if (!Mathf.IsEqualApprox(_container.AnchorTop, anchors.Y)) _container.AnchorTop = anchors.Y;
        if (!Mathf.IsEqualApprox(_container.AnchorRight, anchors.Z)) _container.AnchorRight = anchors.Z;
        if (!Mathf.IsEqualApprox(_container.AnchorBottom, anchors.W)) _container.AnchorBottom = anchors.W;
        if (!Mathf.IsEqualApprox(_container.OffsetLeft, offsets.X)) _container.OffsetLeft = offsets.X;
        if (!Mathf.IsEqualApprox(_container.OffsetTop, offsets.Y)) _container.OffsetTop = offsets.Y;
        if (!Mathf.IsEqualApprox(_container.OffsetRight, offsets.Z)) _container.OffsetRight = offsets.Z;
        if (!Mathf.IsEqualApprox(_container.OffsetBottom, offsets.W)) _container.OffsetBottom = offsets.W;
    }

    private static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value) &&
        (value is not Node node || !node.IsQueuedForDeletion());
}
