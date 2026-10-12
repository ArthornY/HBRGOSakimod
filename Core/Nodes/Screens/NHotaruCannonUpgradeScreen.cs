using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NHotaruCannonUpgradeScreen : Control, IOverlayScreen
{
    private readonly TaskCompletionSource<HotaruCannonKind?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<HotaruCannonUpgradeChoice> _choices = Array.Empty<HotaruCannonUpgradeChoice>();
    private readonly List<Button> _choiceButtons = new();
    private HotaruCannonUpgradeChoice _selected;
    private int _upgradePoints;
    private Panel _panel;
    private Label _title;
    private Label _subtitle;
    private Label _selectionTitle;
    private Label _currentTitle;
    private Label _nextTitle;
    private MegaRichTextLabel _status;
    private Label _notice;
    private ScrollContainer _currentScroll;
    private ScrollContainer _nextScroll;
    private MegaRichTextLabel _currentDescription;
    private MegaRichTextLabel _nextDescription;
    private Button _confirm;
    private Button _cancel;
    private Tween _fade;
    private bool _completed;

    internal static IEnumerable<string> AssetPaths => new[]
    {
        "res://images/characters/hbrgo_hotaru/cannons/happiness.png",
        "res://images/characters/hbrgo_hotaru/cannons/coffin.png",
        "res://images/characters/hbrgo_hotaru/cannons/contest.png",
        "res://images/characters/hbrgo_hotaru/cannons/longing.png",
        "res://images/characters/hbrgo_hotaru/cannons/absolution.png"
    };

    public NetScreenType ScreenType => NetScreenType.CardSelection;
    public bool UseSharedBackstop => true;
    public Control DefaultFocusedControl => (Control)_cancel ?? this;

    internal static Task<HotaruCannonKind?> Choose(
        IReadOnlyList<HotaruCannonUpgradeChoice> choices, int upgradePoints)
    {
        if (NOverlayStack.Instance == null || choices == null || choices.Count == 0)
            return Task.FromResult<HotaruCannonKind?>(null);
        NHotaruCannonUpgradeScreen screen = new()
        {
            Name = "HotaruCannonUpgradeScreen",
            _choices = choices.ToArray(),
            _upgradePoints = Math.Max(0, upgradePoints)
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
        RefreshSelection();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (input.IsActionPressed("ui_cancel") ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Complete(null);
        }
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
        _panel.AddThemeStyleboxOverride("panel", Box(new Color("171922"), new Color("554354")));
        AddChild(_panel);
        _title = Label(Text("title"), 30, new Color("F0D8E9"));
        LocString subtitle = new("rest_site_ui", "HOTARU_CANNON_UPGRADE.subtitle");
        subtitle.Add("points", _upgradePoints);
        _subtitle = Label(subtitle.GetFormattedText(), 17, new Color("B9B6C1"));
        _panel.AddChild(_title);
        _panel.AddChild(_subtitle);

        foreach (HotaruCannonUpgradeChoice choice in _choices)
        {
            Button button = new()
            {
                Text = choice.Name,
                ToggleMode = true,
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                Alignment = HorizontalAlignment.Center
            };
            button.AddThemeFontSizeOverride("font_size", 16);
            button.AddThemeConstantOverride("outline_size", 1);
            button.AddThemeColorOverride("font_color", choice.IsAvailable ? new Color("F0D8E9") : new Color("9D9BA6"));
            button.AddThemeStyleboxOverride("normal", Box(new Color("24232F"), new Color("554354")));
            button.AddThemeStyleboxOverride("pressed", Box(new Color("4B3551"), new Color("C48CAF")));
            button.AddThemeStyleboxOverride("hover", Box(new Color("363040"), new Color("9E718E")));
            TextureRect icon = new()
            {
                Texture = PreloadManager.Cache.GetTexture2D(choice.IconPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = choice.IsAvailable ? Colors.White : new Color(1f, 1f, 1f, 0.55f)
            };
            button.AddChild(icon);
            // Put the name below the image while retaining a single native
            // focus/click target, including choices currently unavailable.
            button.AddThemeConstantOverride("v_separation", 0);
            Label name = Label(choice.Name, 16, choice.IsAvailable ? new Color("F0D8E9") : new Color("9D9BA6"));
            name.HorizontalAlignment = HorizontalAlignment.Center;
            button.Text = string.Empty;
            button.AddChild(name);
            button.Pressed += () => Select(choice);
            _panel.AddChild(button);
            _choiceButtons.Add(button);
        }

        _selectionTitle = Label(Text("choosePrompt"), 21, new Color("F0D8E9"));
        _currentTitle = Label(Text("currentEffect"), 18, new Color("C4BBCB"));
        _nextTitle = Label(Text("nextEffect"), 18, new Color("E3B0CE"));
        _status = RichText(17, new Color("D7B8CB"));
        _notice = Label(Text(_choices.Any(choice => choice.IsAvailable) ? "notice" : "noAvailable"), 16, new Color("AFAAB8"));
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        foreach (Control label in new Control[] { _selectionTitle, _currentTitle, _nextTitle, _status, _notice })
            _panel.AddChild(label);
        (_currentScroll, _currentDescription) = Description();
        (_nextScroll, _nextDescription) = Description();
        _panel.AddChild(_currentScroll);
        _panel.AddChild(_nextScroll);
        _confirm = Button(Text("confirm"), true);
        _cancel = Button(Text("cancel"), false);
        _confirm.Pressed += Confirm;
        _cancel.Pressed += () => Complete(null);
        _panel.AddChild(_confirm);
        _panel.AddChild(_cancel);
        RefreshNavigation();
    }

    private void LayoutPanel()
    {
        if (_panel == null) return;
        Vector2 viewport = GetViewportRect().Size;
        Vector2 size = new(Mathf.Max(320f, Mathf.Min(1180f, viewport.X - 64f)),
            Mathf.Max(300f, Mathf.Min(790f, viewport.Y - 48f)));
        _panel.Size = size;
        _panel.Position = (viewport - size) * 0.5f;
        float inset = 24f, width = size.X - inset * 2f;
        bool compact = size.Y < 600f;
        Place(_title, inset, compact ? 10f : 18f, width, compact ? 32f : 40f);
        Place(_subtitle, inset, compact ? 44f : 62f, width, compact ? 26f : 34f);
        float gap = 10f, tileWidth = (width - gap * (_choiceButtons.Count - 1)) / _choiceButtons.Count;
        float tileY = compact ? 76f : 104f;
        float tileHeight = compact ? Mathf.Clamp(size.Y * 0.18f, 48f, 106f) : Mathf.Clamp(size.Y * 0.21f, 100f, 160f);
        for (int index = 0; index < _choiceButtons.Count; index++)
        {
            Button button = _choiceButtons[index];
            Place(button, inset + index * (tileWidth + gap), tileY, tileWidth, tileHeight);
            Place(button.GetChild<TextureRect>(0), 6f, 6f, tileWidth - 12f, tileHeight - 40f);
            Place(button.GetChild<Label>(1), 4f, tileHeight - 32f, tileWidth - 8f, 24f);
        }
        float headingY = tileY + tileHeight + 8f, previewY = headingY + (compact ? 52f : 62f);
        float columnWidth = (width - 18f) * 0.5f, footerY = size.Y - 58f;
        Place(_selectionTitle, inset, headingY, width, compact ? 24f : 30f);
        Place(_currentTitle, inset, headingY + (compact ? 26f : 34f), columnWidth, 24f);
        Place(_nextTitle, inset + columnWidth + 18f, headingY + (compact ? 26f : 34f), columnWidth, 24f);
        // Reserve the footer first. Short viewports shrink the scrollable
        // previews rather than letting their minimum height cover the buttons.
        float noticeY = footerY - 34f, statusY = noticeY - 38f;
        float previewHeight = Mathf.Max(1f, statusY - previewY - 8f);
        Place(_currentScroll, inset, previewY, columnWidth, previewHeight);
        Place(_nextScroll, inset + columnWidth + 18f, previewY, columnWidth, previewHeight);
        _currentDescription.CustomMinimumSize = new Vector2(Mathf.Max(1f, columnWidth - 14f), 0f);
        _nextDescription.CustomMinimumSize = new Vector2(Mathf.Max(1f, columnWidth - 14f), 0f);
        Place(_status, inset, statusY, width, 30f);
        Place(_notice, inset, noticeY, width, 28f);
        Place(_cancel, size.X - inset - 150f, footerY, 150f, 42f);
        Place(_confirm, size.X - inset - 312f, footerY, 150f, 42f);
    }

    private void Select(HotaruCannonUpgradeChoice choice)
    {
        if (_completed || !_choices.Contains(choice)) return;
        _selected = choice;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        if (_confirm == null) return;
        _confirm.Disabled = _selected?.IsAvailable != true;
        _selectionTitle.Text = _selected?.Name ?? Text("choosePrompt");
        _currentDescription.Text = FormatDescription(_selected?.CurrentDescription);
        _nextDescription.Text = FormatDescription(_selected?.UpgradeDescription);
        _status.Text = FormatDescription(_selected?.StatusText);
        for (int index = 0; index < _choiceButtons.Count; index++)
            _choiceButtons[index].SetPressedNoSignal(ReferenceEquals(_choices[index], _selected));
        RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        if (_confirm == null || _choiceButtons.Count == 0) return;
        NodePath first = _choiceButtons[0].GetPath();
        NodePath last = _choiceButtons[^1].GetPath();
        NodePath footer = (_confirm.Disabled ? _cancel : _confirm).GetPath();
        for (int index = 0; index < _choiceButtons.Count; index++)
        {
            Button button = _choiceButtons[index];
            button.FocusPrevious = index == 0 ? _cancel.GetPath() : _choiceButtons[index - 1].GetPath();
            button.FocusNext = index == _choiceButtons.Count - 1 ? footer : _choiceButtons[index + 1].GetPath();
            button.FocusNeighborLeft = index == 0 ? last : _choiceButtons[index - 1].GetPath();
            button.FocusNeighborRight = index == _choiceButtons.Count - 1 ? first : _choiceButtons[index + 1].GetPath();
            button.FocusNeighborBottom = footer;
        }
        _confirm.FocusPrevious = last;
        _confirm.FocusNext = _cancel.GetPath();
        _confirm.FocusNeighborTop = last;
        _confirm.FocusNeighborRight = _cancel.GetPath();
        _cancel.FocusPrevious = footer == _cancel.GetPath() ? last : _confirm.GetPath();
        _cancel.FocusNext = first;
        _cancel.FocusNeighborTop = last;
        _cancel.FocusNeighborLeft = footer == _cancel.GetPath() ? last : _confirm.GetPath();
    }

    private void Confirm()
    {
        if (_selected?.IsAvailable == true) Complete(_selected.Kind);
    }

    private void Complete(HotaruCannonKind? kind)
    {
        if (_completed) return;
        _completed = true;
        NHoverTipSet.Clear();
        NOverlayStack.Instance?.Remove(this);
        _completion.TrySetResult(kind);
    }

    private static string Text(string key) =>
        new LocString("rest_site_ui", "HOTARU_CANNON_UPGRADE." + key).GetFormattedText();

    private static Label Label(string text, int fontSize, Color color)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Button Button(string text, bool primary)
    {
        Button button = new() { Text = text, FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.AddThemeStyleboxOverride("normal", Box(new Color(primary ? "56384F" : "292733"), new Color("92738A")));
        return button;
    }

    private (ScrollContainer Scroll, MegaRichTextLabel Text) Description()
    {
        ScrollContainer scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, MouseFilter = MouseFilterEnum.Stop };
        MegaRichTextLabel text = RichText(18, new Color("DFD9E3"));
        scroll.AddChild(text);
        return (scroll, text);
    }

    private static string FormatDescription(string text) => string.IsNullOrEmpty(text)
        ? string.Empty : HbrManualGlossaryPresentation.FormatDescription(text, "hotaru_cannon");

    private MegaRichTextLabel RichText(int fontSize, Color color)
    {
        MegaRichTextLabel text = new()
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            AutoSizeEnabled = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        text.AddThemeFontSizeOverride("normal_font_size", fontSize);
        text.AddThemeFontSizeOverride("bold_font_size", fontSize);
        text.AddThemeColorOverride("default_color", color);
        // MegaRichTextLabel checks these overrides in _Ready, even when auto
        // sizing is disabled. Supply them before attaching any of the views.
        foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
            text.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
        return text;
    }

    private static StyleBoxFlat Box(Color background, Color border) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8
    };

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }
}
