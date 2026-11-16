using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Cards;
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

// Memory sublimation in the HBR style (ui-concepts/memory-garden-20261104): the effects as glass rows on the left,
// the upgraded card on a glass panel at the right. Confirming breaks the card like glass before the screen closes.
public partial class NMemoryGardenUpgradeScreen : Control, IOverlayScreen
{
    private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Button> _optionButtons = new();
    private readonly List<TextureRect> _optionIcons = new();
    private IReadOnlyList<MemoryGardenUpgradeOption> _options = Array.Empty<MemoryGardenUpgradeOption>();
    private MemoryGardenUpgradeOption _selected;
    private string _cardTitle;
    private string _noticeKey = "upgradeNotice";
    private Control _rig;
    private Label _title;
    private Label _titleEn;
    private NinePatchRect _titleLine;
    private Label _subtitle;
    private Label _focusPrompt;
    private Panel _footer;
    private Label _notice;
    private ScrollContainer _scroll;
    private VBoxContainer _optionList;
    private Panel _previewPanel;
    private Label _previewTitle;
    private Label _previewPrompt;
    private NCard _previewCard;
    private Button _confirm;
    private Tween _fade;
    private bool _completed;
    private bool _sublimating;

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
        // The previews are already upgraded; their names read as before the upgrade until it is confirmed.
        foreach (MemoryGardenUpgradeOption option in _options)
            HbrMemoryTitleState.Hold(option.PreviewCard, option.PreviewCard.CurrentUpgradeLevel - 1);
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
        NMemoryGardenBackdrop backdrop = new();
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rig = new Control { MouseFilter = MouseFilterEnum.Pass, Size = HbrMemoryGardenTheme.Rig };
        AddChild(_rig);

        _title = HbrMemoryGardenTheme.Label(Text("upgradeTitle"), 48, HbrMemoryGardenTheme.Ink);
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _rig.AddChild(_title);
        _titleEn = HbrMemoryGardenTheme.Label("MEMORY GARDEN", 20, HbrMemoryGardenTheme.Ink2);
        _rig.AddChild(_titleEn);
        _titleLine = new NinePatchRect
        {
            Texture = HbrMemoryGardenTheme.Sprite("MordaTitleLineEdge"),
            PatchMarginLeft = 2, PatchMarginRight = 55,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _rig.AddChild(_titleLine);
        _subtitle = HbrMemoryGardenTheme.Label($"「{_cardTitle}」 · {Text("upgradeSubtitle")}", 21, HbrMemoryGardenTheme.Ink2);
        _subtitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _rig.AddChild(_subtitle);

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
            MouseFilter = MouseFilterEnum.Stop
        };
        _rig.AddChild(_scroll);
        _optionList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _optionList.AddThemeConstantOverride("separation", 14);
        _scroll.AddChild(_optionList);

        // A neutral focus target keeps keyboard navigation available without
        // selecting an upgrade or opening a card tooltip on entry.
        _focusPrompt = HbrMemoryGardenTheme.Label(Text("upgradeChoosePrompt"), 24, HbrMemoryGardenTheme.PinkInk);
        _focusPrompt.FocusMode = FocusModeEnum.All;
        _focusPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _focusPrompt.CustomMinimumSize = new Vector2(0, 40);
        _optionList.AddChild(_focusPrompt);
        foreach (MemoryGardenUpgradeOption option in _options)
        {
            Button button = OptionButton();
            button.CustomMinimumSize = new Vector2(0, 120);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.ToggleMode = true;
            button.Pressed += () => Select(option);
            _optionList.AddChild(button);
            _optionButtons.Add(button);

            TextureRect icon = new()
            {
                Texture = HbrMemoryGardenTheme.Sprite("IconBreakLimitBatchActive"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, .35f)
            };
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft);
            icon.OffsetLeft = 20;
            icon.OffsetRight = 68;
            icon.OffsetTop = -24;
            icon.OffsetBottom = 24;
            button.AddChild(icon);
            _optionIcons.Add(icon);
            Label title = HbrMemoryGardenTheme.Label(option.Title, 25, HbrMemoryGardenTheme.Ink);
            title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            title.OffsetLeft = 84;
            title.OffsetRight = -24;
            title.OffsetTop = 16;
            title.OffsetBottom = 50;
            button.AddChild(title);
            MegaRichTextLabel description = new()
            {
                Text = HbrManualGlossaryPresentation.FormatDescription(option.Description, "cards:" + option.PreviewCard.Id.Entry),
                BbcodeEnabled = true, AutoSizeEnabled = false, FitContent = true, ScrollActive = false,
                SelectionEnabled = false, MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            description.AddThemeFontSizeOverride("normal_font_size", 19);
            description.AddThemeFontSizeOverride("bold_font_size", 19);
            description.AddThemeColorOverride("default_color", HbrMemoryGardenTheme.Ink2);
            foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
                description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
            description.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            description.OffsetLeft = 84;
            description.OffsetRight = -24;
            description.OffsetTop = 54;
            description.OffsetBottom = -16;
            description.MinimumSizeChanged += () =>
                button.CustomMinimumSize = new Vector2(0, Math.Max(120f, description.GetMinimumSize().Y + 70f));
            button.AddChild(description);
        }

