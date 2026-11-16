using System;
using Godot;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NOverclockGauge : Control
{
    private const float GaugeWidth = 380f;
    private const float GaugeHeight = 102f;
    private const float ViewportPadding = 12f;
    private const float AnnouncementOverflowTop = 30f;
    private const string FontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const string OriginalAssetPath = "res://images/ui/overclock/original/";

    private static Vector2? _savedPositionRatio;

    private Overclock _relic;
    private readonly Control[] _fillClips = new Control[Overclock.MaxLevel];
    private readonly TextureRect[] _fills = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[] _buttons = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[] _activeButtons = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[] _rings = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[] _numbers = new TextureRect[Overclock.MaxLevel];
    private readonly TextureRect[] _markers = new TextureRect[Overclock.MaxLevel];
    private ShaderMaterial _fillFlash;
    private ShaderMaterial _disabledMaterial;
    private NHbrOverdriveButtonEffects _buttonEffects;
    private NHbrOverdriveFloorBudget _floorBudget;
    private MegaLabel _chargeLabel;
    private float _displayCharge;
    private float _targetCharge;
    private float _levelBurstTime;
    private float _animationTime;
    private int _observedLevel;
    private int _observedActiveLevel;
    private int _burstLevel;
    private int _observedRemainingFloorLevels = -1;
    private bool? _observedReadiness;
    private bool _isDragging;
    private bool _isHovered;
    private bool _rightPressed;
    private bool _rightPressCancelled;
    private NHoverTipSet _hoverTips;
    private Vector2 _dragPointerOffset;
    private NOverclockSelectionMenu _selectionMenu;

    private bool HasSelectionMenu => GodotObject.IsInstanceValid(_selectionMenu) &&
        !_selectionMenu.IsQueuedForDeletion();

    public override void _Ready()
    {
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -430f;
        OffsetRight = -50f;
        OffsetTop = 236f;
        OffsetBottom = 338f;
        CustomMinimumSize = new Vector2(GaugeWidth, GaugeHeight);
        PivotOffset = new Vector2(GaugeWidth / 2f, GaugeHeight / 2f);
        MouseFilter = MouseFilterEnum.Stop;
        // Native card-style hover tips render rich text; Godot's TooltipText
        // treats color tags as plain text and can stretch across the screen.
        TooltipText = string.Empty;
        ZIndex = 30;

        BuildUi();
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        RestoreSavedPosition();
        RefreshImmediately();
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
        _isHovered = false;
        _selectionMenu?.Close();
        HideHoverTip();
        BindRelic(null);
    }

    public override void _Process(double delta)
    {
        if (_relic == null || !IsVisibleInTree())
        {
            _isHovered = false;
            CancelRightPress();
            _buttonEffects?.ResetEffects();
            HideHoverTip();
            return;
        }

        // Opening a modal can clear native hover tips without moving the
        // pointer. Only explicit hover/drag-release events may recreate ours.
        if (_isDragging || HasSelectionMenu || NHoverTipSet.shouldBlockHoverTips)
        {
            CancelRightPress();
            HideHoverTip();
        }

        float deltaSeconds = (float)delta;
        RefreshFloorBudget();
        RefreshButtonReadiness();
        _animationTime = (_animationTime + deltaSeconds) % HbrOverdriveGaugeVisuals.FlashDuration;
        if (!Mathf.IsEqualApprox(_displayCharge, _targetCharge))
        {
            float distance = Math.Abs(_targetCharge - _displayCharge);
            float speed = Math.Max(90f, distance * 6.5f);
            _displayCharge = Mathf.MoveToward(_displayCharge, _targetCharge, speed * deltaSeconds);
            RefreshGaugeFill();
        }

        _fillFlash.SetShaderParameter("flash_amount", HbrOverdriveGaugeVisuals.Flash(_animationTime));
        UpdateLevelBurst(deltaSeconds);
        _buttonEffects.SetStandby(_relic.CanActivateLevel(_relic.Level) && !_isDragging && !_rightPressed &&
            !HasSelectionMenu && !NHoverTipSet.shouldBlockHoverTips);
        _buttonEffects.Advance(deltaSeconds);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (HasSelectionMenu) return;
        if (inputEvent is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left
            } leftButton)
        {
            if (leftButton.Pressed)
            {
                CancelRightPress();
                _isDragging = true;
                HideHoverTip();
                _dragPointerOffset = GetGlobalMousePosition() - GlobalPosition;
                MouseDefaultCursorShape = CursorShape.Move;
            }
            else if (_isDragging)
            {
                _isDragging = false;
                MoveToClampedPosition(GetGlobalMousePosition() - _dragPointerOffset);
                SavePosition();
                RefreshText();
                _isHovered = GetGlobalRect().HasPoint(GetGlobalMousePosition());
                ShowHoverTip();
            }

            AcceptEvent();
            return;
        }

        if (inputEvent is InputEventMouseMotion && _isDragging)
        {
            MoveToClampedPosition(GetGlobalMousePosition() - _dragPointerOffset);
            AcceptEvent();
            return;
        }

        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right } rightButton)
        {
            return;
        }

        if (rightButton.Pressed)
        {
            _rightPressCancelled = _isDragging || NHoverTipSet.shouldBlockHoverTips;
            if (!_rightPressCancelled && _relic != null && CombatManager.Instance.IsInProgress)
            {
                _rightPressed = true;
                _buttonEffects.SetStandby(false);
                _buttonEffects.Push();
                AcceptEvent();
            }
            return;
        }

        bool cancelled = _rightPressCancelled || !_rightPressed;
        _rightPressed = false;
        _rightPressCancelled = false;
        _buttonEffects.CancelPush();
        if (cancelled || _isDragging || _relic == null || !CombatManager.Instance.IsInProgress) return;
        HideHoverTip();
        _chargeLabel.Visible = false;
        _selectionMenu = NOverclockSelectionMenu.Show(_relic, ActivateSelectedLevel,
            () => _selectionMenu = null);
        AcceptEvent();
    }

    private void ActivateSelectedLevel(int level)
    {
        // Recheck at selection time; the synchronized action rechecks again
        // when executed if another player or queued action changed combat.
        if (_relic?.CanActivateLevel(level) != true) return;
        // A managed action, not a console command, so pressing the gauge never
        // excludes the run from the upload (only typed console commands do). It opens
        // the OD this gauge shows, which the party shares in co-op, whoever presses.
        HbrOverclockNetActions.Request(_relic, level);
    }

    private void RestoreSavedPosition()
    {
        if (!_savedPositionRatio.HasValue)
        {
            return;
        }

        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        Vector2 range = maximum - minimum;
        Vector2 ratio = _savedPositionRatio.Value.Clamp(Vector2.Zero, Vector2.One);
        MoveToClampedPosition(minimum + new Vector2(range.X * ratio.X, range.Y * ratio.Y));
    }

    private void SavePosition()
    {
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        Vector2 range = maximum - minimum;
        _savedPositionRatio = new Vector2(
            range.X <= 0f ? 0f : (GlobalPosition.X - minimum.X) / range.X,
            range.Y <= 0f ? 0f : (GlobalPosition.Y - minimum.Y) / range.Y);
    }

    private void MoveToClampedPosition(Vector2 position)
    {
        GetPositionBounds(out Vector2 minimum, out Vector2 maximum);
        GlobalPosition = position.Clamp(minimum, maximum);
    }

    private void GetPositionBounds(out Vector2 minimum, out Vector2 maximum)
    {
        Vector2 viewportSize = GetViewportRect().Size;
        minimum = new Vector2(ViewportPadding, ViewportPadding + AnnouncementOverflowTop);
        maximum = new Vector2(
            Math.Max(minimum.X, viewportSize.X - Size.X - ViewportPadding),
            Math.Max(minimum.Y, viewportSize.Y - Size.Y - ViewportPadding));
    }

    public void BindRelic(Overclock relic)
    {
        if (ReferenceEquals(_relic, relic))
        {
            RefreshImmediately();
            return;
        }

        if (_relic != null)
        {
            _relic.DisplayAmountChanged -= OnChargeChanged;
        }

        HideHoverTip();

        if (HasSelectionMenu) _selectionMenu.Close();

        CancelRightPress();
        _buttonEffects?.ResetEffects();

        _relic = relic;
        _observedRemainingFloorLevels = -1;
        _observedReadiness = null;
        if (_relic != null)
        {
            _relic.DisplayAmountChanged += OnChargeChanged;
        }

        Visible = _relic != null;
        RefreshImmediately();
        ShowHoverTip();
    }

    private void BuildUi()
    {
        // The source canvas is normalized to the left cap's edge and the
        // 192-pixel icon's top. All original dimensions share one HUD scale.
        Control artwork = new()
        {
            Name = "OriginalOverdrive",
            Position = new Vector2(7f, 0f),
            Size = new Vector2(HbrOverdriveGaugeVisuals.SourceWidth, HbrOverdriveGaugeVisuals.SourceHeight),
            Scale = Vector2.One * HbrOverdriveGaugeVisuals.DisplayScale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(artwork);

        Vector2 barPosition = new(HbrOverdriveGaugeVisuals.BarX, HbrOverdriveGaugeVisuals.BarY);
        Vector2 barSize = new(HbrOverdriveGaugeVisuals.BarWidth, HbrOverdriveGaugeVisuals.BarHeight);
        artwork.AddChild(CreateSlicedBar("GaugeBase", "GaugeOverdriveBase", barPosition, barSize));

        NinePatchRect fillMask = CreateSlicedBar("GaugeMask", "GaugeOverdriveBase", barPosition, barSize);
        fillMask.ClipChildren = ClipChildrenMode.Only;
        artwork.AddChild(fillMask);
        _fillFlash = new ShaderMaterial
        {
            Shader = ResourceLoader.Load<Shader>(OriginalAssetPath + "od_fill_flash.gdshader")
        };
        _disabledMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = "shader_type canvas_item; varying vec4 vertex_tint; void vertex(){ vertex_tint = COLOR; } " +
                    "void fragment(){ vec4 c = texture(TEXTURE, UV); " +
                    "float grey = dot(c.rgb, vec3(0.299, 0.587, 0.114)); COLOR = vec4(vec3(grey), c.a) * vertex_tint; }"
            }
        };
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            _fillClips[i] = new Control
            {
                Name = $"Level{i + 1}FillClip",
                Size = new Vector2(0f, HbrOverdriveGaugeVisuals.BarHeight),
                ClipContents = true,
                MouseFilter = MouseFilterEnum.Ignore
            };
            fillMask.AddChild(_fillClips[i]);
            _fills[i] = CreateOriginalSprite($"Level{i + 1}Fill", $"GaugeOverdrive{i + 1}",
                Vector2.Zero, new Vector2(HbrOverdriveGaugeVisuals.FillWidth, HbrOverdriveGaugeVisuals.BarHeight));
            _fills[i].Material = _fillFlash;
            _fillClips[i].AddChild(_fills[i]);
        }
        artwork.AddChild(CreateSlicedBar("GaugeFrame", "GaugeOverdriveFrame", barPosition, barSize));
        artwork.AddChild(CreateOriginalSprite("LeftFrame", "FrameOverdriveGaugeL",
            new Vector2(0f, 67.4f), new Vector2(60f, 60f)));
        artwork.AddChild(CreateOriginalSprite("RightFrame", "FrameOverdriveGaugeR",
            new Vector2(466.3f, 12f), new Vector2(212f, 168f)));

        Vector2 iconPosition = new(HbrOverdriveGaugeVisuals.ButtonX, 0f);
        Vector2 iconSize = new(192f, 192f);
        Vector2[] markerPositions =
        {
            new(583.4f, 54.5f), new(558.6f, 97f), new(606.6f, 97f)
        };
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            int level = i + 1;
            _buttons[i] = CreateOriginalSprite($"Level{level}Button", $"ButtonOverdrive{level}Default", iconPosition, iconSize);
            artwork.AddChild(_buttons[i]);
            _activeButtons[i] = CreateOriginalSprite($"Level{level}Active", $"ButtonOverdrive{level}Active", iconPosition, iconSize);
            artwork.AddChild(_activeButtons[i]);
            _rings[i] = CreateOriginalSprite($"Level{level}Burst", $"EffectOverdrive{level}", iconPosition, iconSize);
            _rings[i].PivotOffset = iconSize / 2f;
            _rings[i].Visible = false;
            artwork.AddChild(_rings[i]);
            _numbers[i] = CreateOriginalSprite($"Level{level}Number", $"NumberOverdrive{level}",
                new Vector2(565f, 66.2f), new Vector2(56f, 60f));
            artwork.AddChild(_numbers[i]);
            _markers[i] = CreateOriginalSprite($"Level{level}Marker", $"MarkerOverdrive{level}",
                markerPositions[i], new Vector2(24f, 24f));
            _markers[i].PivotOffset = new Vector2(12f, 12f);
            artwork.AddChild(_markers[i]);
        }

        _buttonEffects = new NHbrOverdriveButtonEffects
        {
            Name = "OriginalOverdriveButtonEffects",
            Position = new Vector2(HbrOverdriveGaugeVisuals.ButtonX + 96f, 96f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        artwork.AddChild(_buttonEffects);

        _floorBudget = new NHbrOverdriveFloorBudget
        {
            Name = "OriginalFloorBudgetTokens",
            Position = new Vector2(HbrOverdriveGaugeVisuals.BarX, 43f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        // The battle HUD preview drops the "全队本幕剩余 N / 12 级" caption: the token row
        // already shows the remaining levels (filled) and the used ones (empty).
        artwork.AddChild(_floorBudget);

        // Keep the existing numerical inspection readout outside the source
        // artwork. It appears only while hovered, along with the native tip.
        _chargeLabel = CreateLabel("Charge", string.Empty,
            new Vector2(46f, 66f), new Vector2(245f, 24f), 16,
            HorizontalAlignment.Center, new Color("F9EFFFD8"));
        _chargeLabel.Visible = false;
        AddChild(_chargeLabel);
    }

    private void OnChargeChanged()
    {
        if (_relic == null) return;

        int nextLevel = _relic.Level;
        int nextActiveLevel = _relic.ActiveLevel;
        // The command can be rejected by action timing. Animate activation
        // only after the relic reports that it actually became active.
        if (_observedActiveLevel == 0 && nextActiveLevel > 0)
        {
            _rightPressed = false;
            _buttonEffects?.Activate();
        }
        if (nextLevel > _observedLevel)
        {
            _burstLevel = nextLevel;
            _levelBurstTime = HbrOverdriveGaugeVisuals.LevelUpDuration;
        }
        else if (nextActiveLevel != _observedActiveLevel)
        {
            _levelBurstTime = 0f;
        }

        _observedLevel = nextLevel;
        _observedActiveLevel = nextActiveLevel;
        _targetCharge = (float)_relic.PreciseCharge;
        RefreshText();
    }

    private void RefreshImmediately()
    {
        if (_relic == null || _chargeLabel == null)
        {
            Visible = false;
            return;
        }

        Visible = true;
        _displayCharge = (float)_relic.PreciseCharge;
        _targetCharge = _displayCharge;
        _observedLevel = _relic.Level;
        _observedActiveLevel = _relic.ActiveLevel;
        _levelBurstTime = 0f;
        RefreshText();
        RefreshGaugeFill();
        UpdateLevelBurst(0f);
    }

    private void RefreshText()
    {
        bool active = _relic.ActiveLevel > 0;
        int displayedLevel = active ? _relic.ActiveLevel : _relic.Level;
        _chargeLabel?.SetTextAutoSize($"{_relic.DisplayChargeIntoLevel} / {_relic.ChargePerLevel}");
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            bool selected = displayedLevel == i + 1;
            _buttons[i].Visible = selected && !active;
            _activeButtons[i].Visible = selected && active;
            _numbers[i].Visible = selected;
            _markers[i].Visible = selected;
        }

        MouseDefaultCursorShape = _relic.CanActivate
            ? CursorShape.PointingHand
            : CursorShape.Move;
        RefreshFloorBudget();
        RefreshButtonReadiness();
    }

    private void RefreshFloorBudget()
    {
        int remaining = _relic.RemainingFloorLevels;
        if (_observedRemainingFloorLevels == remaining) return;
        _observedRemainingFloorLevels = remaining;
        _floorBudget?.SetRemaining(remaining);
    }

    private void RefreshButtonReadiness()
    {
        int displayedLevel = _relic.ActiveLevel > 0 ? _relic.ActiveLevel : _relic.Level;
        bool ready = _relic.ActiveLevel > 0 || _relic.CanActivateLevel(displayedLevel);
        if (_observedReadiness == ready) return;
        _observedReadiness = ready;
        ShaderMaterial material = ready ? null : _disabledMaterial;
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            _buttons[i].Material = material;
            _numbers[i].Material = material;
            _markers[i].Material = material;
        }
    }

    private void RefreshGaugeFill()
    {
        if (_fillClips[0] == null) return;
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            float ratio = HbrOverdriveGaugeVisuals.LevelFill(_displayCharge, _relic.ChargePerLevel, i);
            float hiddenWidth = HbrOverdriveGaugeVisuals.FillWidth * (1f - ratio);
            _fillClips[i].Position = new Vector2(HbrOverdriveGaugeVisuals.FillInset + hiddenWidth, 0f);
            _fillClips[i].Size = new Vector2(HbrOverdriveGaugeVisuals.FillWidth * ratio, HbrOverdriveGaugeVisuals.BarHeight);
            _fills[i].Position = new Vector2(-hiddenWidth, 0f);
            _fillClips[i].Visible = ratio > 0f;
        }
    }

    private void OnMouseEntered()
    {
        _isHovered = true;
        if (_chargeLabel != null)
        {
            _chargeLabel.Visible = true;
        }
        ShowHoverTip();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        CancelRightPress();
        HideHoverTip();
        if (_chargeLabel != null)
        {
            _chargeLabel.Visible = false;
        }
    }

    private void CancelRightPress()
    {
        if (_rightPressed) _rightPressCancelled = true;
        _rightPressed = false;
        _buttonEffects?.CancelPush();
    }

    private void ShowHoverTip()
    {
        if (!_isHovered || _isDragging || HasSelectionMenu || _relic == null || !IsVisibleInTree() ||
            NHoverTipSet.shouldBlockHoverTips ||
            (GodotObject.IsInstanceValid(_hoverTips) && !_hoverTips.IsQueuedForDeletion()))
            return;

        // This explanation is independent of activation readiness, so charge
        // changes and action locks never rebuild a stationary reading tooltip.
        LocString description = new("relics", "OVERCLOCK.gaugeDescription");
        description.Add("chargePerLevel", _relic.ChargePerLevel);
        description.Add("energyPrefix", EnergyIconHelper.GetPrefix(_relic));
        NHoverTipSet.Remove(this);
        _hoverTips = NHoverTipSet.CreateAndShow(this, new HoverTip(_relic.Title, description),
            HoverTip.GetHoverTipAlignment(this));
    }

    private void HideHoverTip()
    {
        if (GodotObject.IsInstanceValid(_hoverTips)) _hoverTips.Hide();
        NHoverTipSet.Remove(this);
        _hoverTips = null;
    }

    private void UpdateLevelBurst(float delta)
    {
        _levelBurstTime = Math.Max(0f, _levelBurstTime - delta);
        float elapsed = HbrOverdriveGaugeVisuals.LevelUpDuration - _levelBurstTime;
        bool playing = _levelBurstTime > 0f;
        float ringAlpha = playing ? HbrOverdriveGaugeVisuals.LevelRingAlpha(elapsed) : 0f;
        float markerScale = playing ? HbrOverdriveGaugeVisuals.LevelMarkerScale(elapsed) : 1f;
        float detailsAlpha = playing ? HbrOverdriveGaugeVisuals.LevelDetailsAlpha(elapsed) : 1f;
        for (int i = 0; i < Overclock.MaxLevel; i++)
        {
            _rings[i].Visible = playing && _burstLevel == i + 1;
            _rings[i].Scale = Vector2.One * HbrOverdriveGaugeVisuals.LevelRingScale(elapsed);
            _rings[i].Modulate = new Color(1f, 1f, 1f, ringAlpha * (125f / 255f));
            _markers[i].Scale = Vector2.One * markerScale;
            _markers[i].Modulate = new Color(1f, 1f, 1f, detailsAlpha);
            _numbers[i].Modulate = new Color(1f, 1f, 1f, detailsAlpha);
        }
    }

    private static NinePatchRect CreateSlicedBar(string name, string sprite, Vector2 position, Vector2 size) => new()
    {
        Name = name,
        Position = position,
        Size = size,
        Texture = LoadTexture(OriginalAssetPath + sprite + ".png"),
        PatchMarginLeft = 20,
        PatchMarginRight = 20,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private static TextureRect CreateOriginalSprite(string name, string sprite, Vector2 position, Vector2 size) => new()
    {
        Name = name,
        Position = position,
        Size = size,
        Texture = LoadTexture(OriginalAssetPath + sprite + ".png"),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private static MegaLabel CreateLabel(
        string name,
        string text,
        Vector2 position,
        Vector2 size,
        int fontSize,
        HorizontalAlignment alignment,
        Color color)
    {
        MegaLabel label = new()
        {
            Name = name,
            Position = position,
            Size = size,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 10,
            MaxFontSize = fontSize
        };
        label.SetTextAutoSize(text);
        label.AddThemeFontOverride("font", LoadFont());
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color("16051BE6"));
        label.AddThemeConstantOverride("outline_size", 7);
        return label;
    }

    private static Texture2D LoadTexture(string path)
    {
        return ResourceLoader.Load<Texture2D>(path);
    }

    private static Font LoadFont()
    {
        return ResourceLoader.Load<Font>(FontPath);
    }
}
