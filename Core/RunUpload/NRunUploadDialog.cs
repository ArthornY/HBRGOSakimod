using System;
using HBRGOSakiMod.Core.Compatibility;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.RunUpload;

// The consent dialog and the small notices of the upload settings. One or two
// buttons; Escape always means "no" (the second button) when there is one.
// The second button holds the default focus, so a stray confirm never agrees.
public partial class NRunUploadDialog : Control, IScreenContext
{
    private string _titleText;
    private string _bodyText;
    private string _primaryText;
    private string _secondaryText;
    private Action _onPrimary;
    private Action _onSecondary;
    private Panel _panel;
    private Label _title;
    private ScrollContainer _scroll;
    private HBoxContainer _buttons;
    private Button _primary;
    private Button _secondary;
    private Font _font;
    private NHotkeyManager _hotkeys;
    private CanvasLayer _overlay;
    private Control _returnFocus;
    private bool _blocking;
    private bool _closing;

    public Control DefaultFocusedControl => _secondary ?? _primary;

    // True while this dialog is on screen and has not been answered.
    internal bool IsOpen => !_closing && GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion() && IsInsideTree();

    internal static bool Show(string title, string body, string primary, string secondary, Action onPrimary,
        Action onSecondary, Control returnFocus = null) =>
        Open(title, body, primary, secondary, onPrimary, onSecondary, returnFocus) != null;

    // Prefers the game's modal container; when another modal owns it (the
    // settings screen may be one), an overlay layer above everything is used.
    internal static NRunUploadDialog Open(string title, string body, string primary, string secondary,
        Action onPrimary, Action onSecondary, Control returnFocus = null)
    {
        var dialog = new NRunUploadDialog
        {
            Name = "RunUploadDialog",
            _titleText = title,
            _bodyText = body,
            _primaryText = primary,
            _secondaryText = secondary,
            _onPrimary = onPrimary,
            _onSecondary = onSecondary,
            _returnFocus = returnFocus
        };
        NModalContainer container = NModalContainer.Instance;
        if (container != null && container.OpenModal == null)
        {
            container.Add(dialog, showBackstop: true);
            if (ReferenceEquals(container.OpenModal, dialog)) return dialog;
            dialog.QueueFree();
            return null;
        }
        if (Engine.GetMainLoop() is SceneTree { Root: { } root })
        {
            var overlay = new CanvasLayer { Name = "RunUploadDialogLayer", Layer = 120 };
            dialog._overlay = overlay;
            overlay.AddChild(dialog);
            root.AddChild(overlay);
            return dialog;
        }
        dialog.QueueFree();
        return null;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _font = FontManager.GetSubstituteFont("zhs", FontType.Regular)
            ?? ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        BuildUi();
        LayoutPanel();
        if (_overlay == null)
        {
            _hotkeys = NHotkeyManager.Instance;
            if (_hotkeys != null)
            {
                _hotkeys.AddBlockingScreen(this);
                _hotkeys.PushHotkeyPressedBindingCompat(MegaInput.cancel, Dismiss);
                _hotkeys.PushHotkeyPressedBindingCompat(MegaInput.pauseAndBack, Dismiss);
                _hotkeys.PushHotkeyPressedBindingCompat(MegaInput.select, ActivateFocused);
                _blocking = true;
            }
        }
        DefaultFocusedControl?.GrabFocus();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Input(InputEvent input)
    {
        if (_closing || !IsVisibleInTree()) return;
        if (_overlay == null && !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (input.IsActionPressed(MegaInput.cancel) ||
            input.IsActionPressed(MegaInput.pauseAndBack) ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } ||
            input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.B })
        {
            GetViewport().SetInputAsHandled();
            Dismiss();
        }
    }

    public override void _ExitTree() => ReleaseBlocking();

    private void Dismiss() => Close(_secondary == null);

    private void ActivateFocused()
    {
        if (_primary?.HasFocus() == true) Close(true);
        else if (_secondary?.HasFocus() == true) Close(false);
    }