        _previewPanel = new Panel { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        _previewPanel.AddThemeStyleboxOverride("panel", HbrMemoryGardenTheme.GlassBox(22));
        _previewPanel.Resized += LayoutPreview;
        _rig.AddChild(_previewPanel);
        _previewTitle = HbrMemoryGardenTheme.Label(Text("upgradePreviewTitle"), 28, HbrMemoryGardenTheme.Gold);
        _previewTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _previewPanel.AddChild(_previewTitle);
        _previewPrompt = HbrMemoryGardenTheme.Label(Text("upgradePreviewPrompt"), 22, HbrMemoryGardenTheme.Ink2);
        _previewPrompt.HorizontalAlignment = HorizontalAlignment.Center;
        _previewPrompt.VerticalAlignment = VerticalAlignment.Center;
        _previewPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _previewPanel.AddChild(_previewPrompt);

        _footer = new Panel { MouseFilter = MouseFilterEnum.Pass };
        _footer.AddThemeStyleboxOverride("panel", HbrMemoryGardenTheme.GlassBox(22));
        _rig.AddChild(_footer);
        _notice = HbrMemoryGardenTheme.Label(Text(_noticeKey), 20, HbrMemoryGardenTheme.Ink2);
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _footer.AddChild(_notice);
        _confirm = HbrMemoryGardenTheme.HbrButton(Text("upgradeConfirm"));
        _confirm.Disabled = true;
        _confirm.Pressed += Confirm;
        _footer.AddChild(_confirm);
    }

    // Everything sits on the preview's 1920x1080 rig, centred in the screen.
    private void LayoutPanel()
    {
        if (_rig == null || _confirm == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        _rig.Position = HbrMemoryGardenTheme.Origin(viewport);
        _rig.Size = HbrMemoryGardenTheme.Rig;
        Place(_title, 168, 92, 1500, 64);
        Place(_titleEn, 172, 158, 700, 28);
        Place(_titleLine, 168, 190, 720, 10);
        Place(_subtitle, 170, 210, 1580, 34);
        Place(_scroll, 96, 262, 1000, 640);
        Place(_previewPanel, 1130, 262, 700, 640);
        Place(_footer, 60, 932, 1800, 136);
        Place(_notice, 28, 30, 1300, 76);
        Place(_confirm, 1800 - 30 - 330, 2, 330, 132);
        LayoutPreview();
    }

    private void LayoutPreview()
    {
        if (_previewPanel == null || _previewTitle == null || _previewPrompt == null) return;
        Vector2 size = _previewPanel.Size;
        Place(_previewTitle, 12f, 22f, size.X - 24f, 40f);
        Place(_previewPrompt, 22f, 84f, size.X - 44f, size.Y - 120f);
        if (_previewCard == null) return;
        float scale = Math.Max(0.01f, Math.Min(1f,
            Math.Min((size.X - 50f) / NCard.defaultSize.X, (size.Y - 100f) / NCard.defaultSize.Y)));
        _previewCard.Scale = Vector2.One * scale;
        _previewCard.Position = new Vector2(size.X / 2f, 76f + (size.Y - 100f) / 2f);
    }

    private void Select(MemoryGardenUpgradeOption option)
    {
        if (_completed || _sublimating || !_options.Contains(option)) return;
        _selected = option;
        for (int index = 0; index < _optionButtons.Count; index++)
        {
            bool on = ReferenceEquals(_options[index], option);
            _optionButtons[index].SetPressedNoSignal(on);
            _optionIcons[index].Modulate = new Color(1, 1, 1, on ? 1f : .35f);
        }
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

    private async void Confirm()
    {
        if (_completed || _sublimating || _selected == null) return;
        // The next part of the name decodes, the card breaks like glass, a rainbow spreads, then the choice goes through.
        if (_previewCard?.GetNodeOrNull<Control>("%Frame") is { } frame)
        {
            _sublimating = true;
            _confirm.Disabled = true;
            int heldParts = HbrMemoryTitleState.Parts(_selected.PreviewCard);
            HbrMemoryTitleState.Release(_selected.PreviewCard);
            double decode = NHbrCardStyle.Of(_previewCard)?.PlayTitleReveal(heldParts) ?? 0;
            if (decode > 0)
            {
                await ToSignal(GetTree().CreateTimer(decode + .1), SceneTreeTimer.SignalName.Timeout);
                if (!GodotObject.IsInstanceValid(this) || _completed) return;
            }
            await NMemoryGardenSublimation.Play(this, _previewCard, frame, shatter: true);
            if (!GodotObject.IsInstanceValid(this) || _completed) return;
        }
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

    // A glass row; the chosen one turns pink with a pink ring (.opt / .opt.on in the preview).
    private static Button OptionButton()
    {
        Button button = new()
        {
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        StyleBoxFlat Row(Color fill, Color rim, int width) => HbrMemoryGardenTheme.GlassBox(18, fill, rim, width);
        Color chosen = new(1f, .9f, .95f, .9f);
        button.AddThemeStyleboxOverride("normal", Row(HbrMemoryGardenTheme.Glass, HbrMemoryGardenTheme.Rim, 1));
        button.AddThemeStyleboxOverride("hover", Row(HbrMemoryGardenTheme.Glass, new Color(HbrMemoryGardenTheme.Pink, .55f), 2));
        button.AddThemeStyleboxOverride("pressed", Row(chosen, HbrMemoryGardenTheme.Pink, 2));
        button.AddThemeStyleboxOverride("hover_pressed", Row(chosen, HbrMemoryGardenTheme.Pink, 2));
        button.AddThemeStyleboxOverride("focus", Row(new Color(0, 0, 0, 0), new Color(HbrMemoryGardenTheme.Pink, .8f), 2));
        button.AddThemeStyleboxOverride("disabled", Row(HbrMemoryGardenTheme.Glass, HbrMemoryGardenTheme.Rim, 1));
        return button;
    }
}
