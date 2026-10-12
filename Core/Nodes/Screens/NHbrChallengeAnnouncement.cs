using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NHbrChallengeAnnouncement : Control, IScreenContext
{
    private Action _acknowledged;
    private Control _returnFocus;
    private NHotkeyManager _hotkeyManager;
    private Panel _panel;
    private Control _header;
    private Label _title;
    private ScrollContainer _scroll;
    private Button _close;
    private Font _font;
    private bool _blocking;
    private bool _closing;

    public Control DefaultFocusedControl => _close;

    internal static NHbrChallengeAnnouncement Create(Action acknowledged, Control returnFocus) => new()
    {
        Name = "HbrChallengeAnnouncement",
        _acknowledged = acknowledged,
        _returnFocus = returnFocus
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _font = FontManager.GetSubstituteFont("zhs", FontType.Regular)
            ?? ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        BuildUi();
        LayoutPanel();
        _hotkeyManager = NHotkeyManager.Instance;
        if (_hotkeyManager != null)
        {
            _hotkeyManager.AddBlockingScreen(this);
            _hotkeyManager.PushHotkeyPressedBinding(MegaInput.cancel, Close);
            _hotkeyManager.PushHotkeyPressedBinding(MegaInput.pauseAndBack, Close);
            _hotkeyManager.PushHotkeyPressedBinding(MegaInput.select, Confirm);
            _blocking = true;
        }
        if (ActiveScreenContext.Instance.IsCurrent(this)) _close.GrabFocus();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Input(InputEvent input)
    {
        if (_closing || !IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (input.IsActionPressed(MegaInput.cancel) ||
            input.IsActionPressed(MegaInput.pauseAndBack) ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    private void Close()
    {
        NModalContainer container = NModalContainer.Instance;
        // A higher-priority screen or a replacement modal must retain its input.
        if (_closing || !ReferenceEquals(container?.OpenModal, this) ||
            !ActiveScreenContext.Instance.IsCurrent(this)) return;
        _closing = true;
        GetViewport().SetInputAsHandled();
        ReleaseBlocking();
        try
        {
            _acknowledged?.Invoke();
        }
        finally
        {
            if (ReferenceEquals(container.OpenModal, this)) container.Clear();
            if (container.OpenModal == null && GodotObject.IsInstanceValid(_returnFocus) &&
                !_returnFocus.IsQueuedForDeletion() && _returnFocus.IsInsideTree() &&
                _returnFocus.IsVisibleInTree() && _returnFocus.FocusMode != FocusModeEnum.None)
                _returnFocus.GrabFocus();
        }
    }

    private void Confirm()
    {
        if (_close?.HasFocus() == true) Close();
    }

    private void ReleaseBlocking()
    {
        if (!_blocking) return;
        _blocking = false;
        if (!GodotObject.IsInstanceValid(_hotkeyManager)) return;
        _hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.cancel, Close);
        _hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.pauseAndBack, Close);
        _hotkeyManager.RemoveHotkeyPressedBinding(MegaInput.select, Confirm);
        _hotkeyManager.RemoveBlockingScreen(this);
    }

    public override void _ExitTree() => ReleaseBlocking();

    private void BuildUi()
    {
        var shade = new ColorRect
        {
            Name = "Shade",
            Color = new Color(0.018f, 0.022f, 0.028f, 0.9f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _panel = new Panel { Name = "AnnouncementPanel", ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", Box(new Color("171922"), new Color("554354")));
        AddChild(_panel);
        _header = new Control
        {
            Name = "AnnouncementHeader",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        _panel.AddChild(_header);
        _title = TextLabel(ChallengeAnnouncementHelper.Title, 30, new Color("F0D8E9"));
        _title.Name = "Title";
        // Autowrap starts at zero width during construction. Clipping prevents
        // that temporary text height from enlarging the fixed header rectangle.
        _title.ClipText = true;
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.VerticalAlignment = VerticalAlignment.Top;
        _header.AddChild(_title);
        _title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _scroll = new ScrollContainer
        {
            Name = "AnnouncementScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.AddChild(_scroll);
        var content = new VBoxContainer
        {
            Name = "AnnouncementContent",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(content);
        Label intro = TextLabel(ChallengeAnnouncementHelper.Intro, 22, new Color("D4CBD7"));
        intro.Name = "Intro";
        content.AddChild(intro);
        Label rulesHeading = TextLabel("规则：", 22, new Color("F0D8E9"));
        rulesHeading.Name = "RulesHeading";
        content.AddChild(rulesHeading);
        var entries = new List<Control>();
        foreach (string rule in ChallengeAnnouncementHelper.Rules)
        {
            var entry = new PanelContainer
            {
                Name = $"Rule{entries.Count + 1}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All,
                MouseFilter = MouseFilterEnum.Pass
            };
            StyleBoxFlat normal = RuleBox(new Color("24212B"), new Color("403747"));
            StyleBoxFlat focused = RuleBox(new Color("302637"), new Color("F0D8E9"));
            entry.AddThemeStyleboxOverride("panel", normal);
            entry.FocusEntered += () => entry.AddThemeStyleboxOverride("panel", focused);
            entry.FocusExited += () => entry.AddThemeStyleboxOverride("panel", normal);
            entry.AddChild(TextLabel(rule, 22, new Color("ECE6EF")));
            content.AddChild(entry);
            entries.Add(entry);
        }
        _close = new Button
        {
            Name = "AcknowledgeButton",
            Text = "我知道了",
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        if (_font != null) _close.AddThemeFontOverride("font", _font);
        _close.AddThemeFontSizeOverride("font_size", 22);
        _close.AddThemeColorOverride("font_color", new Color("F0D8E9"));
        _close.AddThemeColorOverride("font_hover_color", Colors.White);
        _close.AddThemeColorOverride("font_pressed_color", Colors.White);
        _close.AddThemeStyleboxOverride("normal", Box(new Color("3B2D42"), new Color("8D6F94")));
        _close.AddThemeStyleboxOverride("hover", Box(new Color("51405A"), new Color("DDB4D4")));
        _close.AddThemeStyleboxOverride("pressed", Box(new Color("2F2335"), new Color("DDB4D4")));
        var focus = Box(Colors.Transparent, new Color("F0D8E9"));
        focus.BorderWidthLeft = focus.BorderWidthTop = focus.BorderWidthRight = focus.BorderWidthBottom = 2;
        _close.AddThemeStyleboxOverride("focus", focus);
        _close.Pressed += Close;
        _panel.AddChild(_close);
        for (int index = 0; index < entries.Count; index++)
        {
            Control entry = entries[index];
            NodePath previous = (index == 0 ? _close : entries[index - 1]).GetPath();
            NodePath next = (index == entries.Count - 1 ? _close : entries[index + 1]).GetPath();
            entry.FocusPrevious = entry.FocusNeighborTop = previous;
            entry.FocusNext = entry.FocusNeighborBottom = next;
        }
        if (entries.Count > 0)
        {
            _close.FocusPrevious = _close.FocusNeighborTop = entries[^1].GetPath();
            _close.FocusNext = _close.FocusNeighborBottom = entries[0].GetPath();
        }
    }

    private void LayoutPanel()
    {
        if (_panel == null || _close == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        Vector2 size = new(Math.Max(1, Math.Min(1040, viewport.X - 32)),
            Math.Max(1, Math.Min(900, viewport.Y - 48)));
        _panel.Position = (viewport - size) / 2;
        _panel.Size = size;
        float width = Math.Max(1, size.X - 48);
        float titleHeight = size.X < 720 ? 92 : 54;
        Place(_header, 24, 16, width, titleHeight);
        float bodyTop = 16 + titleHeight + 16;
        // Keep the button outside the scroll region at 720p and when resized.
        Place(_scroll, 24, bodyTop, width, Math.Max(1, size.Y - bodyTop - 88));
        float buttonWidth = Math.Min(220, width);
        Place(_close, (size.X - buttonWidth) / 2, size.Y - 66, buttonWidth, 46);
    }

    private Label TextLabel(string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        if (_font != null) label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat RuleBox(Color fill, Color border)
    {
        StyleBoxFlat box = Box(fill, border);
        box.ContentMarginLeft = box.ContentMarginRight = 16;
        box.ContentMarginTop = box.ContentMarginBottom = 12;
        return box;
    }

    private static StyleBoxFlat Box(Color fill, Color border) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 10,
        CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10,
        CornerRadiusBottomRight = 10
    };

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(1, width), Math.Max(1, height));
    }
}
