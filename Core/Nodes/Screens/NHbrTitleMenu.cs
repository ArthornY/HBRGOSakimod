using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The cover leads to the original menu. Native buttons retain their positions,
// actions, saved-run summary, unlock checks and third-party menu entries.
public partial class NHbrTitleMenu : Control
{
    private const string NodeName = "HbrTitleMenu";
    private const string ArtPath = "res://images/ui/title/";
    private static readonly Vector2 ReferenceSize = new(1920, 1080);
    private NMainMenu _menu;
    private Control _canvas;
    private Control _chrome;
    private Control _nativeList;
    private NButton _profile;
    private NButton _patchNotes;
    private NMainMenuTextButton _website;
    private NHbrTitleButton _start;
    private NHbrTitleButton _hamburger;
    private TextureRect _startImage;
    private Font _font;
    private bool _menuShown;
    private bool _ready;
    private bool _refreshing;

    internal static NHbrTitleMenu Find(NMainMenu menu) => menu?.GetNodeOrNull<NHbrTitleMenu>(NodeName);

    internal static void Install(NMainMenu menu)
    {
        if (Find(menu) != null) return;
        var title = new NHbrTitleMenu { Name = NodeName, _menu = menu };
        menu.AddChild(title);
        // Paint behind every native control, including RitsuLib's independent
        // button below PatchNotesButton. The native modal/blur order stays intact.
        menu.MoveChild(title, menu.GetNode<Control>("MainMenuBg").GetIndex() + 1);
        if (menu.GetNodeOrNull<Label>("ModdedWarning") is { } warning)
        {
            warning.AddThemeColorOverride("font_outline_color", new Color("362E43"));
            warning.AddThemeConstantOverride("outline_size", 2);
        }
        title.Refresh();
        if (ActiveScreenContext.Instance.IsCurrent(menu))
            title.DefaultFocusedControl?.GrabFocus();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _font = FontManager.GetSubstituteFont(LocManager.Instance.Language, FontType.Regular)
            ?? ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        _nativeList = _menu.GetNode<Control>("MainMenuTextButtons");
        _website = HbrWebsiteMenuEntry.Find(_menu);
        _profile = _menu.GetNode<NButton>("ChangeProfileButton");
        _patchNotes = _menu.GetNode<NButton>("PatchNotesButton");
        Control background = _menu.GetNode<Control>("MainMenuBg");
        background.Hide();
        background.ProcessMode = ProcessModeEnum.Disabled;
        // Keep native text legible over the new white cover. Its own focus,
        // disabled-state and press animations still control label modulation.
        foreach (NMainMenuTextButton button in _nativeList.GetChildren().OfType<NMainMenuTextButton>())
        {
            if (button.label == null) continue;
            button.label.AddThemeColorOverride("font_color", new Color("65516F"));
            button.label.AddThemeColorOverride("font_outline_color", Colors.White);
            button.label.AddThemeConstantOverride("outline_size", 2);
        }
        AddChild(new ColorRect { Name = "WhiteBackground", Color = Colors.White,
            MouseFilter = MouseFilterEnum.Ignore, AnchorsPreset = (int)LayoutPreset.FullRect });
        GetChild<Control>(0).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _canvas = new Control { Name = "TitleCanvas", Size = ReferenceSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_canvas);
        // This is the supplied 3840x2160 cover, copied without altering its art.
        Sprite(_canvas, "SakikoArtwork", "sakiko_title_cover_4k.png", new(0, 0), new(1920, 1080));
        _chrome = new Control { Name = "TitleControls", Size = ReferenceSize, MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_chrome);
        Sprite(_chrome, "Sts2Logo", "sts2_logo_static.png", new(145, 250), new(650, 328)).SelfModulate = new Color("5D465F");
        _start = Button(_chrome, "TapToStart", new(210, 784), new(520, 101), ShowNativeMenu);
        _startImage = Sprite(_start, "Message", "tap_to_start.png", Vector2.Zero, _start.Size);
        _start.Highlighted = active => _startImage.Texture = Texture(active
            ? "tap_to_start_active.png" : "tap_to_start.png");
        _hamburger = Button(_chrome, "Menu", new(40, 40), new(148, 148), ShowNativeMenu);
        BuildHamburger();
        _start.FocusNeighborTop = _start.FocusNeighborLeft = _hamburger.GetPath();
        _hamburger.FocusNeighborBottom = _hamburger.FocusNeighborRight = _start.GetPath();
        LayoutCanvas();
        _ready = true;
        ActiveScreenContext.Instance.Updated += Refresh;
        Refresh();
    }

