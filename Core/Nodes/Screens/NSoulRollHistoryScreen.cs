using System;
using Godot;
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
    private Label _summaryLabel;
    private VBoxContainer _historyList;
    private Button _closeButton;
    private Tween _fadeTween;

    public NetScreenType ScreenType => NetScreenType.Room;

    public bool UseSharedBackstop => true;

    public Control DefaultFocusedControl => (Control)_closeButton ?? this;

    public static void Show(SoulOfAnchor soulOfAnchor)
    {
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

        Label title = CreateLabel("安科之魂 · 出目记录", 30, Colors.White);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(title);

        _summaryLabel = CreateLabel(string.Empty, 19, new Color(0.72f, 0.82f, 0.90f));
        _summaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(_summaryLabel);

        layout.AddChild(new HSeparator());

        ScrollContainer scroll = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        layout.AddChild(scroll);

        _historyList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _historyList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_historyList);

        layout.AddChild(new HSeparator());

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
        _soulOfAnchor = soulOfAnchor;
        if (_historyList == null || _summaryLabel == null)
        {
            return;
        }

        foreach (Node child in _historyList.GetChildren())
        {
            _historyList.RemoveChild(child);
            child.QueueFree();
        }

        int count = _soulOfAnchor?.RollHistory.Count ?? 0;
        _summaryLabel.Text = $"本场战斗共 {count} 次，按新到旧排列；划线数字为被淘汰的出目";
        if (count == 0)
        {
            Label emptyLabel = CreateLabel("尚无出目记录。", 22, new Color(0.68f, 0.72f, 0.78f));
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.CustomMinimumSize = new Vector2(0f, 120f);
            emptyLabel.VerticalAlignment = VerticalAlignment.Center;
            _historyList.AddChild(emptyLabel);
            return;
        }

        for (int index = count - 1; index >= 0; index--)
        {
            SoulRollHistoryEntry entry = _soulOfAnchor.RollHistory[index];
            AddHistoryEntry(entry);
        }
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
            CustomMinimumSize = new Vector2(entry.DiscardedRoll.HasValue ? 180f : 110f, 64f)
        };
        RichTextLabel rollLabel = new()
        {
            BbcodeEnabled = true,
            Text = BuildRollText(entry),
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(entry.DiscardedRoll.HasValue ? 170f : 100f, 42f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        rollLabel.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.NormalFontSize, 30);
        rollLabel.AddThemeColorOverride(
            ThemeConstants.RichTextLabel.DefaultColor,
            GetRollColor(entry.Roll));
        rollContainer.AddChild(rollLabel);
        row.AddChild(rollContainer);

        Label sourceLabel = CreateLabel($"来源：{entry.Source}", 21, Colors.White);
        sourceLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sourceLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(sourceLabel);

        _historyList.AddChild(row);
        _historyList.AddChild(new HSeparator());
    }

    private static string BuildRollText(SoulRollHistoryEntry entry)
    {
        string discarded = entry.DiscardedRoll.HasValue
            ? $"[color=#8a9099][s]{entry.DiscardedRoll.Value}[/s][/color]  "
            : string.Empty;
        return $"[center]{discarded}{entry.Roll}[/center]";
    }

    private void LayoutPanel()
    {
        if (_panel == null || Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        float panelWidth = Mathf.Min(820f, Mathf.Max(520f, Size.X * 0.58f));
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

    private static Color GetRollColor(int roll)
    {
        if (roll == 100)
        {
            return new Color(1f, 0.82f, 0.28f);
        }

        if (roll >= 91)
        {
            return new Color(0.92f, 0.55f, 1f);
        }

        if (roll >= 76)
        {
            return new Color(0.40f, 0.86f, 1f);
        }

        if (roll >= 50)
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
