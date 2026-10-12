using System;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NDeflectorProtectionDpBar : Control
{
    public bool ShowGauge { get; set; } = true;
    public bool ShowCurrentRoll { get; set; } = true;

    private const string HealthBarFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const int LayerSegmentCount = 3;
    private const float MonsterVerticalOffset = -6.4f;
    internal const float PlayerVerticalOffset = -8.699f;
    private const float RedCrimsonLevelSeparation = 37f;
    private const float RedCrimsonLevelOutline = 10f;
    private static readonly StringName ShadowOffsetX = "shadow_offset_x";
    private static readonly StringName ShadowOffsetY = "shadow_offset_y";
    private static readonly StringName ShadowOutlineSize = "shadow_outline_size";
    private static readonly Action<MegaLabel> AdjustNumberFont =
        PrivateAccess.MethodDelegate<MegaLabel, Action<MegaLabel>>("AdjustFontSize");

    private readonly Color _outlineColor = new("10435B");
    private NinePatchRect _background;
    private Control _foregroundContainer;
    private NinePatchRect _mask;
    private NinePatchRect _foreground;
    private NinePatchRect _frame;
    private ShaderMaterial _fillMaterial;
    private Control _layeredForeground;
    private Control[] _reserveBars;
    private NinePatchRect[] _reserveBackgrounds;
    private MegaLabel _label;
    private MegaLabel _valueTag;
    private MegaLabel _currentRollLabel;
    private MegaLabel _redCrimsonLevelLabel;
    private Control _hpBarContainer;
    private NHealthBar _nativeHealthBar;
    private Creature _creature;
    private DeflectorProtection _relic;
    private SoulOfAnchor _rollHistory;
    private HighDimensionalShellPower _shell;
    private RedCrimson _redCrimson;
    private bool _subscribed;
    private double _refreshElapsed;
    private bool _labelStateInitialized;
    private decimal _lastLabelAmount;
    private decimal _lastLabelMaximum;
    private int _lastLabelLayers;
    private bool _lastLabelUsesHp;
    private Tween _fillTween;
    private float _lastFillWidth = -1f;
    private float _targetFillRatio = -1f;
    private bool _layoutInitialized;
    private Vector2 _layoutSize;
    private Vector2 _layoutOffset;
    private bool _layoutPlayer;

    public void SetCreature(Creature creature, Control hpBarContainer, NHealthBar nativeHealthBar = null)
    {
        Unsubscribe();
        _creature = creature;
        _hpBarContainer = hpBarContainer;
        _nativeHealthBar = nativeHealthBar;
        _relic = creature.Player?.GetRelic<DeflectorProtection>();
        _rollHistory = creature.Player?.GetRelic<SoulOfAnchor>();
        _shell = creature.GetPower<HighDimensionalShellPower>();
        _redCrimson = creature?.Monster as RedCrimson;
        _labelStateInitialized = false;
        _layoutInitialized = false;
        Subscribe();
        SyncLayoutToHpBar();
        Refresh();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 1;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Size = new Vector2(250f, 16f);

        BuildUi();
        SyncLayoutToHpBar();
        Refresh();
    }

    public override void _Process(double delta)
    {
        SyncLayoutToHpBar();
        _refreshElapsed += delta;
        if (_refreshElapsed < 0.1)
        {
            return;
        }

        _refreshElapsed = 0;
        // A small hit on a very large HP pool can leave the integer proxy
        // unchanged. Refresh the exact HP text even without a native int event.
        if (_creature?.Monster != null && _nativeHealthBar != null &&
            GodotObject.IsInstanceValid(_nativeHealthBar) &&
            LargeCreatureHealthHelper.GetMaxHp(_creature) > BigCombatNumbers.NativeStatCap)
            _nativeHealthBar.RefreshValues();
        RefreshBindings();
        Refresh();
    }

    public override void _ExitTree()
    {
        _fillTween?.Kill();
        Unsubscribe();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            _labelStateInitialized = false;
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_foreground == null || _label == null || _valueTag == null)
        {
            return;
        }

        SyncLayoutToHpBar();
        RefreshCurrentRoll();
        RefreshRedCrimsonLevel();
        _background.Modulate = _creature?.IsEnemy == true
            ? HbrBattleGaugeStyle.BackgroundColor : Colors.White;
        decimal maxAmount;
        decimal currentAmount;
        int remainingLayers = -1;
        if (_relic != null)
        {
            _relic.EnsureDpInitialized();
            maxAmount = _relic.MaxDp;
            currentAmount = maxAmount > 0 ? Math.Clamp((decimal)_relic.Dp, 0m, maxAmount) : _relic.Dp;
        }
        else if (_shell is NararaLayeredDpPower layeredShell)
        {
            maxAmount = Math.Max(0m, layeredShell.MaximumDp);
            remainingLayers = Math.Clamp(layeredShell.RemainingLayers, 0, LayerSegmentCount);
            currentAmount = remainingLayers > 0
                ? Math.Clamp(layeredShell.CurrentDp, 0m, maxAmount)
                : 0;
        }
        else if (_shell != null)
        {
            maxAmount = Math.Max(
                _shell.MaximumDp,
                _shell.CurrentDp);
            currentAmount = Math.Clamp(_shell.CurrentDp, 0m, maxAmount);
        }
        else
        {
            maxAmount = currentAmount = 0;
        }

        // The player's upper-right readout remains available after Break and
        // switches to HP. It must not inherit a hidden empty-DP root.
        Visible = maxAmount > 0 || _creature?.IsPlayer == true;
        bool showGauge = ShowGauge && maxAmount > 0;
        _background.Visible = _foregroundContainer.Visible = _frame.Visible = showGauge;
        if (remainingLayers >= 0)
        {
            RefreshLayeredForeground(currentAmount, maxAmount, remainingLayers);
            RefreshDpLabel(currentAmount, maxAmount, remainingLayers);
            return;
        }

        if (_layeredForeground != null)
        {
            _layeredForeground.Visible = false;
        }

        float ratio = maxAmount > 0 ? (float)(currentAmount / maxAmount) : 0f;
        float maxWidth = _mask.Size.X > 0f ? _mask.Size.X : Size.X;
        RefreshFill(ratio, maxWidth);
        RefreshDpLabel(currentAmount, maxAmount, -1);
    }

    private void RefreshFill(float ratio, float maxWidth)
    {
        bool resized = !Mathf.IsEqualApprox(_lastFillWidth, maxWidth);
        bool initialize = _targetFillRatio < 0f || !_foreground.Visible || resized;
        _foreground.Visible = true;
        if (!initialize && Mathf.IsEqualApprox(_targetFillRatio, ratio)) return;

        _fillTween?.Kill();
        _lastFillWidth = maxWidth;
        _targetFillRatio = ratio;
        float target = maxWidth * (ratio - 1f);
        if (initialize || !IsInsideTree())
        {
            _foreground.OffsetRight = target;
            _fillMaterial.SetShaderParameter("fill_span", ratio);
            return;
        }

        // UIBattleHeadGauge CountupGageDataComponent duration = 0.15s.
        _fillTween = CreateTween().SetParallel();
        _fillTween.TweenProperty(_foreground, "offset_right", target, 0.15);
        _fillTween.TweenProperty(_fillMaterial, "shader_parameter/fill_span", ratio, 0.15);
    }

    private void RefreshDpLabel(decimal currentAmount, decimal maximumAmount, int remainingLayers)
    {
        bool player = _creature?.IsPlayer == true;
        bool visible = ShowGauge && _creature?.IsAlive == true && (player
            ? !_creature.HpDisplay.IsInfinite()
            : maximumAmount > 0 && remainingLayers != 0);
        _label.Visible = visible;
        _valueTag.Visible = visible && player;
        if (!visible) return;
        bool usesHp = player && currentAmount <= 0;
        decimal displayed = usesHp ? _creature.CurrentHp : currentAmount;
        if (_labelStateInitialized &&
            _lastLabelAmount == displayed &&
            _lastLabelMaximum == maximumAmount &&
            _lastLabelLayers == remainingLayers && _lastLabelUsesHp == usesHp)
        {
            return;
        }

        _labelStateInitialized = true;
        _lastLabelAmount = displayed;
        _lastLabelMaximum = maximumAmount;
        _lastLabelLayers = remainingLayers;
        _lastLabelUsesHp = usesHp;
        // Player DP retains its white readout. Break shows HP in DP blue;
        // monster DP uses the opposing HP gauge's pink tint.
        _label.AddThemeColorOverride(ThemeConstants.Label.FontColor,
            player
                ? usesHp ? HbrBattleGaugeStyle.HpNumberColor : Colors.White
                : HbrBattleGaugeStyle.MonsterDpNumberColor);
        _valueTag.SetTextAutoSize(usesHp ? "HP" : "DP");
        _label.SetTextAutoSize(player ? displayed.ToString() : $"{currentAmount}/{maximumAmount}");
    }

    private void RefreshLayeredForeground(decimal currentAmount, decimal maximumAmount, int remainingLayers)
    {
        if (_layeredForeground == null)
        {
            BuildLayeredForeground();
        }

        _layeredForeground.Visible = remainingLayers > 1;
        // UIBattleEnemy2's dpContainer1..5 overlap left/up by (9, 10).
        // Keep the highest visible (current) slot at the existing HUD anchor;
        // earlier full slots are therefore behind it, toward the right/bottom.
        for (int i = 1; i < LayerSegmentCount; i++)
        {
            _reserveBars[i - 1].Visible = i < remainingLayers;
            _reserveBackgrounds[i - 1].Modulate = _background.Modulate;
        }
        float maxWidth = _mask.Size.X > 0f ? _mask.Size.X : Size.X;
        RefreshFill(GetLayerFillRatio(0, remainingLayers, currentAmount, maximumAmount), maxWidth);
    }

    // Slot zero is the current DP pool; additional visible slots are full
    // reserves. Breaking a layer removes a whole slot, rather than a third of
    // a shared bar. Neither the stored DP values nor break limits change.
    internal static float GetLayerFillRatio(
        int segmentIndex, int remainingLayers, decimal currentAmount, decimal maximumAmount)
    {
        remainingLayers = Math.Clamp(remainingLayers, 0, LayerSegmentCount);
        if (segmentIndex < 0 || segmentIndex >= LayerSegmentCount ||
            remainingLayers == 0 || maximumAmount <= 0)
        {
            return 0f;
        }

        if (segmentIndex >= remainingLayers)
        {
            return 0f;
        }

        return segmentIndex == 0
            ? (float)(Math.Clamp(currentAmount, 0m, maximumAmount) / maximumAmount)
            : 1f;
    }

    private void BuildLayeredForeground()
    {
        _layeredForeground = new Control
        {
            Name = "DpLayeredForeground",
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_layeredForeground);
        AddChild(_layeredForeground);
        MoveChild(_layeredForeground, 0);
        _reserveBars = new Control[LayerSegmentCount - 1];
        _reserveBackgrounds = new NinePatchRect[LayerSegmentCount - 1];
        // Furthest reserve first, current native DP last, matching source
        // sibling order after normalizing the highest visible slot to (0, 0).
        for (int i = LayerSegmentCount - 1; i >= 1; i--)
        {
            var segment = new Control
            {
                Name = $"DpReserve{i}",
                MouseFilter = MouseFilterEnum.Ignore
            };
            _layeredForeground.AddChild(segment);
            SetFullRect(segment, 9f * i, 10f * i * HbrBattleGaugeSizing.GetThickness(_creature),
                9f * i, 10f * i * HbrBattleGaugeSizing.GetThickness(_creature));
            _reserveBars[i - 1] = segment;

            var background = new NinePatchRect();
            HbrBattleGaugeStyle.StyleBackground(background);
            segment.AddChild(background);
            SetFullRect(background);
            _reserveBackgrounds[i - 1] = background;

            var mask = new NinePatchRect { ClipChildren = ClipChildrenMode.Only };
            HbrBattleGaugeStyle.StyleMask(mask);
            segment.AddChild(mask);
            SetFullRect(mask);

            var fill = new TextureRect
            {
                Name = "Fill",
                SelfModulate = Colors.White,
                Texture = _foreground.Texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            mask.AddChild(fill);
            SetFullRect(fill);
            HbrBattleGaugeStyle.AddFrame(segment, "HbrDpReserveFrame");
        }
    }

    private void BuildUi()
    {
        if (_background != null)
        {
            return;
        }

        _background = new NinePatchRect
        {
            Name = "DpBackground",
            Modulate = HbrBattleGaugeStyle.BackgroundColor,
            MouseFilter = MouseFilterEnum.Ignore
        };
        HbrBattleGaugeStyle.StyleBackground(_background);
        SetFullRect(_background);
        AddChild(_background);

        _foregroundContainer = new Control
        {
            Name = "DpForegroundContainer",
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foregroundContainer);
        AddChild(_foregroundContainer);

        _mask = new NinePatchRect
        {
            Name = "DpMask",
            ClipChildren = ClipChildrenMode.Only,
            MouseFilter = MouseFilterEnum.Ignore
        };
        HbrBattleGaugeStyle.StyleMask(_mask);
        SetFullRect(_mask);
        _foregroundContainer.AddChild(_mask);

        _fillMaterial = HbrBattleGaugeStyle.CreateFillMaterial();
        _foreground = new NinePatchRect
        {
            Name = "DpForeground",
            SelfModulate = Colors.White,
            Texture = HbrBattleGaugeStyle.Dp,
            Material = _fillMaterial,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foreground);
        _mask.AddChild(_foreground);

        _frame = HbrBattleGaugeStyle.AddFrame(this, "HbrDpFrame");

        _label = new MegaLabel
        {
            Name = "DpLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 12,
            MaxFontSize = 24
        };
        SetFullRect(_label, 0f, -5f, 0f, 5f);
        _label.AddThemeColorOverride(ThemeConstants.Label.FontColor, new Color(1f, 0.964706f, 0.886275f));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color(0f, 0f, 0f, 0.25098f));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, _outlineColor);
        _label.AddThemeConstantOverride(ShadowOffsetX, 6);
        _label.AddThemeConstantOverride(ShadowOffsetY, 4);
        _label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 16);
        _label.AddThemeConstantOverride(ShadowOutlineSize, 0);
        _label.AddThemeFontOverride(ThemeConstants.Label.Font, LoadFont(HealthBarFontPath));
        _label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 24);
        HbrBattleGaugeStyle.StyleNumber(_label, 26);
        AddChild(_label);
        // MegaLabel._Ready substitutes the locale font in Chinese. Restore
        // the extracted numeral face after that native initialization.
        HbrBattleGaugeStyle.StyleNumber(_label, 26);
        _label.SetAnchorsPreset(LayoutPreset.TopLeft);
        _valueTag = new MegaLabel
        {
            Name = "GaugeValueTag",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        HbrBattleGaugeStyle.StyleNumber(_valueTag);
        AddChild(_valueTag);
        HbrBattleGaugeStyle.StyleNumber(_valueTag);

        _currentRollLabel = new MegaLabel
        {
            Name = "CurrentRollLabel",
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 0f,
            OffsetTop = -30f,
            OffsetRight = 0f,
            OffsetBottom = -4f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 10,
            MaxFontSize = 18,
            Visible = false
        };
        _currentRollLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            new Color("9EDCF7FF"));
        _currentRollLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            _outlineColor);
        _currentRollLabel.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            10);
        _currentRollLabel.AddThemeFontOverride(
            ThemeConstants.Label.Font,
            LoadFont(HealthBarFontPath));
        _currentRollLabel.AddThemeFontSizeOverride(
            ThemeConstants.Label.FontSize,
            18);
        AddChild(_currentRollLabel);

        _redCrimsonLevelLabel = new MegaLabel
        {
            Name = "RedCrimsonLevelLabel",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 10,
            MaxFontSize = 18
        };
        _redCrimsonLevelLabel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _redCrimsonLevelLabel.OffsetLeft = 0f;
        _redCrimsonLevelLabel.OffsetTop = -25f;
        _redCrimsonLevelLabel.OffsetRight = 80f;
        _redCrimsonLevelLabel.OffsetBottom = 0f;
        _redCrimsonLevelLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            new Color("FFE9D9"));
        _redCrimsonLevelLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            _outlineColor);
        _redCrimsonLevelLabel.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            10);
        _redCrimsonLevelLabel.AddThemeFontOverride(
            ThemeConstants.Label.Font,
            LoadFont(HealthBarFontPath));
        _redCrimsonLevelLabel.AddThemeFontSizeOverride(
            ThemeConstants.Label.FontSize,
            18);
        AddChild(_redCrimsonLevelLabel);
    }

    private void RefreshCurrentRoll()
    {
        if (_currentRollLabel == null)
        {
            return;
        }

        bool shouldShow =
            ShowCurrentRoll &&
            _relic != null &&
            _rollHistory != null &&
            _creature?.Player != null &&
            _creature.IsAlive;
        _currentRollLabel.Visible = shouldShow;
        if (!shouldShow)
        {
            return;
        }

        if (AmnesiaRollVisibilityHelper.IsRollHidden(_creature.Player))
        {
            _currentRollLabel.SetTextAutoSize("当前出目：？？？");
            return;
        }

        _currentRollLabel.SetTextAutoSize(
            _rollHistory.LastRoll > 0
                ? $"当前出目：{_rollHistory.LastRoll}"
                : "当前出目：—");
    }

    private void RefreshRedCrimsonLevel()
    {
        if (_redCrimsonLevelLabel == null)
        {
            return;
        }

        bool shouldShow =
            _redCrimson != null &&
            _creature?.IsAlive == true;
        _redCrimsonLevelLabel.Visible = shouldShow;
        if (shouldShow)
        {
            _redCrimsonLevelLabel.SetTextAutoSize(
                $"LV.{_redCrimson.SummonLevel}");
        }
    }

    private void SyncLayoutToHpBar()
    {
        if (!GodotObject.IsInstanceValid(_hpBarContainer))
        {
            return;
        }

        Vector2 offset = GetGaugeOffset(_creature, Vector2.One);
        GlobalPosition = _hpBarContainer.GlobalPosition +
            offset * _hpBarContainer.GetGlobalTransform().Scale;
        Vector2 size = new(_hpBarContainer.Size.X * GetWidthRatio(_creature), _hpBarContainer.Size.Y);
        if (!Size.IsEqualApprox(size))
        {
            Size = size;
        }
        if (_label == null || _valueTag == null) return;
        bool player = _creature?.IsPlayer == true;
        if (_layoutInitialized && _layoutSize.IsEqualApprox(size) &&
            _layoutOffset.IsEqualApprox(offset) && _layoutPlayer == player) return;
        _layoutInitialized = true;
        _layoutSize = size;
        _layoutOffset = offset;
        _layoutPlayer = player;
        _labelStateInitialized = false;

        if (_reserveBars != null)
        {
            for (int i = 0; i < _reserveBars.Length; i++)
            {
                float top = 10f * (i + 1) * HbrBattleGaugeSizing.GetThickness(_creature);
                SetFullRect(_reserveBars[i], 9f * (i + 1), top, 9f * (i + 1), top);
            }
        }

        if (player)
        {
            // Preserve the original upper-right player DP/HP readout.
            float widthScale = _hpBarContainer.Size.X / 225f;
            float tagHeight = Mathf.Ceil(18f * 32f / 23f);
            float valueHeight = Mathf.Ceil(26f * 32f / 23f);
            _valueTag.Position = new Vector2(110.6f * widthScale,
                -9f * HbrBattleGaugeSizing.GetThickness(_creature) - tagHeight / 2f) - offset;
            _valueTag.Size = new Vector2(30f * widthScale, tagHeight);
            HbrBattleGaugeStyle.StyleNumber(_label, 26);
            _label.Position = new Vector2(142f * widthScale,
                -14.5f * HbrBattleGaugeSizing.GetThickness(_creature) - valueHeight / 2f) - offset;
            _label.Size = new Vector2(90f * widthScale, valueHeight);
        }
        else
        {
            // One readout follows the active pool; reserve bars have no labels.
            // Center it inside the current DP frame, including stacked pools.
            // Grow the readout with the gauge height, while allowing long
            // values to fit its shorter width. Preserve MegaLabel's fitted
            // font cache instead of overwriting the theme font size.
            float numberScale = size.Y / HbrBattleGaugeSizing.BaseHeight;
            _label.ClipText = true;
            _label.MaxFontSize = Math.Max(1, Mathf.RoundToInt(12f * numberScale));
            _label.MinFontSize = 1;
            _label.Size = size;
            // The old font's minimum height can prevent a resize notification
            // when scrolling down. Fit explicitly, then reclaim that height.
            AdjustNumberFont(_label);
            _label.Size = size;
            _label.Position = new Vector2(0f, (size.Y - _label.Size.Y) * 0.5f);
        }
        if (_currentRollLabel != null)
        {
            _currentRollLabel.SetAnchorsPreset(LayoutPreset.TopLeft);
            _currentRollLabel.Position = new Vector2(0,
                -31f * HbrBattleGaugeSizing.GetThickness(_creature) - 28f) - offset;
            _currentRollLabel.Size = new Vector2(_hpBarContainer.Size.X, 26f);
        }
        if (_redCrimsonLevelLabel != null)
        {
            float levelTop = _redCrimson != null && !player
                ? Math.Min(-34f, offset.Y - RedCrimsonLevelSeparation) : -34f;
            _redCrimsonLevelLabel.Position = new Vector2(0, levelTop) - offset;
            _redCrimsonLevelLabel.Size = new Vector2(80f, 25f);
        }
    }

    internal static float GetWidthRatio(Creature creature) =>
        creature?.IsPlayer == true ? 108.09894f / 225f : 0.5f;

    internal static Vector2 GetGaugeOffset(Creature creature, Vector2 scale)
    {
        int remaining = creature?.GetPower<HighDimensionalShellPower>() is NararaLayeredDpPower layered
            ? Math.Clamp(layered.RemainingLayers, 0, LayerSegmentCount) : 1;
        int reserves = Math.Max(0, remaining - 1);
        bool player = creature?.IsPlayer == true;
        float thickness = HbrBattleGaugeSizing.GetThickness(creature);
        // Preserve the source's overlapping HP/DP layout. Their interior
        // borders are clipped against the opposite gauge's capsule at draw time.
        float verticalOffset = player
            ? PlayerVerticalOffset * thickness
            : MonsterVerticalOffset * thickness;
        return new Vector2(((player ? -0.26f : 0.9f) - 9f * reserves) * scale.X,
            (verticalOffset -
            HbrMultiHealthBar.GetExtraHeight(creature) - 10f * reserves * thickness) * scale.Y);
    }

    // Top occupied local Y relative to the native HP anchor. Ancillary HUD
    // rows share this boundary so they cannot overlap the compact HBR bars.
    internal static float GetHudTop(Creature creature)
    {
        float top = -HbrMultiHealthBar.GetExtraHeight(creature);
        HighDimensionalShellPower shell = creature?.GetPower<HighDimensionalShellPower>();
        if (creature?.Player?.GetRelic<DeflectorProtection>()?.MaxDp > 0 ||
            shell != null && Math.Max(shell.MaximumDp, shell.CurrentDp) > 0)
        {
            top = Math.Min(top, GetGaugeOffset(creature, Vector2.One).Y);
        }
        if (creature?.IsPlayer == true)
            top = Math.Min(top, -31f * HbrBattleGaugeSizing.GetThickness(creature) -
                (creature.Player.GetRelic<SoulOfAnchor>() != null ? 28f : 0f));
        if (creature?.Monster is RedCrimson)
            top = Math.Min(top, Math.Min(-58f,
                GetGaugeOffset(creature, Vector2.One).Y -
                RedCrimsonLevelSeparation - RedCrimsonLevelOutline));
        return top;
    }

    internal bool HitTestGauge(Vector2 pointer)
    {
        if (_creature?.Monster == null || _background?.Visible != true) return false;
        if (HbrBattleGaugeSizing.HitGauge(this, pointer)) return true;
        if (_reserveBars != null)
            foreach (Control reserve in _reserveBars)
                if (HbrBattleGaugeSizing.HitGauge(reserve, pointer)) return true;
        return false;
    }

    private static void SetFullRect(Control control, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }

    private static Font LoadFont(string path)
    {
        return ResourceLoader.Load<Font>(path);
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        if (_creature != null)
        {
            _creature.MaxHpChanged += OnCreatureMaxHpChanged;
            _creature.CurrentHpChanged += OnCreatureMaxHpChanged;
            _creature.PowerApplied += OnPowerApplied;
            _creature.PowerRemoved += OnPowerRemoved;
        }

        if (_relic != null)
        {
            _relic.DisplayAmountChanged += Refresh;
        }

        if (_rollHistory != null)
        {
            _rollHistory.DisplayAmountChanged += Refresh;
            _rollHistory.HistoryChanged += Refresh;
        }

        if (_shell != null)
        {
            _shell.DisplayAmountChanged += Refresh;
        }
    }

    private void RefreshBindings()
    {
        if (_creature == null)
        {
            return;
        }

        DeflectorProtection nextRelic = _creature.Player?.GetRelic<DeflectorProtection>();
        SoulOfAnchor nextRollHistory =
            _creature.Player?.GetRelic<SoulOfAnchor>();
        HighDimensionalShellPower nextShell = _creature.GetPower<HighDimensionalShellPower>();
        if (ReferenceEquals(_relic, nextRelic) &&
            ReferenceEquals(_rollHistory, nextRollHistory) &&
            ReferenceEquals(_shell, nextShell))
        {
            return;
        }

        Unsubscribe();
        _relic = nextRelic;
        _rollHistory = nextRollHistory;
        _shell = nextShell;
        Subscribe();
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        if (_creature != null)
        {
            _creature.MaxHpChanged -= OnCreatureMaxHpChanged;
            _creature.CurrentHpChanged -= OnCreatureMaxHpChanged;
            _creature.PowerApplied -= OnPowerApplied;
            _creature.PowerRemoved -= OnPowerRemoved;
        }

        if (_relic != null)
        {
            _relic.DisplayAmountChanged -= Refresh;
        }

        if (_rollHistory != null)
        {
            _rollHistory.DisplayAmountChanged -= Refresh;
            _rollHistory.HistoryChanged -= Refresh;
        }

        if (_shell != null)
        {
            _shell.DisplayAmountChanged -= Refresh;
        }
    }

    private void OnCreatureMaxHpChanged(int oldMaxHp, int newMaxHp)
    {
        Refresh();
    }

    private void OnPowerApplied(PowerModel power)
    {
        if (power is not HighDimensionalShellPower shell)
        {
            return;
        }

        BindShell(shell);
        Refresh();
    }

    private void OnPowerRemoved(PowerModel power)
    {
        if (!ReferenceEquals(power, _shell))
        {
            return;
        }

        BindShell(null);
        Refresh();
    }

    private void BindShell(HighDimensionalShellPower shell)
    {
        if (ReferenceEquals(_shell, shell))
        {
            return;
        }

        if (_shell != null)
        {
            _shell.DisplayAmountChanged -= Refresh;
        }

        _shell = shell;

        if (_shell != null && _subscribed)
        {
            _shell.DisplayAmountChanged += Refresh;
        }
    }
}