    public override void _ExitTree() => ActiveScreenContext.Instance.Updated -= Refresh;

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutCanvas();
        if (what == NotificationTranslationChanged && _ready)
        {
            HbrWebsiteMenuEntry.RefreshLabel(_menu);
            Refresh();
        }
    }

    public override void _Input(InputEvent input)
    {
        // Once the menu is open, its original hotkeys (including profile/back)
        // belong to the native controls and submenus.
        if (!_ready || _menuShown || input.IsEcho() ||
            !ActiveScreenContext.Instance.IsCurrent(_menu) || !IsVisibleInTree()) return;
        if (NDevConsole.Instance?.Visible == true) return;
        Control focused = GetViewport().GuiGetFocusOwner();
        if (focused is LineEdit lineEdit && lineEdit.IsEditing() ||
            focused is NMegaTextEdit textEdit && textEdit.IsEditing()) return;
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Z }) return;
        GetViewport().SetInputAsHandled();
        ShowNativeMenu();
    }

    internal bool UsesNativeMenu => _ready && _menuShown;
    internal Control DefaultFocusedControl => _ready && !_menuShown ? _start : null;

    internal void Refresh()
    {
        if (!_ready || _refreshing || !IsInsideTree()) return;
        _refreshing = true;
        try
        {
            bool active = ActiveScreenContext.Instance.IsCurrent(_menu);
            bool mainLayer = !_menu.SubmenuStack.SubmenusOpen;
            _chrome.Visible = active && !_menuShown;
            _start.SetEnabled(active && !_menuShown);
            _hamburger.SetEnabled(active && !_menuShown);
            _nativeList.Visible = mainLayer && _menuShown;
            _website?.SetEnabled(active && mainLayer && _menuShown);
            _menu.GetNode<Control>("ButtonReticleLeft").Visible = mainLayer && _menuShown;
            _menu.GetNode<Control>("ButtonReticleRight").Visible = mainLayer && _menuShown;
            _profile.Visible = mainLayer && _menuShown;
            _profile.SetEnabled(active && _menuShown);
            // RitsuLib mirrors this native button's visibility and anchors.
            // Keep it at its original position on both the cover and main menu.
            _patchNotes.Visible = mainLayer;
            _patchNotes.SetEnabled(active);
            if (!_menuShown) _menu.ContinueRunInfo.AnimHide();
            // Keep _menuShown when a submenu/modal opens so returning restores
            // the original menu rather than requiring another start click.
        }
        finally { _refreshing = false; }
    }

    private void ShowNativeMenu()
    {
        if (!ActiveScreenContext.Instance.IsCurrent(_menu)) return;
        _menuShown = true;
        Refresh();
        _menu.DefaultFocusedControl?.GrabFocus();
    }

    private void BuildHamburger()
    {
        Texture2D image = Texture("title_atlas.png");
        if (image != null)
        {
            var normal = new AtlasTexture { Atlas = image, Region = new Rect2(0, 139, 101, 101) };
            var active = new AtlasTexture { Atlas = image, Region = new Rect2(0, 364, 148, 148) };
            var sprite = new TextureRect { Name = "MenuImage", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Texture = normal, Position = new(23.5f, 23.5f), Size = new(101, 101), MouseFilter = MouseFilterEnum.Ignore };
            _hamburger.AddChild(sprite);
            _hamburger.Highlighted = highlighted =>
            {
                sprite.Texture = highlighted ? active : normal;
                sprite.Position = highlighted ? Vector2.Zero : new(23.5f, 23.5f);
                sprite.Size = highlighted ? new(148, 148) : new(101, 101);
            };
        }
        else
        {
            var panel = new Panel { Size = _hamburger.Size, MouseFilter = MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Colors.White,
                CornerRadiusTopLeft = 48, CornerRadiusTopRight = 48, CornerRadiusBottomLeft = 48, CornerRadiusBottomRight = 48,
                ShadowColor = new("7F6D8655"), ShadowSize = 10 });
            _hamburger.AddChild(panel);
            var glyph = new Label { Name = "MenuGlyph", Text = "≡", Size = _hamburger.Size,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore };
            glyph.AddThemeFontOverride("font", _font);
            glyph.AddThemeFontSizeOverride("font_size", 58);
            glyph.AddThemeColorOverride("font_color", new Color("AB9DB3"));
            _hamburger.AddChild(glyph);
        }
    }

    private void LayoutCanvas()
    {
        if (_canvas == null) return;
        float scale = Math.Max(0.1f, Math.Min(Size.X / ReferenceSize.X, Size.Y / ReferenceSize.Y));
        _canvas.Scale = Vector2.One * scale;
        _canvas.Position = (Size - ReferenceSize * scale) / 2;
    }

    private static NHbrTitleButton Button(Control parent, string name, Vector2 position, Vector2 size, Action action)
    {
        var button = new NHbrTitleButton { Name = name, Position = position, Size = size, Activated = action,
            FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand, MouseFilter = MouseFilterEnum.Stop };
        parent.AddChild(button);
        return button;
    }

    private static TextureRect Sprite(Control parent, string name, string file, Vector2 position, Vector2 size)
    {
        // Ignore the texture's minimum size before assigning Size: a 4K cover
        // otherwise clamps the 1920x1080 canvas rect to its native dimensions.
        var image = new TextureRect { Name = name, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = Texture(file), Position = position, Size = size, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(image);
        return image;
    }

    private static Texture2D Texture(string file) => ResourceLoader.Load<Texture2D>(ArtPath + file);
}
