using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NMemoryGardenUpgradeScreen : Control, IOverlayScreen
{
    private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Button> _optionButtons = new();
    private IReadOnlyList<MemoryGardenUpgradeOption> _options = Array.Empty<MemoryGardenUpgradeOption>();
    private MemoryGardenUpgradeOption _selected;
    private string _cardTitle;
    private string _noticeKey = "upgradeNotice";
    private Panel _panel;
    private Label _title;
    private Label _cardName;
    private Label _subtitle;
    private Label _focusPrompt;
    private Label _notice;
    private ScrollContainer _scroll;
    private GridContainer _content;
    private VBoxContainer _optionList;
    private Panel _previewPanel;
    private Label _previewTitle;
    private Label _previewPrompt;
    private NCard _previewCard;
    private Button _confirm;
    private Tween _fade;
    private bool _completed;

    internal static IEnumerable<string> AssetPaths => NCard.AssetPaths
        .Concat(new[] { "res://images/packed/sprite_fonts/colorless_energy_icon.png" });

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => true;
    public Control DefaultFocusedControl => (Control)_focusPrompt ?? this;

    internal static Task<int> Choose(string cardTitle, IReadOnlyList<MemoryGardenUpgradeOption> options,
        string noticeKey = "upgradeNotice")
    {
        if (NOverlayStack.Instance == null || options == null || options.Count == 0)
            return Task.FromResult(-1);

        NMemoryGardenUpgradeScreen screen = new()
        {
            Name = "MemoryGardenUpgradeScreen",
            _cardTitle = cardTitle ?? string.Empty,
            _noticeKey = noticeKey,
            _options = options.Take(3).ToArray()
        };
        NOverlayStack.Instance.Push(screen);
        return screen._completion.Task;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutPanel();
        RefreshNavigation();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        // The options are already drawn for this upgrade. Going back
        // must not dismiss the committed selection or generate another offer.
        if (input.IsAction("ui_cancel") || input is InputEventKey { Keycode: Key.Escape })
            GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (IsVisibleInTree() && ActiveScreenContext.Instance.IsCurrent(this) && input is InputEventKey)
            GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        _fade?.Kill();
        CancelPendingSelection();
    }

    public void AfterOverlayOpened()
    {
        NHoverTipSet.Clear();
        Modulate = Colors.Transparent;
        _fade = CreateTween();
        _fade.TweenProperty(this, "modulate:a", 1f, 0.18)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void AfterOverlayClosed()
    {
        _fade?.Kill();
        CancelPendingSelection();
        NHoverTipSet.Clear();
        this.QueueFreeSafely();
    }

    public void AfterOverlayShown() => Visible = true;
    public void AfterOverlayHidden() => Visible = false;

    private void CancelPendingSelection()
    {
        if (_completed) return;
        _completed = true;
        // Unlike a confirmed effect, tearing down the overlay must cancel
        // the caller all the way out of an in-progress card acquisition.
        _completion.TrySetCanceled();
    }

    private void BuildUi()
    {
        ColorRect background = new()
        {
            Color = new Color(0.018f, 0.022f, 0.028f, 0.90f),
            MouseFilter = MouseFilterEnum.Stop
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _panel = new Panel { ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", Box(new Color("171922"), new Color("554354"), 14));
        AddChild(_panel);
        _title = MakeLabel(Text("upgradeTitle"), 30, new Color("F0D8E9"));
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _panel.AddChild(_title);
        _cardName = MakeLabel(_cardTitle, 20, new Color("F0D8E9"));
        _cardName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _panel.AddChild(_cardName);
        _subtitle = MakeLabel(Text("upgradeSubtitle"), 17, new Color("B9B6C1"));
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _panel.AddChild(_subtitle);

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.AddChild(_scroll);
        _content = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        _content.AddThemeConstantOverride("h_separation", 22);
        _content.AddThemeConstantOverride("v_separation", 18);
        _scroll.AddChild(_content);
        _optionList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _optionList.AddThemeConstantOverride("separation", 14);
        _content.AddChild(_optionList);

        // A neutral focus target keeps keyboard navigation available without
        // selecting an upgrade or opening a card tooltip on entry.
        _focusPrompt = MakeLabel(Text("upgradeChoosePrompt"), 20, new Color("F0D8E9"));
        _focusPrompt.FocusMode = FocusModeEnum.All;
        _focusPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _focusPrompt.CustomMinimumSize = new Vector2(0, 32);
        _optionList.AddChild(_focusPrompt);
        foreach (MemoryGardenUpgradeOption option in _options)
        {
            Button button = MakeButton(string.Empty, false);
            button.CustomMinimumSize = new Vector2(0, 132);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.ToggleMode = true;
            button.Pressed += () => Select(option);
            _optionList.AddChild(button);
            _optionButtons.Add(button);

            Label title = MakeLabel(option.Title, 22, new Color("F0D8E9"));
            title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            title.OffsetLeft = 18;
            title.OffsetRight = -18;
            title.OffsetTop = 12;
            title.OffsetBottom = 44;
            button.AddChild(title);
            MegaRichTextLabel description = new()
            {
                Text = HbrManualGlossaryPresentation.FormatDescription(option.Description, "cards:" + option.PreviewCard.Id.Entry),
                BbcodeEnabled = true, AutoSizeEnabled = false, FitContent = true, ScrollActive = false,
                SelectionEnabled = false, MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            description.AddThemeFontSizeOverride("normal_font_size", 18);
            description.AddThemeFontSizeOverride("bold_font_size", 18);
            description.AddThemeColorOverride("default_color", new Color("D4CBD7"));
            foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
                description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
            description.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            description.OffsetLeft = 18;
            description.OffsetRight = -18;
            description.OffsetTop = 49;
            description.OffsetBottom = -12;
            description.MinimumSizeChanged += () =>
                button.CustomMinimumSize = new Vector2(0, Math.Max(132f, description.GetMinimumSize().Y + 62f));
            button.AddChild(description);
        }

        _previewPanel = new Panel
        {
            ClipContents = true,
            CustomMinimumSize = new Vector2(0, 470),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _previewPanel.AddThemeStyleboxOverride("panel", Box(new Color("12141C"), new Color("3D3443"), 9));
        _previewPanel.Resized += LayoutPreview;
        _content.AddChild(_previewPanel);
        _previewTitle = MakeLabel(Text("upgradePreviewTitle"), 20, new Color("F0D8E9"));
        _previewTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _previewPanel.AddChild(_previewTitle);
        _previewPrompt = MakeLabel(Text("upgradePreviewPrompt"), 18, new Color("9D95A3"));
        _previewPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        _previewPrompt.VerticalAlignment = VerticalAlignment.Center;
        _previewPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _previewPanel.AddChild(_previewPrompt);

        _notice = MakeLabel(Text(_noticeKey), 15, new Color("9D95A3"));
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _panel.AddChild(_notice);
        _confirm = MakeButton(Text("upgradeConfirm"), true);
        _confirm.Disabled = true;
        _confirm.Pressed += Confirm;
        _panel.AddChild(_confirm);
    }

    private void LayoutPanel()
    {
        if (_panel == null || _confirm == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        Vector2 panelSize = new(Math.Max(1f, Math.Min(1100f, viewport.X - 32f)),
            Math.Max(1f, Math.Min(790f, viewport.Y - 48f)));
        _panel.Position = (viewport - panelSize) / 2f;
        _panel.Size = panelSize;
        bool narrow = panelSize.X < 760f;
        float inset = narrow ? 16f : 28f;
        float width = Math.Max(1f, panelSize.X - inset * 2f);
        float bodyTop = narrow ? 150f : 138f;
        float footerHeight = narrow ? 112f : 86f;
        float bodyHeight = Math.Max(1f, panelSize.Y - bodyTop - footerHeight);
        Place(_title, inset, 18f, width, 40f);
        Place(_cardName, inset, 63f, width, 30f);
        Place(_subtitle, inset, 98f, width, narrow ? 46f : 34f);
        Place(_scroll, inset, bodyTop, width, bodyHeight);
        _content.Columns = narrow ? 1 : 2;
        _previewPanel.CustomMinimumSize = new Vector2(0, narrow ? 410f : 470f);
        float buttonWidth = Math.Min(160f, width);
        float buttonY = panelSize.Y - 62f;
        Place(_confirm, panelSize.X - inset - buttonWidth, buttonY, buttonWidth, 42f);
        Place(_notice, inset, narrow ? buttonY - 42f : buttonY,
            narrow ? width : Math.Max(1f, width - buttonWidth - 24f), narrow ? 36f : 42f);
        LayoutPreview();
    }

    private void LayoutPreview()
    {
        if (_previewPanel == null || _previewTitle == null || _previewPrompt == null) return;
        Vector2 size = _previewPanel.Size;
        Place(_previewTitle, 12f, 16f, size.X - 24f, 32f);
        Place(_previewPrompt, 22f, 58f, size.X - 44f, size.Y - 80f);
        if (_previewCard == null) return;
        float scale = Math.Max(0.01f, Math.Min(1f,
            Math.Min((size.X - 50f) / NCard.defaultSize.X, (size.Y - 78f) / NCard.defaultSize.Y)));
        _previewCard.Scale = Vector2.One * scale;
        _previewCard.Position = new Vector2(size.X / 2f, 58f + (size.Y - 78f) / 2f);
    }

    private void Select(MemoryGardenUpgradeOption option)
    {
        if (_completed || !_options.Contains(option)) return;
        _selected = option;
        for (int index = 0; index < _optionButtons.Count; index++)
            _optionButtons[index].SetPressedNoSignal(ReferenceEquals(_options[index], option));
        NHoverTipSet.Clear();
        if (_previewCard == null)
        {
            _previewCard = NCard.Create(option.PreviewCard);
            if (_previewCard != null) _previewPanel.AddChild(_previewCard);
        }
        else
            _previewCard.Model = option.PreviewCard;
        _previewCard?.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        _previewPrompt.Visible = _previewCard == null;
        _confirm.Disabled = false;
        LayoutPreview();
        RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        if (_optionButtons.Count == 0) return;
        NodePath first = _optionButtons[0].GetPath();
        NodePath prompt = _focusPrompt.GetPath();
        NodePath footer = (_confirm.Disabled ? (Control)_focusPrompt : _confirm).GetPath();
        _focusPrompt.FocusNext = first;
        _focusPrompt.FocusNeighborBottom = first;
        _focusPrompt.FocusPrevious = footer;
        _confirm.FocusPrevious = _optionButtons[^1].GetPath();
        _confirm.FocusNeighborTop = _optionButtons[^1].GetPath();
        _confirm.FocusNext = first;
        _confirm.FocusNeighborBottom = first;
        for (int index = 0; index < _optionButtons.Count; index++)
        {
            Button button = _optionButtons[index];
            NodePath previous = index == 0 ? prompt : _optionButtons[index - 1].GetPath();
            NodePath next = index + 1 == _optionButtons.Count ? footer : _optionButtons[index + 1].GetPath();
            button.FocusPrevious = previous;
            button.FocusNeighborTop = previous;
            button.FocusNext = next;
            button.FocusNeighborBottom = next;
            button.FocusNeighborRight = footer;
        }
    }

    private void Confirm()
    {
        if (_completed || _selected == null) return;
        _completed = true;
        NHoverTipSet.Clear();
        NOverlayStack.Instance?.Remove(this);
        _completion.TrySetResult(_selected.EffectId);
    }

    private static string Text(string key) =>
        new LocString("rest_site_ui", "HBR_MEMORY_GARDEN." + key).GetFormattedText();

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(1f, width), Math.Max(1f, height));
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button MakeButton(string text, bool accent)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        Color edge = accent ? new Color("C879AF") : new Color("5E5568");
        button.AddThemeFontSizeOverride("font_size", 20);
        button.AddThemeColorOverride("font_color", new Color("F4EAF2"));
        button.AddThemeStyleboxOverride("normal", Box(accent ? new Color("54344F") : new Color("282430"), edge, 7));
        button.AddThemeStyleboxOverride("hover", Box(new Color("69465F"), new Color("E5ACD1"), 7));
        button.AddThemeStyleboxOverride("hover_pressed", Box(new Color("69465F"), new Color("E5ACD1"), 7));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("54344F"), new Color("D890BE"), 7));
        button.AddThemeStyleboxOverride("focus", Box(new Color(0f, 0f, 0f, 0f), new Color("EDD1E6"), 7));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("211F28"), new Color("3C3743"), 7));
        return button;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int radius) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
    };
}
