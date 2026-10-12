using System;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// A read-only modal works in the main-menu compendium as well as during a run.
// Run-only overlays cannot cover the native inspect screen in both contexts.
public partial class NMemoryGardenUpgradeSkillsScreen : Control, IScreenContext
{
    private IReadOnlyList<MemoryGardenUpgradeSkill> _skills;
    private string _cardTitle;
    private Panel _panel;
    private Label _title;
    private Label _hint;
    private ScrollContainer _scroll;
    private Button _close;
    private Control _returnFocus;
    private bool _blocking;

    public Control DefaultFocusedControl => _close;

    internal static void Show(CardModel card, Control returnFocus)
    {
        NModalContainer container = NModalContainer.Instance;
        if (!HbrMemoryGardenCompendiumHelper.CanBrowseSkills(card) ||
            container == null || container.OpenModal != null) return;

        var screen = new NMemoryGardenUpgradeSkillsScreen
        {
            Name = "MemoryGardenUpgradeSkills",
            _cardTitle = card.Title,
            _skills = HbrMemoryGardenUpgradeCatalog.GetAllUpgradeSkills(card),
            _returnFocus = returnFocus
        };
        NHoverTipSet.Clear();
        container.Add(screen, showBackstop: true);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutPanel();
        NHotkeyManager.Instance.AddBlockingScreen(this);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(MegaInput.cancel, Close);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(MegaInput.pauseAndBack, Close);
        _blocking = true;
        _close.GrabFocus();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || !ActiveScreenContext.Instance.IsCurrent(this)) return;
        if (input.IsActionPressed("ui_cancel") ||
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
        if (!ReferenceEquals(container?.OpenModal, this)) return;
        ReleaseBlocking();
        container.Clear();
        if (GodotObject.IsInstanceValid(_returnFocus) && !_returnFocus.IsQueuedForDeletion() &&
            _returnFocus.IsVisibleInTree()) _returnFocus.GrabFocus();
    }

    private void ReleaseBlocking()
    {
        if (!_blocking) return;
        _blocking = false;
        NHotkeyManager.Instance.RemoveHotkeyPressedBinding(MegaInput.cancel, Close);
        NHotkeyManager.Instance.RemoveHotkeyPressedBinding(MegaInput.pauseAndBack, Close);
        NHotkeyManager.Instance.RemoveBlockingScreen(this);
    }

    public override void _ExitTree() => ReleaseBlocking();

    private void BuildUi()
    {
        var shade = new ColorRect
        {
            Color = new Color(0.018f, 0.022f, 0.028f, 0.9f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _panel = new Panel { ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", Box(new Color("171922"), new Color("554354")));
        AddChild(_panel);
        _title = Label(_cardTitle + " · " + Text("all_upgrade_skills"), 28, new Color("F0D8E9"));
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _panel.AddChild(_title);
        _hint = Label(Text("all_upgrade_skills_hint"), 17, new Color("B9B6C1"));
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _panel.AddChild(_hint);
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
            MouseFilter = MouseFilterEnum.Stop
        };
        _panel.AddChild(_scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 14);
        _scroll.AddChild(content);
        var entries = new List<Control>();
        foreach (MemoryGardenUpgradeSkill skill in _skills)
        {
            var entry = new PanelContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All,
                MouseFilter = MouseFilterEnum.Pass
            };
            StyleBoxFlat style = Box(new Color("282430"), new Color("5E5568"));
            style.ContentMarginLeft = style.ContentMarginRight = 18;
            style.ContentMarginTop = style.ContentMarginBottom = 14;
            entry.AddThemeStyleboxOverride("panel", style);
            content.AddChild(entry);
            entries.Add(entry);
            var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 8);
            entry.AddChild(body);
            Label title = Label($"{skill.EffectId + 1}. {skill.Title}", 22, new Color("F0D8E9"));
            title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(title);
            var description = new MegaRichTextLabel
            {
                Text = HbrMemoryGardenCompendiumHelper.FormatSkillDescription(skill.Description),
                BbcodeEnabled = true, AutoSizeEnabled = false, FitContent = true,
                ScrollActive = false, SelectionEnabled = false,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            description.AddThemeFontSizeOverride("normal_font_size", 20);
            description.AddThemeFontSizeOverride("bold_font_size", 20);
            description.AddThemeColorOverride("default_color", new Color("D4CBD7"));
            foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
                description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
            body.AddChild(description);
        }
        _close = new Button
        {
            Text = Text("close_upgrade_skills"),
            MouseDefaultCursorShape = CursorShape.PointingHand,
            FocusMode = FocusModeEnum.All
        };
        _close.AddThemeFontSizeOverride("font_size", 20);
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
        Vector2 size = new(Math.Max(1, Math.Min(960, viewport.X - 32)),
            Math.Max(1, Math.Min(820, viewport.Y - 48)));
        _panel.Position = (viewport - size) / 2;
        _panel.Size = size;
        float width = Math.Max(1, size.X - 48);
        Place(_title, 24, 18, width, 42);
        Place(_hint, 24, 66, width, 48);
        Place(_scroll, 24, 122, width, size.Y - 202);
        Place(_close, size.X - 184, size.Y - 60, 160, 42);
    }

    internal static string Text(string key) =>
        new LocString("cards", "HBRGO_MEMORY_GARDEN." + key).GetFormattedText();

    private static Label Label(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat Box(Color fill, Color border) => new()
    {
        BgColor = fill, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10
    };

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(1, width), Math.Max(1, height));
    }
}
