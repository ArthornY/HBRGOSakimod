using System.Runtime.CompilerServices;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Nodes.Combat;

/// <summary>
/// Places the existing, creature-bound health bar beneath the native energy
/// orb. No health view, gameplay entity, label or DP binding is duplicated.
/// </summary>
public partial class NSakiCornerStatusHud : Node
{
    internal const string WidgetName = "SakiCornerStatusHud";
    internal const float HealthBarWidth = 220f;
    private static readonly Vector2 EnergyBottomLeftOffset = new(245f, -270f);
    private static readonly Vector2 HpOffsetFromEnergy = new(-110f, 56f);
    private const float EdgePadding = 12f;

    private static readonly ConditionalWeakTable<NCombatUi, NSakiCornerStatusHud> Widgets = new();
    private static readonly ConditionalWeakTable<NHealthBar, NSakiCornerStatusHud> BarOwners = new();

    private NCombatUi _ui;
    private Player _player;
    private NCreature _creatureNode;
    private NHealthBar _bar;
    private NDeflectorProtectionDpBar _dp;
    private Control _hoverHitbox;
    private ControlLayout _barLayout;
    private ControlLayout _hpLayout;
    private ControlLayout _hoverLayout;
    private ControlLayout[] _dpGaugeLayouts;
    private bool _hasLayout;
    private bool _refreshing;
    private int _originalZIndex;
    private bool _originalZAsRelative;

    public override void _EnterTree()
    {
        _ui = GetParent() as NCombatUi;
        if (_ui != null)
        {
            Widgets.Remove(_ui);
            Widgets.Add(_ui, this);
        }
        RenderingServer.FramePreDraw += RefreshNow;
    }

    public override void _Ready() => RefreshNow();

    public void BindPlayer(Player player)
    {
        if (!ReferenceEquals(_player, player)) RestoreNativeLayout();
        // HbrgoSaki is shared by the 31O and 30A styles.
        _player = LocalContext.IsMe(player) && player?.Character is HbrgoSaki ? player : null;
        RefreshNow();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshNow;
        RestoreNativeLayout();
        if (_ui != null && Widgets.TryGetValue(_ui, out NSakiCornerStatusHud current) &&
            ReferenceEquals(current, this)) Widgets.Remove(_ui);
        _player = null;
        _ui = null;
    }

    /// <summary>
    /// Shared with the energy wrapper: coordinates are local to CombatUi.
    /// Godot's UI/canvas transform supplies physical viewport scaling; the
    /// offsets remain relative to the current logical canvas's bottom-left.
    /// </summary>
    internal static bool TryGetEnergyAnchor(NCombatUi ui, out Vector2 center)
    {
        center = default;
        if (!Usable(ui) || !Widgets.TryGetValue(ui, out NSakiCornerStatusHud widget) ||
            !widget.IsActiveFor(ui)) return false;

        Vector2 size = ui.Size;
        Vector2 minimum = new Vector2(110f + EdgePadding, 64f + EdgePadding).Min(size * 0.5f);
        Vector2 maximum = (size - new Vector2(110f + EdgePadding, 84f)).Max(minimum);
        center = (new Vector2(0f, size.Y) + EnergyBottomLeftOffset).Clamp(minimum, maximum);
        return true;
    }

    /// <summary>
    /// Native bounds updates assign child global X positions. Reset our root
    /// transform first, otherwise repeated updates accumulate equal/opposite
    /// root and child offsets. Called by the native-layout Harmony prefix.
    /// </summary>
    internal static void PrepareForNativeBounds(NHealthBar bar)
    {
        if (bar != null && BarOwners.TryGetValue(bar, out NSakiCornerStatusHud widget) &&
            widget._hasLayout) widget._barLayout.Restore();
    }

    // A character layout which runs later in FramePreDraw can finish by
    // calling this overload, after its final body/bounds placement.
    internal static void RefreshNow(NCombatUi ui)
    {
        if (ui != null && Widgets.TryGetValue(ui, out NSakiCornerStatusHud widget)) widget.RefreshNow();
    }

    public void RefreshNow()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            if (!TryGetEnergyAnchor(_ui, out Vector2 energyCenter))
            {
                RestoreNativeLayout();
                return;
            }

