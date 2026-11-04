using System;
using Godot;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NTopBarHealthHud : Node
{
    internal const string WidgetName = "HBRGOTopBarHealthHud";
    internal const string ViewMarker = "HBRGOTopBarHealthView";
    private static readonly Action<NHealthBar, Vector2> SetBarSize =
        PrivateAccess.MethodDelegate<NHealthBar, Action<NHealthBar, Vector2>>(
            "SetHpBarContainerSizeWithOffsetsImmediately", typeof(Vector2));
    private NGlobalUi _ui;
    private Player _player;
    private Control _nativeHp;
    private Control _heart;
    private Control _nativeText;
    private NHealthBar _view;
    private NCombatRoom _sourceRoom;
    private NHealthBar _sourceBar;
    private (int Hp, int MaxHp, int Block, HpDisplay Display, int Poison, int Doom)? _viewValues;
    private NDeflectorProtectionDpBar _viewDp;
    private Control _viewBlock;
    private Control _viewResistance;
    private Control _viewActionBar;
    private bool _viewExtrasDirty = true;
    private bool _enabled;
    private bool _heartVisible;
    private bool _textVisible;
    private Vector2 _minimumSize;
    private Vector2 _barSize = new(250f, 16f);
    private Vector2 _barScale = Vector2.One;
    private double _refreshElapsed;

    internal Control GaugeControl => _enabled && Usable(_view) ? _view.HpBarContainer : null;

    internal static void Install(NGlobalUi ui, RunState runState)
    {
        if (!Usable(ui?.TopBar)) return;
        Player player = LocalContext.GetMe(runState);
        if (player?.Creature == null) return;
        if (ui.TopBar.GetNodeOrNull<NTopBarHealthHud>(WidgetName) != null) return;
        var hud = new NTopBarHealthHud { Name = WidgetName };
        hud.Bind(ui, player);
        ui.TopBar.AddChild(hud);
    }

    private void Bind(NGlobalUi ui, Player player)
    {
        _ui = ui;
        _player = player;
        _nativeHp = ui.TopBar.Hp;
        _heart = _nativeHp.GetNodeOrNull<Control>("HpIcon");
        _nativeText = _nativeHp.GetNodeOrNull<Control>("HpLabel");
        _heartVisible = _heart?.Visible == true;
        _textVisible = _nativeText?.Visible == true;
        _minimumSize = _nativeHp.CustomMinimumSize;
    }

    public override void _Ready()
    {
        _player.Creature.CurrentHpChanged += OnHpChanged;
        _player.Creature.MaxHpChanged += OnHpChanged;
        RenderingServer.FramePreDraw += RefreshLayout;
        RefreshLayout();
    }

    public override void _Process(double delta)
    {
        _refreshElapsed += delta;
        if (_refreshElapsed < 0.1 || !_enabled || !Usable(_view)) return;
        _refreshElapsed = 0;
        RefreshViewValues();
        HideViewExtras();
    }

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= RefreshLayout;
        _sourceRoom = null;
        _sourceBar = null;
        if (_player?.Creature != null)
        {
            _player.Creature.CurrentHpChanged -= OnHpChanged;
            _player.Creature.MaxHpChanged -= OnHpChanged;
        }
        RestoreNativeTopBar();
        if (Usable(_view))
        {
            _view.ChildOrderChanged -= InvalidateViewExtras;
            _view.QueueFree();
        }
    }

    private void OnHpChanged(int previous, int current)
    {
        if (!Usable(_view)) return;
        RefreshViewValues();
        HideViewExtras();
    }

    private void RefreshViewValues()
    {
        Creature creature = _player.Creature;
        var values = (creature.CurrentHp, creature.MaxHp, creature.Block, creature.HpDisplay,
            creature.GetPower<PoisonPower>()?.CalculateTotalDamageNextTurn() ?? 0,
            creature.GetPowerAmount<DoomPower>());
        // Native RefreshValues also rebuilds text/theme overrides and runs all
        // health-bar postfixes. Poll its actual inputs, not the full UI, while
        // retaining poison/doom and block updates without an HP-change event.
        if (_viewValues == values) return;
        _viewValues = values;
        _view.RefreshValues();
    }

    private void CreateView()
    {
        _view = ResourceLoader.Load<PackedScene>(SceneHelper.GetScenePath("combat/health_bar"))
            .Instantiate<NHealthBar>();
        _viewExtrasDirty = true;
        _view.ChildOrderChanged += InvalidateViewExtras;
        _view.Name = "HBRGOTopBarHealthView";
        _viewValues = null;
        _view.SetMeta(ViewMarker, true);
        _view.MouseFilter = Control.MouseFilterEnum.Ignore;
        _view.TopLevel = true;
        _view.ZIndex = _ui.TopBar.ZIndex;
        _nativeHp.AddChild(_view);
        _view.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _view.Position = new Vector2(0f, 54f);
        // Ready initializes the native fields. The view shares the real hero;
        // it has no gameplay entity, duplicate relic or independent HP/DP.
        _view.SetCreature(_player.Creature);
        RefreshViewExtraBindings();
        if (Usable(_viewDp)) _viewDp.ShowCurrentRoll = false;
        RefreshViewValues();
        HideViewExtras();
    }

    private void HideViewExtras()
    {
        if (!Usable(_view)) return;
        RefreshViewExtraBindings();
        if (Usable(_viewBlock) && _viewBlock.Visible) _viewBlock.Hide();
        if (Usable(_viewResistance) && _viewResistance.Visible) _viewResistance.Hide();
        if (Usable(_viewActionBar) && _viewActionBar.Visible) _viewActionBar.Hide();
        if (Usable(_viewDp)) _viewDp.ShowCurrentRoll = false;
    }

    private void InvalidateViewExtras() => _viewExtrasDirty = true;

    private void RefreshViewExtraBindings()
    {
        if (!_viewExtrasDirty) return;
        _viewExtrasDirty = false;
        // Ritsu attachments can arrive after Ready or be replaced. The native
        // block subtree is fixed; attachment child changes invalidate all four.
        _viewBlock = _view.GetNodeOrNull<Control>("%BlockContainer");
        _viewResistance = _view.GetNodeOrNull<Control>("SixSenseResistanceRow");
        _viewActionBar = _view.GetNodeOrNull<Control>("RedCrimsonActionBar");
        _viewDp = _view.GetNodeOrNull<NDeflectorProtectionDpBar>("DeflectorProtectionDpBar");
    }

    private void RefreshLayout()
    {
        if (!Usable(_nativeHp) || !Usable(_ui?.TopBar)) return;
        bool enabled = HBRGOSakiConfig.ModUI;
        if (!enabled)
        {
            if (_enabled)
            {
                _enabled = false;
                RestoreNativeTopBar();
                if (Usable(_view)) _view.Hide();
            }
            return;
        }
        if (!Usable(_view)) CreateView();
        bool justEnabled = !_enabled;
        _enabled = true;
        _view.Show();
        if (justEnabled) RefreshViewValues();
        if (Usable(_heart)) _heart.Hide();
        if (Usable(_nativeText)) _nativeText.Hide();

        NCombatRoom room = NCombatRoom.Instance;
        if (!ReferenceEquals(room, _sourceRoom) || !Usable(_sourceBar) || !_sourceBar.IsInsideTree())
        {
            _sourceRoom = room;
            _sourceBar = Usable(room) ? room.GetCreatureNode(_player.Creature)
                ?.GetNodeOrNull<NHealthBar>("HealthBar/HealthBar") : null;
        }
        NHealthBar source = _sourceBar;
        if (Usable(source?.HpBarContainer))
        {
            // Mirror the battle gauges' dimensions and rendered scale while
            // retaining the original HP/DP bars and their native interaction.
            _barSize = source.HpBarContainer.Size;
            _barScale = source.HpBarContainer.GetGlobalTransform().Scale;
        }
        Transform2D parentTransform = _nativeHp.GetGlobalTransform();
        Vector2 parentScale = parentTransform.Scale;
        _view.Scale = _barScale;
        _view.Rotation = parentTransform.Rotation;
        _view.GlobalPosition = parentTransform * new Vector2(0f, 54f);
        _view.HpBarContainer.Position = Vector2.Zero;
        if (!_view.HpBarContainer.Size.IsEqualApprox(_barSize))
        {
            SetBarSize(_view, _barSize);
            // Native resizing only updates HP/middleground. Poison and doom
            // previews also need their offsets recomputed for the new width.
            _viewValues = null;
            RefreshViewValues();
        }
        _nativeHp.CustomMinimumSize = new Vector2(Math.Max(_minimumSize.X,
            _barSize.X * _barScale.X / Math.Max(0.001f, Math.Abs(parentScale.X))), Math.Max(80f, _minimumSize.Y));
        // DP already follows its model events and exact-value polling. Layout
        // only mirrors geometry; it must not force a second full refresh/frame.
        HideViewExtras();
    }

    private void RestoreNativeTopBar()
    {
        if (Usable(_heart)) _heart.Visible = _heartVisible;
        if (Usable(_nativeText)) _nativeText.Visible = _textVisible;
        if (Usable(_nativeHp)) _nativeHp.CustomMinimumSize = _minimumSize;
    }

    private static bool Usable(GodotObject value) => value != null && GodotObject.IsInstanceValid(value) &&
        (value is not Node node || !node.IsQueuedForDeletion());
}
