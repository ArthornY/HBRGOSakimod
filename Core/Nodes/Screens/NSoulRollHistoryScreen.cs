using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NSoulRollHistoryScreen : Control, IOverlayScreen
{
    private static NSoulRollHistoryScreen _activeScreen;

    private SoulOfAnchor _soulOfAnchor;
    private PanelContainer _panel;
    private Label _titleLabel;
    private Label _summaryLabel;
    private VBoxContainer _historyList;
    private ScrollContainer _scroll;
    private Label _pageLabel;
    private Button _newestButton;
    private Button _previousButton;
    private Button _nextButton;
    private Button _oldestButton;
    private Button _closeButton;
    private Tween _fadeTween;
    private int _historyCount;
    private int _pageIndex;

    public NetScreenType ScreenType => NetScreenType.Room;

    public bool UseSharedBackstop => true;

    public Control DefaultFocusedControl => (Control)_closeButton ?? this;

    public static void Show(SoulOfAnchor soulOfAnchor)
    {
        if (soulOfAnchor == null ||
            AmnesiaRollVisibilityHelper.IsRollHidden(soulOfAnchor.Owner))
        {
            return;
        }

        NOverlayStack overlayStack = NOverlayStack.Instance;
        if (overlayStack == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_activeScreen) &&
            !_activeScreen.IsQueuedForDeletion() &&
            _activeScreen.IsInsideTree())
        {
            _activeScreen.Refresh(soulOfAnchor);
            return;
        }

        NSoulRollHistoryScreen screen = new()
        {
            Name = "SoulRollHistoryScreen",
            _soulOfAnchor = soulOfAnchor
        };
        _activeScreen = screen;
        overlayStack.Push(screen);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetFullRect(this);
        BuildUi();
        LayoutPanel();
        Refresh(_soulOfAnchor);
    }

    public override void _ExitTree()
    {
        BindHistory(null);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutPanel();
        }
    }

    public void AfterOverlayOpened()
    {
        Modulate = Colors.Transparent;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2);
    }

    public void AfterOverlayClosed()
    {
        _fadeTween?.Kill();
        BindHistory(null);
        if (ReferenceEquals(_activeScreen, this))
        {
            _activeScreen = null;
        }

        this.QueueFreeSafely();
    }

    public void AfterOverlayShown()
    {
        Visible = true;
    }

    public void AfterOverlayHidden()
    {
        Visible = false;
    }

    private void BuildUi()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.015f, 0.02f, 0.035f, 0.82f),
            MouseFilter = MouseFilterEnum.Stop
        };
        SetFullRect(backdrop);
        AddChild(backdrop);

        _panel = new PanelContainer
        {
            Name = "SoulRollHistoryPanel",
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.AddThemeStyleboxOverride(
            "panel",
            CreatePanelStyle(
                new Color(0.055f, 0.07f, 0.10f, 0.98f),
                new Color(0.38f, 0.72f, 0.92f, 0.95f)));
        AddChild(_panel);

        VBoxContainer layout = new()
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 28f,
            OffsetTop = 24f,
            OffsetRight = -28f,
            OffsetBottom = -24f
        };
        layout.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(layout);

        _titleLabel = CreateLabel(string.Empty, 30, Colors.White);
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(_titleLabel);

        _summaryLabel = CreateLabel(string.Empty, 19, new Color(0.72f, 0.82f, 0.90f));
        _summaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(_summaryLabel);

        layout.AddChild(new HSeparator());

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        layout.AddChild(_scroll);

        _historyList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _historyList.AddThemeConstantOverride("separation", 8);
        _scroll.AddChild(_historyList);

        layout.AddChild(new HSeparator());

        HBoxContainer navigation = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        navigation.AddThemeConstantOverride("separation", 8);
        layout.AddChild(navigation);
        _newestButton = CreatePageButton(navigation, "最新页", () => RenderPage(0));
        _previousButton = CreatePageButton(navigation, "上一页", () => RenderPage(_pageIndex - 1));
        _pageLabel = CreateLabel(string.Empty, 19, new Color(0.72f, 0.82f, 0.90f));
        _pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _pageLabel.VerticalAlignment = VerticalAlignment.Center;
        _pageLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        navigation.AddChild(_pageLabel);
        _nextButton = CreatePageButton(navigation, "下一页", () => RenderPage(_pageIndex + 1));
        _oldestButton = CreatePageButton(navigation, "最早页", () => RenderPage(int.MaxValue));
        CreatePageButton(navigation, "刷新", () => Refresh(_soulOfAnchor));

        _closeButton = new Button
        {
            Text = "关闭",
            CustomMinimumSize = new Vector2(150f, 48f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.All
        };
        _closeButton.AddThemeFontSizeOverride("font_size", 21);
        _closeButton.Pressed += Close;
        layout.AddChild(_closeButton);
    }

    private void Refresh(SoulOfAnchor soulOfAnchor)
    {
        BindHistory(soulOfAnchor);
        _historyCount = _soulOfAnchor?.RollHistory.Count ?? 0;
        RenderPage(0);
    }

    private void BindHistory(SoulOfAnchor soulOfAnchor)
    {
        if (_soulOfAnchor != null)
        {
            _soulOfAnchor.HistoryChanged -= OnHistoryChanged;
        }

        _soulOfAnchor = soulOfAnchor;
        if (_soulOfAnchor != null)
        {
            _soulOfAnchor.HistoryChanged += OnHistoryChanged;
        }
    }

    private void OnHistoryChanged()
    {
        // Appending rolls keeps the current snapshot. A clear must invalidate it
        // immediately, before a new battle can refill the same list.
        int currentCount = _soulOfAnchor?.RollHistory.Count ?? 0;
        if (currentCount < _historyCount)
        {
            _historyCount = currentCount;
            RenderPage(0);
        }
    }

    private void RenderPage(int requestedPage)
    {
        if (_historyList == null || _summaryLabel == null || _titleLabel == null)
        {
            return;
        }

        _titleLabel.Text = "出目历史";

        // Appending rolls must not shift pages already being read. A refresh
        // captures the new count; clearing a battle's history resets this view.
        int currentCount = _soulOfAnchor?.RollHistory.Count ?? 0;
        if (currentCount < _historyCount)
        {
            _historyCount = currentCount;
            requestedPage = 0;
        }
        SoulRollHistoryPage page = SoulRollHistoryPage.Create(_historyCount, requestedPage);
        _pageIndex = page.PageIndex;
        bool isNewest = page.PageIndex == 0;
        bool isOldest = page.PageIndex == page.PageCount - 1;
        if (isNewest && (_newestButton.HasFocus() || _previousButton.HasFocus()) ||
            isOldest && (_nextButton.HasFocus() || _oldestButton.HasFocus()))
            _closeButton.GrabFocus();
        _newestButton.Disabled = _previousButton.Disabled = isNewest;
        _nextButton.Disabled = _oldestButton.Disabled = isOldest;
        _pageLabel.Text = $"第 {page.PageIndex + 1} / {page.PageCount} 页";

        foreach (Node child in _historyList.GetChildren())
        {
            _historyList.RemoveChild(child);
            child.QueueFree();
        }

        _scroll.ScrollVertical = 0;
        _summaryLabel.Text =
            $"本场战斗共 {_historyCount} 次，按新到旧排列，每页 {SoulRollHistoryPage.EntriesPerPage} 条";
        if (page.EntryCount == 0)
        {
            Label emptyLabel = CreateLabel("尚无出目记录。", 22, new Color(0.68f, 0.72f, 0.78f));
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.CustomMinimumSize = new Vector2(0f, 120f);
            emptyLabel.VerticalAlignment = VerticalAlignment.Center;
            _historyList.AddChild(emptyLabel);
            return;
        }

        for (int offset = 0; offset < page.EntryCount; offset++)
        {
            SoulRollHistoryEntry entry = _soulOfAnchor.RollHistory[page.NewestIndex - offset];
            AddHistoryEntry(entry);
        }
    }

    private static Button CreatePageButton(Control parent, string text, Action action)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(78f, 42f),
            FocusMode = FocusModeEnum.All
        };
        button.AddThemeFontSizeOverride("font_size", 19);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private void AddHistoryEntry(SoulRollHistoryEntry entry)
    {
        HBoxContainer row = new()
        {
            CustomMinimumSize = new Vector2(0f, 64f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 18);

        CenterContainer rollContainer = new()
        {
            CustomMinimumSize = new Vector2(
                entry.DiscardedRollDetails.Count > 0 ? 570f : 250f,
                64f)
        };
        HBoxContainer rollExpressions = new()
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        rollExpressions.AddThemeConstantOverride("separation", 14);

        foreach (SoulRollBreakdown discardedRoll in entry.DiscardedRollDetails)
        {
            rollExpressions.AddChild(CreateRollLabel(
                discardedRoll,
                entry.Source,
                isDiscarded: true));
        }

        rollExpressions.AddChild(CreateRollLabel(
            entry.WinningRoll,
            entry.Source,
            isDiscarded: false));
        rollContainer.AddChild(rollExpressions);
        row.AddChild(rollContainer);

        Label sourceLabel = CreateLabel($"来源：{entry.Source}", 21, Colors.White);
        sourceLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sourceLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(sourceLabel);

        _historyList.AddChild(row);
        _historyList.AddChild(new HSeparator());
    }

    private RichTextLabel CreateRollLabel(
        SoulRollBreakdown roll,
        string source,
        bool isDiscarded)
    {
        IReadOnlyList<RollExpressionPart> parts = BuildRollParts(roll, source);
        string expression = string.Concat(parts.Select(part => part.Text));
        RichTextLabel label = new()
        {
            BbcodeEnabled = true,
            HintUnderlined = false,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(
                Mathf.Max(170f, expression.Length * 17f),
                42f),
            MouseFilter = MouseFilterEnum.Pass
        };
        label.AddThemeFontSizeOverride(
            ThemeConstants.RichTextLabel.NormalFontSize,
            26);
        label.AddThemeColorOverride(
            ThemeConstants.RichTextLabel.DefaultColor,
            isDiscarded
                ? new Color(0.54f, 0.56f, 0.60f)
                : GetRollColor(
                    RollKeywordRules.GetRollTier(
                        _soulOfAnchor?.Owner,
                        roll)));
        label.PushParagraph(HorizontalAlignment.Center);
        if (isDiscarded) label.PushStrikethrough();
        foreach (RollExpressionPart part in parts)
        {
            if (part.Tooltip != null) label.PushHint(part.Tooltip);
            label.AddText(part.Text);
            if (part.Tooltip != null) label.Pop();
        }
        if (isDiscarded) label.Pop();
        label.Pop();
        return label;
    }

    private readonly record struct RollExpressionPart(string Text, string Tooltip = null);

    private static IReadOnlyList<RollExpressionPart> BuildRollParts(SoulRollBreakdown roll, string source)
    {
        long bonus = (long)roll.FinalRoll - roll.BaseRoll;
        if (bonus == 0)
        {
            return new[] { new RollExpressionPart(roll.FinalRoll.ToString()) };
        }

        string diceSource = $"骰子来源：{roll.DiceSource}";
        if (!string.IsNullOrWhiteSpace(source)) diceSource += $"\n触发来源：{source}";
        return new[]
        {
            new RollExpressionPart(roll.BaseRoll.ToString(), diceSource),
            new RollExpressionPart(bonus > 0 ? " + " : " - "),
            new RollExpressionPart(Math.Abs(bonus).ToString(), BuildBonusTooltip(roll, bonus)),
            new RollExpressionPart(" = "),
            new RollExpressionPart(roll.FinalRoll.ToString())
        };
    }

    private static string BuildBonusTooltip(SoulRollBreakdown roll, long actualBonus)
    {
        if (roll.Bonuses.Count == 0) return "加成来源未记录。";
        string text = "加成来源：\n" + string.Join("\n", roll.Bonuses.Select(bonus =>
            $"{bonus.Source}：{bonus.Amount:+#;-#;0}"));
        long recordedBonus = roll.Bonuses.Sum(bonus => (long)bonus.Amount);
        if (recordedBonus != actualBonus)
            text += $"\n已记录合计：{recordedBonus:+#;-#;0}\n实际加成：{actualBonus:+#;-#;0}";
        return text;
    }

    private void LayoutPanel()
    {
        if (_panel == null || Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        float panelWidth = Mathf.Min(1180f, Mathf.Max(760f, Size.X * 0.72f));
        float panelHeight = Mathf.Min(820f, Mathf.Max(460f, Size.Y * 0.78f));
        panelWidth = Mathf.Min(panelWidth, Size.X - 40f);
        panelHeight = Mathf.Min(panelHeight, Size.Y - 40f);
        _panel.Size = new Vector2(panelWidth, panelHeight);
        _panel.Position = (Size - _panel.Size) * 0.5f;
    }

    private void Close()
    {
        NOverlayStack.Instance?.Remove(this);
    }

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        Label label = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, fontSize);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, color);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, Colors.Black);
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 5);
        return label;
    }

    private static Color GetRollColor(SoulRollTier tier)
    {
        if (tier == SoulRollTier.Critical)
        {
            return new Color(1f, 0.82f, 0.28f);
        }

        if (tier == SoulRollTier.Third)
        {
            return new Color(0.92f, 0.55f, 1f);
        }

        if (tier == SoulRollTier.Second)
        {
            return new Color(0.40f, 0.86f, 1f);
        }

        if (tier == SoulRollTier.First)
        {
            return new Color(0.55f, 0.92f, 0.65f);
        }

        return Colors.White;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            ContentMarginBottom = 18,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 18,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
    }

    private static void SetFullRect(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }
}