            NCreature creature = NCombatRoom.Instance.GetCreatureNode(_player.Creature);
            NCreatureStateDisplay state = Usable(creature)
                ? creature.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar") : null;
            NHealthBar bar = Usable(state) ? state.GetNodeOrNull<NHealthBar>("%HealthBar") : null;
            if (!Usable(bar) || !Usable(bar.HpBarContainer))
            {
                RestoreNativeLayout();
                return;
            }
            if (!ReferenceEquals(_bar, bar))
            {
                RestoreNativeLayout();
                _creatureNode = creature;
                _bar = bar;
                _barLayout = new ControlLayout(bar);
                _hpLayout = new ControlLayout(bar.HpBarContainer);
                _hoverHitbox = bar.GetNodeOrNull<Control>("%HpBarHitbox");
                _hoverLayout = Usable(_hoverHitbox) ? new ControlLayout(_hoverHitbox) : null;
                _originalZIndex = bar.ZIndex;
                _originalZAsRelative = bar.ZAsRelative;
                _hasLayout = true;
                BarOwners.Remove(bar);
                BarOwners.Add(bar, this);
            }
            if (!_barLayout.HasOriginalParent || !_hpLayout.HasOriginalParent)
            {
                RestoreNativeLayout();
                return;
            }

            Control hp = bar.HpBarContainer;
            // Resize through the native API so its foreground-width cache,
            // poison/doom previews and damage tail retain correct ratios.
            if (!Mathf.IsEqualApprox(hp.Size.X, HealthBarWidth))
                bar.UpdateWidthRelativeToReferenceValue(Mathf.Max(1f, _player.Creature.MaxHp), HealthBarWidth);
            hp.Scale = Vector2.One;
            hp.Rotation = 0f;
            hp.PivotOffset = Vector2.Zero;

            if (bar.GetParent() is not CanvasItem parent) return;
            Transform2D parentCanvas = parent.GetGlobalTransformWithCanvas();
            if (Mathf.IsZeroApprox(parentCanvas.Determinant())) return;
            Transform2D uiToParent = parentCanvas.AffineInverse() * _ui.GetGlobalTransformWithCanvas();
            // Only NHealthBar moves. Its original parent, creature subscriptions,
            // show/hide opacity, powers and nameplate remain native.
            bar.PivotOffset = Vector2.Zero;
            bar.Scale = uiToParent.Scale;
            bar.Rotation = uiToParent.Rotation;
            bar.Position = uiToParent * (energyCenter + HpOffsetFromEnergy - hp.Position);
            // Above the corner artwork (-10), behind hand/energy UI (0),
            // independent of the scene/creature parent's inherited Z.
            bar.ZAsRelative = false;
            bar.ZIndex = -1;

            if (Usable(_hoverHitbox) && _hoverLayout.HasOriginalParent)
            {
                // Keep the native signals and dead/revived mouse filter.
                _hoverHitbox.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
                _hoverHitbox.Position = hp.Position + new Vector2(-6f, -10f);
                _hoverHitbox.Size = hp.Size + new Vector2(12f, 20f);
            }
            RefreshDp(bar);
            bar.GetNodeOrNull<NHbrBattleGaugeFrameOverlap>("HbrGaugeFrameOverlap")?.RefreshOverlap();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private bool IsActiveFor(NCombatUi ui)
    {
        NCombatRoom room = NCombatRoom.Instance;
        // Keep the wrapper anchored during the native energy exit animation.
        // Combat ending precedes the UI being hidden by several frames.
        return HBRGOSakiConfig.ModUI && ReferenceEquals(_ui, ui) && ui.IsVisibleInTree() &&
            ui.Size.X > 0f && ui.Size.Y > 0f && LocalContext.IsMe(_player) &&
            _player?.Character is HbrgoSaki &&
            Usable(room) && ReferenceEquals(room.Ui, ui) &&
            room.Mode == CombatRoomMode.ActiveCombat;
    }