    private void Close(bool primary)
    {
        if (_closing) return;
        _closing = true;
        ReleaseBlocking();
        if (_overlay != null)
        {
            _overlay.QueueFree();
        }
        else
        {
            NModalContainer container = NModalContainer.Instance;
            if (ReferenceEquals(container?.OpenModal, this)) container.Clear();
            else QueueFree();
            if (container?.OpenModal == null && GodotObject.IsInstanceValid(_returnFocus) &&
                !_returnFocus.IsQueuedForDeletion() && _returnFocus.IsInsideTree() &&
                _returnFocus.IsVisibleInTree() && _returnFocus.FocusMode != FocusModeEnum.None)
                _returnFocus.GrabFocus();
        }
        try
        {
            (primary ? _onPrimary : _onSecondary)?.Invoke();
        }
        catch (Exception exception)
        {
            RunUploadService.Log("Dialog action failed: " + exception);
        }
    }

    private void ReleaseBlocking()
    {
        if (!_blocking) return;
        _blocking = false;
        if (!GodotObject.IsInstanceValid(_hotkeys)) return;
        _hotkeys.RemoveHotkeyPressedBindingCompat(MegaInput.cancel, Dismiss);
        _hotkeys.RemoveHotkeyPressedBindingCompat(MegaInput.pauseAndBack, Dismiss);
        _hotkeys.RemoveHotkeyPressedBindingCompat(MegaInput.select, ActivateFocused);
        _hotkeys.RemoveBlockingScreen(this);
    }

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

        _panel = new Panel { Name = "DialogPanel", ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", Box(new Color("171922"), new Color("554354")));
        AddChild(_panel);

        _title = TextLabel(_titleText, 28, new Color("F0D8E9"));
        _title.Name = "Title";
        _title.ClipText = true;
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _panel.AddChild(_title);

        _scroll = new ScrollContainer
        {
            Name = "DialogScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.AddChild(_scroll);
        Label body = TextLabel(_bodyText, 20, new Color("ECE6EF"));
        body.Name = "Body";
        _scroll.AddChild(body);

        _buttons = new HBoxContainer { Name = "Buttons", Alignment = BoxContainer.AlignmentMode.Center };
        _buttons.AddThemeConstantOverride("separation", 24);
        _panel.AddChild(_buttons);
        _primary = MakeButton(_primaryText, () => Close(true));
        _buttons.AddChild(_primary);
        if (!string.IsNullOrEmpty(_secondaryText))
        {
            _secondary = MakeButton(_secondaryText, () => Close(false));
            _buttons.AddChild(_secondary);
            _primary.FocusNeighborRight = _primary.FocusNext = _secondary.GetPath();
            _secondary.FocusNeighborLeft = _secondary.FocusPrevious = _primary.GetPath();
        }
    }

    private Button MakeButton(string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(200, 46)
        };
        if (_font != null) button.AddThemeFontOverride("font", _font);
        button.AddThemeFontSizeOverride("font_size", 22);
        button.AddThemeColorOverride("font_color", new Color("F0D8E9"));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Colors.White);
        button.AddThemeStyleboxOverride("normal", Box(new Color("3B2D42"), new Color("8D6F94")));
        button.AddThemeStyleboxOverride("hover", Box(new Color("51405A"), new Color("DDB4D4")));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("2F2335"), new Color("DDB4D4")));
        StyleBoxFlat focus = Box(Colors.Transparent, new Color("F0D8E9"));
        focus.BorderWidthLeft = focus.BorderWidthTop = focus.BorderWidthRight = focus.BorderWidthBottom = 2;
        button.AddThemeStyleboxOverride("focus", focus);
        button.Pressed += pressed;
        return button;
    }

    private void LayoutPanel()
    {
        if (_panel == null || _buttons == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        Vector2 size = new(Math.Max(1, Math.Min(960, viewport.X - 32)), Math.Max(1, Math.Min(820, viewport.Y - 48)));
        _panel.Position = (viewport - size) / 2;
        _panel.Size = size;
        float width = Math.Max(1, size.X - 48);
        Place(_title, 24, 18, width, 44);
        float bodyTop = 18 + 44 + 14;
        Place(_scroll, 24, bodyTop, width, Math.Max(1, size.Y - bodyTop - 92));
        Place(_buttons, 24, size.Y - 70, width, 50);
    }

    private Label TextLabel(string text, int fontSize, Color color)
    {
        var label = new Label
        {
            Text = text ?? string.Empty,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        if (_font != null) label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
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
        CornerRadiusBottomRight = 10,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 6,
        ContentMarginBottom = 6
    };

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(1, width), Math.Max(1, height));
    }
}
