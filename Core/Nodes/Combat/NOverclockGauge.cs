using System;
using Godot;
using HBRGOSakiMod.Core.DevConsole;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NOverclockGauge : Control
{
    private const float GaugeWidth = 380f;
    private const float GaugeHeight = 102f;
    private const float BarWidth = 280f;
    private const float BarHeight = 96f;
    private const float ViewportPadding = 12f;
    private const float AnnouncementOverflowTop = 30f;
    private const string FontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const string EmptyGaugePath = "res://images/ui/overclock/hbr_od_gauge_empty.png";
    private const string FullGaugePath = "res://images/ui/overclock/hbr_od_gauge_full.png";
    private const string InactiveEmblemPath = "res://images/ui/overclock/hbr_od_emblem_inactive.png";
    private const string ActiveEmblemPath = "res://images/ui/overclock/hbr_od_emblem_active.png";
    private const string LevelUpPath = "res://images/ui/overclock/hbr_od_level_up.png";

    private static readonly Vector2 BarPosition = new(0f, 2f);
    private static readonly Vector2 BarSize = new(BarWidth, BarHeight);
    private static Vector2? _savedPositionRatio;

    private Overclock _relic;
    private Control _fillClip;
    private TextureRect _fillTexture;
    private TextureRect _inactiveEmblem;
    private TextureRect _activeEmblem;
    private TextureRect _levelUpTexture;
    private MegaLabel _levelLabel;
    private MegaLabel _chargeLabel;
    private MegaLabel _announcementLabel;
    private float _displayCharge;
    private float _targetCharge;
    private float _chargePulseTime;
    private float _levelBurstTime;
    private float _animationTime;
    private int _observedCharge;
    private int _observedLevel;
    private int _observedActiveLevel;
    private bool _showLevelUpTexture;
    private bool _isDragging;
    private Vector2 _dragPointerOffset;

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
        BindRelic(null);
    }

    public override void _Process(double delta)
    {
        if (_relic == null)
        {
            return;
        }

        float deltaSeconds = (float)delta;
        _animationTime += deltaSeconds;
        if (!Mathf.IsEqualApprox(_displayCharge, _targetCharge))
        {
            float distance = Math.Abs(_targetCharge - _displayCharge);
            float speed = Math.Max(90f, distance * 6.5f);
            _displayCharge = Mathf.MoveToward(_displayCharge, _targetCharge, speed * deltaSeconds);
            RefreshGaugeFill();
        }

        UpdateNativeGlow();
        UpdateChargePulse(deltaSeconds);
        UpdateLevelBurst(deltaSeconds);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left
            } leftButton)
        {
            if (leftButton.Pressed)
            {
                _isDragging = true;
                _dragPointerOffset = GetGlobalMousePosition() - GlobalPosition;
                MouseDefaultCursorShape = CursorShape.Move;
            }
            else if (_isDragging)
            {
                _isDragging = false;
                MoveToClampedPosition(GetGlobalMousePosition() - _dragPointerOffset);
                SavePosition();
                RefreshText();
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

        if (inputEvent is not InputEventMouseButton
            {
                ButtonIndex: MouseButton.Right,
                Pressed: false
            } ||
            _relic?.CanActivate != true)
        {
            return;
        }

        int level = _relic.Level;
        string command =
            $"hbrgosakimod {FixedSoulRollConsoleCmd.ActivateOverclockSubcommand} {level}";
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
            new ConsoleCmdGameAction(_relic.Owner, command, CombatManager.Instance.IsInProgress));
        AcceptEvent();
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

        _relic = relic;
        if (_relic != null)
        {
            _relic.DisplayAmountChanged += OnChargeChanged;
        }

        Visible = _relic != null;
        RefreshImmediately();
    }

    private void BuildUi()
    {
        TextureRect shadow = CreateTextureRect(
            "GaugeShadow",
            EmptyGaugePath,
            BarPosition + new Vector2(4f, 5f),
            BarSize);
        shadow.Modulate = new Color(0f, 0f, 0f, 0.62f);
        AddChild(shadow);

        TextureRect emptyGauge = CreateTextureRect(
            "GaugeEmpty",
            EmptyGaugePath,
            BarPosition,
            BarSize);
        AddChild(emptyGauge);

        _fillClip = new Control
        {
            Name = "GaugeFillClip",
            Position = BarPosition,
            Size = new Vector2(0f, BarHeight),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_fillClip);

        _fillTexture = CreateTextureRect(
            "GaugeFill",
            FullGaugePath,
            Vector2.Zero,
            BarSize);
        _fillClip.AddChild(_fillTexture);

        _inactiveEmblem = CreateTextureRect(
            "InactiveEmblem",
            InactiveEmblemPath,
            new Vector2(241f, 0f),
            new Vector2(126f, 100f));
        AddChild(_inactiveEmblem);

        _activeEmblem = CreateTextureRect(
            "ActiveEmblem",
            ActiveEmblemPath,
            new Vector2(265f, -1f),
            new Vector2(104f, 104f));
        _activeEmblem.PivotOffset = _activeEmblem.Size / 2f;
        AddChild(_activeEmblem);

        _chargeLabel = CreateLabel(
            "Charge",
            "0 / 100",
            new Vector2(54f, 36f),
            new Vector2(166f, 24f),
            16,
            HorizontalAlignment.Center,
            new Color("F9EFFFD8"));
        _chargeLabel.Visible = false;
        AddChild(_chargeLabel);

        _levelLabel = CreateLabel(
            "Level",
            "OD Lv.0",
            new Vector2(265f, 77f),
            new Vector2(85f, 19f),
            13,
            HorizontalAlignment.Center,
            new Color("FFF4FFFF"));
        AddChild(_levelLabel);

        _levelUpTexture = CreateTextureRect(
            "LevelUp",
            LevelUpPath,
            new Vector2(109f, -25f),
            new Vector2(139f, 38f));
        _levelUpTexture.PivotOffset = _levelUpTexture.Size / 2f;
        _levelUpTexture.Visible = false;
        AddChild(_levelUpTexture);

        _announcementLabel = CreateLabel(
            "StateAnnouncement",
            string.Empty,
            new Vector2(74f, -24f),
            new Vector2(270f, 24f),
            17,
            HorizontalAlignment.Right,
            new Color("F9E7FFFF"));
        _announcementLabel.Visible = false;
        AddChild(_announcementLabel);
    }

    private void OnChargeChanged()
    {
        if (_relic == null)
        {
            return;
        }

        int nextCharge = _relic.Charge;
        int nextGaugeCharge = _relic.ChargeIntoLevel;
        int nextLevel = _relic.Level;
        int nextActiveLevel = _relic.ActiveLevel;
        bool crossedChargeThreshold =
            nextCharge > _observedCharge &&
            nextCharge / Overclock.ChargePerLevel >
            _observedCharge / Overclock.ChargePerLevel;
        if (nextCharge > _observedCharge)
        {
            _chargePulseTime = 0.34f;
        }

        if (crossedChargeThreshold)
        {
            _displayCharge = nextGaugeCharge;
        }

        if (nextLevel > _observedLevel)
        {
            StartLevelBurst(showLevelUpTexture: true);
        }

        if (nextActiveLevel > 0 && _observedActiveLevel == 0)
        {
            _announcementLabel.Text = $"OVERDRIVE Lv.{nextActiveLevel}";
            StartLevelBurst(showLevelUpTexture: false);
        }
        else if (nextActiveLevel == 0 && _observedActiveLevel > 0)
        {
            _announcementLabel.Text = "OVERDRIVE END";
            StartLevelBurst(showLevelUpTexture: false);
        }

        _observedCharge = nextCharge;
        _observedLevel = nextLevel;
        _observedActiveLevel = nextActiveLevel;
        _targetCharge = nextGaugeCharge;
        RefreshText();
        if (crossedChargeThreshold)
        {
            RefreshGaugeFill();
        }
    }

    private void StartLevelBurst(bool showLevelUpTexture)
    {
        _levelBurstTime = 1.25f;
        _showLevelUpTexture = showLevelUpTexture;
        _levelUpTexture.Visible = showLevelUpTexture;
        _announcementLabel.Visible = !showLevelUpTexture;
    }

    private void RefreshImmediately()
    {
        if (_relic == null || _levelLabel == null)
        {
            Visible = false;
            return;
        }

        Visible = true;
        _displayCharge = _relic.ChargeIntoLevel;
        _targetCharge = _relic.ChargeIntoLevel;
        _observedCharge = _relic.Charge;
        _observedLevel = _relic.Level;
        _observedActiveLevel = _relic.ActiveLevel;
        RefreshText();
        RefreshGaugeFill();
    }

    private void RefreshText()
    {
        bool isActive = _relic.ActiveLevel > 0;
        int displayedLevel = isActive ? _relic.ActiveLevel : _relic.Level;
        _levelLabel?.SetTextAutoSize(isActive
            ? $"OD Lv.{displayedLevel}"
            : $"OD Lv.{_relic.Level}");
        _chargeLabel?.SetTextAutoSize(
            $"{_relic.ChargeIntoLevel} / {Overclock.ChargePerLevel}");

        bool emblemIsReady = displayedLevel > 0;
        if (_activeEmblem != null)
        {
            _activeEmblem.Visible = emblemIsReady;
        }

        if (_inactiveEmblem != null)
        {
            _inactiveEmblem.Modulate = emblemIsReady
                ? new Color(1f, 1f, 1f, 0.42f)
                : Colors.White;
        }

        MouseDefaultCursorShape = _relic.CanActivate
            ? CursorShape.PointingHand
            : CursorShape.Move;
        TooltipText = _relic.CanActivate
            ? $"{_relic.Title.GetRawText()}\n{_relic.DynamicDescription.GetRawText()}\n左键拖动，右键启动当前等级。"
            : $"{_relic.Title.GetRawText()}\n{_relic.DynamicDescription.GetRawText()}\n左键拖动。";
    }

    private void RefreshGaugeFill()
    {
        if (_fillClip == null)
        {
            return;
        }

        float ratio = Mathf.Clamp(_displayCharge / Overclock.ChargePerLevel, 0f, 1f);
        _fillClip.Size = new Vector2(BarWidth * ratio, BarHeight);
        _fillClip.Visible = ratio > 0f;
    }

    private void OnMouseEntered()
    {
        if (_chargeLabel != null)
        {
            _chargeLabel.Visible = true;
        }
    }

    private void OnMouseExited()
    {
        if (_chargeLabel != null)
        {
            _chargeLabel.Visible = false;
        }
    }

    private void UpdateNativeGlow()
    {
        if (_fillTexture == null || _activeEmblem == null)
        {
            return;
        }

        float pulse = 0.88f + 0.12f * (Mathf.Sin(_animationTime * 4.6f) * 0.5f + 0.5f);
        _fillTexture.Modulate = new Color(1f, 1f, 1f, pulse);
        if (_activeEmblem.Visible)
        {
            float emblemPulse = _relic.ActiveLevel > 0
                ? 0.86f + 0.14f * (Mathf.Sin(_animationTime * 6.2f) * 0.5f + 0.5f)
                : pulse;
            _activeEmblem.Modulate = new Color(1f, 1f, 1f, emblemPulse);
        }
    }

    private void UpdateChargePulse(float delta)
    {
        if (_chargePulseTime <= 0f)
        {
            Scale = Vector2.One;
            return;
        }

        _chargePulseTime = Math.Max(0f, _chargePulseTime - delta);
        float normalized = _chargePulseTime / 0.34f;
        float pulse = Mathf.Sin((1f - normalized) * Mathf.Pi) * 0.025f;
        Scale = Vector2.One * (1f + pulse);
    }

    private void UpdateLevelBurst(float delta)
    {
        if (_levelBurstTime <= 0f)
        {
            _levelUpTexture.Visible = false;
            _announcementLabel.Visible = false;
            _activeEmblem.Scale = Vector2.One;
            return;
        }

        _levelBurstTime = Math.Max(0f, _levelBurstTime - delta);
        float elapsed = 1.25f - _levelBurstTime;
        float fadeIn = Mathf.Clamp(elapsed / 0.12f, 0f, 1f);
        float fadeOut = Mathf.Clamp(_levelBurstTime / 0.32f, 0f, 1f);
        float alpha = Math.Min(fadeIn, fadeOut);
        float burstScale = 1f + Mathf.Sin(Mathf.Clamp(elapsed / 0.42f, 0f, 1f) * Mathf.Pi) * 0.13f;

        _levelUpTexture.Visible = _showLevelUpTexture;
        _levelUpTexture.Modulate = new Color(1f, 1f, 1f, alpha);
        _levelUpTexture.Scale = Vector2.One * burstScale;
        _announcementLabel.Visible = !_showLevelUpTexture;
        _announcementLabel.Modulate = new Color(1f, 1f, 1f, alpha);
        _activeEmblem.Scale = Vector2.One * burstScale;
    }

    private static TextureRect CreateTextureRect(
        string name,
        string texturePath,
        Vector2 position,
        Vector2 size)
    {
        return new TextureRect
        {
            Name = name,
            Position = position,
            Size = size,
            Texture = LoadTexture(texturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

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