    private void RefreshDp(NHealthBar bar)
    {
        HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(bar,
            HBRGOSakiCombatUiIntegration.DeflectorProtectionDpBarAttachmentId,
            out NDeflectorProtectionDpBar dp);
        if (!ReferenceEquals(_dp, dp))
        {
            RestoreDpGaugeLayouts();
            _dp = dp;
            if (Usable(dp))
            {
                _dpGaugeLayouts = new[]
                {
                    Capture(dp.GetNodeOrNull<Control>("DpBackground")),
                    Capture(dp.GetNodeOrNull<Control>("DpForegroundContainer")),
                    Capture(dp.GetNodeOrNull<Control>("HbrDpFrame"))
                };
            }
        }
        if (!Usable(_dp)) return;
        _dp.Refresh();
        // Keep the existing 108/225 player DP width and its numeric readout.
        // Shift only its three gauge roots to the HP bar's right edge; moving
        // the DP root would also displace the native upper-right numbers.
        Vector2 offset = NDeflectorProtectionDpBar.GetGaugeOffset(_player.Creature, Vector2.One);
        float shift = bar.HpBarContainer.Size.X - _dp.Size.X - offset.X;
        if (_dpGaugeLayouts == null) return;
        foreach (ControlLayout layout in _dpGaugeLayouts) layout?.SetHorizontalOffset(shift);
    }

    private void RestoreDpGaugeLayouts()
    {
        if (_dpGaugeLayouts != null)
            foreach (ControlLayout layout in _dpGaugeLayouts) layout?.Restore();
        _dpGaugeLayouts = null;
        _dp = null;
    }

    private void RestoreNativeLayout()
    {
        if (!_hasLayout) return;
        RestoreDpGaugeLayouts();
        if (Usable(_bar))
        {
            BarOwners.Remove(_bar);
            if (_barLayout.HasOriginalParent && _hpLayout.HasOriginalParent)
            {
                _barLayout.Restore();
                // Call the native width setter before restoring offsets, so
                // its internal fill-width cache changes along with the size.
                if (_player?.Creature != null)
                    _bar.UpdateWidthRelativeToReferenceValue(Mathf.Max(1f, _player.Creature.MaxHp), _hpLayout.Size.X);
                _hpLayout.Restore();
                _hoverLayout?.Restore();
                _bar.ZIndex = _originalZIndex;
                _bar.ZAsRelative = _originalZAsRelative;
                if (Usable(_creatureNode) && Usable(_creatureNode.Hitbox))
                    _bar.UpdateLayoutForCreatureBounds(_creatureNode.Hitbox);
                _bar.RefreshValues();
            }
        }
        _hasLayout = false;
        _bar = null;
        _creatureNode = null;
        _hoverHitbox = null;
        _barLayout = _hpLayout = _hoverLayout = null;
    }

    private static ControlLayout Capture(Control control) => Usable(control) ? new ControlLayout(control) : null;

    private sealed class ControlLayout
    {
        private readonly Control _control;
        private readonly Node _parent;
        private readonly Vector4 _anchors;
        private readonly Vector4 _offsets;
        private readonly Vector2 _scale;
        private readonly Vector2 _pivot;
        private readonly float _rotation;
        internal Vector2 Size { get; }
        internal bool HasOriginalParent => Usable(_control) && ReferenceEquals(_control.GetParent(), _parent);

        internal ControlLayout(Control control)
        {
            _control = control;
            _parent = control.GetParent();
            _anchors = new Vector4(control.AnchorLeft, control.AnchorTop, control.AnchorRight, control.AnchorBottom);
            _offsets = new Vector4(control.OffsetLeft, control.OffsetTop, control.OffsetRight, control.OffsetBottom);
            _scale = control.Scale;
            _pivot = control.PivotOffset;
            _rotation = control.Rotation;
            Size = control.Size;
        }

        internal void Restore()
        {
            if (!HasOriginalParent) return;
            _control.AnchorLeft = _anchors.X;
            _control.AnchorTop = _anchors.Y;
            _control.AnchorRight = _anchors.Z;
            _control.AnchorBottom = _anchors.W;
            _control.OffsetLeft = _offsets.X;
            _control.OffsetTop = _offsets.Y;
            _control.OffsetRight = _offsets.Z;
            _control.OffsetBottom = _offsets.W;
            _control.Scale = _scale;
            _control.PivotOffset = _pivot;
            _control.Rotation = _rotation;
        }

        internal void SetHorizontalOffset(float shift)
        {
            if (!HasOriginalParent) return;
            _control.OffsetLeft = _offsets.X + shift;
            _control.OffsetRight = _offsets.Z + shift;
        }
    }

    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
