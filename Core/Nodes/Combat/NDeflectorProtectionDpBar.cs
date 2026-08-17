using System;
using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Nodes.Combat;

public partial class NDeflectorProtectionDpBar : Control
{
    private const string HealthBarBackgroundPath = "res://images/ui/combat/health_bar_bg.png";
    private const string HealthBarMaskPath = "res://images/ui/combat/health_bar.png";
    private const string HealthBarFillPath = "res://images/ui/combat/health_bar_fill.png";
    private const string HealthBarFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const float VerticalOffsetFromHpBar = -36f;
    private static readonly StringName ShadowOffsetX = "shadow_offset_x";
    private static readonly StringName ShadowOffsetY = "shadow_offset_y";
    private static readonly StringName ShadowOutlineSize = "shadow_outline_size";

    private readonly Color _outlineColor = new("10435B");
    private NinePatchRect _background;
    private Control _foregroundContainer;
    private NinePatchRect _mask;
    private NinePatchRect _foreground;
    private MegaLabel _label;
    private MegaLabel _currentRollLabel;
    private MegaLabel _redCrimsonLevelLabel;
    private Control _hpBarContainer;
    private Creature _creature;
    private DeflectorProtection _relic;
    private SoulOfAnchor _rollHistory;
    private HighDimensionalShellPower _shell;
    private RedCrimson _redCrimson;
    private bool _subscribed;
    private double _refreshElapsed;

    public void SetCreature(Creature creature, Control hpBarContainer)
    {
        Unsubscribe();
        _creature = creature;
        _hpBarContainer = hpBarContainer;
        _relic = creature.Player?.GetRelic<DeflectorProtection>();
        _rollHistory = creature.Player?.GetRelic<SoulOfAnchor>();
        _shell = creature.GetPower<HighDimensionalShellPower>();
        _redCrimson = creature?.Monster as RedCrimson;
        Subscribe();
        SyncLayoutToHpBar();
        Refresh();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Size = new Vector2(250f, 16f);

        BuildUi();
        SyncLayoutToHpBar();
        Refresh();
    }

    public override void _Process(double delta)
    {
        _refreshElapsed += delta;
        if (_refreshElapsed < 0.1)
        {
            return;
        }

        _refreshElapsed = 0;
        RefreshBindings();
        Refresh();
    }

    public override void _ExitTree()
    {
        Unsubscribe();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_foreground == null || _label == null)
        {
            return;
        }

        SyncLayoutToHpBar();
        RefreshCurrentRoll();
        RefreshRedCrimsonLevel();
        int maxAmount;
        int currentAmount;
        if (_relic != null)
        {
            _relic.EnsureDpInitialized();
            maxAmount = _relic.MaxDp;
            currentAmount = maxAmount > 0 ? Mathf.Clamp(_relic.Dp, 0, maxAmount) : _relic.Dp;
        }
        else if (_shell != null)
        {
            maxAmount = Math.Max(
                HighDimensionalShellPower.CalculateAmount(_creature),
                _shell.Amount);
            currentAmount = Mathf.Clamp(_shell.Amount, 0, maxAmount);
        }
        else
        {
            Visible = false;
            _currentRollLabel.Visible = false;
            _redCrimsonLevelLabel.Visible = false;
            return;
        }

        Visible = maxAmount > 0;
        float ratio = maxAmount > 0 ? (float)currentAmount / maxAmount : 0f;
        float maxWidth = _mask.Size.X > 0f ? _mask.Size.X : Size.X - 10f;
        _foreground.OffsetRight = maxWidth * ratio - maxWidth;
        _foreground.Visible = currentAmount > 0;
        _label.SetTextAutoSize($"{currentAmount}/{maxAmount}");
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
            Modulate = new Color(0.164706f, 0.376471f, 0.419608f, 0.752941f),
            Texture = LoadTexture(HealthBarBackgroundPath),
            PatchMarginLeft = 9,
            PatchMarginRight = 9,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_background, 1f, 0f, -1f, 0f);
        AddChild(_background);

        _foregroundContainer = new Control
        {
            Name = "DpForegroundContainer",
            ClipChildren = ClipChildrenMode.Only,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foregroundContainer, 5f, 3f, -5f, -3f);
        AddChild(_foregroundContainer);

        _mask = new NinePatchRect
        {
            Name = "DpMask",
            ClipChildren = ClipChildrenMode.Only,
            Texture = LoadTexture(HealthBarMaskPath),
            PatchMarginLeft = 6,
            PatchMarginRight = 6,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_mask);
        _foregroundContainer.AddChild(_mask);

        _foreground = new NinePatchRect
        {
            Name = "DpForeground",
            SelfModulate = DeflectorProtection.DpColor,
            Texture = LoadTexture(HealthBarFillPath),
            PatchMarginLeft = 6,
            PatchMarginRight = 6,
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(_foreground, 0f, -4f, 0f, 4f);
        _mask.AddChild(_foreground);

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
        SetFullRect(_label, 0f, -7f, 0f, 8f);
        _label.AddThemeColorOverride(ThemeConstants.Label.FontColor, new Color(1f, 0.964706f, 0.886275f));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color(0f, 0f, 0f, 0.25098f));
        _label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, _outlineColor);
        _label.AddThemeConstantOverride(ShadowOffsetX, 6);
        _label.AddThemeConstantOverride(ShadowOffsetY, 4);
        _label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 16);
        _label.AddThemeConstantOverride(ShadowOutlineSize, 0);
        _label.AddThemeFontOverride(ThemeConstants.Label.Font, LoadFont(HealthBarFontPath));
        _label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 24);
        AddChild(_label);

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
            _relic != null &&
            _rollHistory != null &&
            _creature?.Player != null &&
            _creature.IsAlive;
        _currentRollLabel.Visible = shouldShow;
        if (!shouldShow)
        {
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
        if (_hpBarContainer == null)
        {
            return;
        }

        Position = _hpBarContainer.Position + new Vector2(0f, VerticalOffsetFromHpBar);
        Size = _hpBarContainer.Size;
    }

    private static void SetFullRect(Control control, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = left;
        control.OffsetTop = top;
        control.OffsetRight = right;
        control.OffsetBottom = bottom;
    }

    private static Texture2D LoadTexture(string path)
    {
        return ResourceLoader.Load<Texture2D>(path);
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
