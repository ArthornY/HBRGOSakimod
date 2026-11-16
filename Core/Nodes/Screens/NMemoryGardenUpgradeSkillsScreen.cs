using System;
using HBRGOSakiMod.Core.Compatibility;
using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.Cards;
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
            _cardTitle = HbrMemoryTitleState.Title(card),
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
        NHotkeyManager.Instance.PushHotkeyPressedBindingCompat(MegaInput.cancel, Close);
        NHotkeyManager.Instance.PushHotkeyPressedBindingCompat(MegaInput.pauseAndBack, Close);
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
        NHotkeyManager.Instance.RemoveHotkeyPressedBindingCompat(MegaInput.cancel, Close);
        NHotkeyManager.Instance.RemoveHotkeyPressedBindingCompat(MegaInput.pauseAndBack, Close);
        NHotkeyManager.Instance.RemoveBlockingScreen(this);
    }

    public override void _ExitTree() => ReleaseBlocking();

    // The preview's skills modal: a light frosted veil, a white box, one row per skill under a fine rule.
    private void BuildUi()
    {
        var shade = new ColorRect
        {
            Color = new Color(240 / 255f, 243 / 255f, 250 / 255f, .84f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _panel = new Panel { ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        StyleBoxFlat box = HbrMemoryGardenTheme.GlassBox(24, new Color(1, 1, 1, .94f));
        box.ShadowSize = 50;
        box.ShadowOffset = new Vector2(0, 20);
        box.ShadowColor = new Color(60 / 255f, 80 / 255f, 130 / 255f, .18f);
        _panel.AddThemeStyleboxOverride("panel", box);
        AddChild(_panel);
        _title = HbrMemoryGardenTheme.Label($"「{_cardTitle}」 · {Text("all_upgrade_skills")}", 30, HbrMemoryGardenTheme.Ink);
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _panel.AddChild(_title);
        _hint = HbrMemoryGardenTheme.Label(Text("all_upgrade_skills_hint"), 17, HbrMemoryGardenTheme.Ink2);
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
        content.AddThemeConstantOverride("separation", 0);
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
            var rule = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                BorderColor = new Color(126 / 255f, 146 / 255f, 196 / 255f, .25f),
                BorderWidthBottom = 1,
                ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 14, ContentMarginBottom = 14
            };
            entry.AddThemeStyleboxOverride("panel", rule);
            content.AddChild(entry);
            entries.Add(entry);
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 14);
            entry.AddChild(row);
            row.AddChild(NumberTag(skill.EffectId + 1));
            var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 6);
            row.AddChild(body);
            Label title = HbrMemoryGardenTheme.Label(skill.Title, 22, HbrMemoryGardenTheme.Ink);
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
            description.AddThemeColorOverride("default_color", HbrMemoryGardenTheme.Ink2);
            foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
                description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
            body.AddChild(description);
        }
        _close = HbrMemoryGardenTheme.HbrButton(Text("close_upgrade_skills"));
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

    // The compendium has no run to read, so the tag numbers each skill instead of marking it acquired.
    private static PanelContainer NumberTag(int number)
    {
        var tag = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        var pill = new StyleBoxFlat
        {
            BgColor = new Color(126 / 255f, 146 / 255f, 196 / 255f, .2f),
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 2, ContentMarginBottom = 2,
            AntiAliasing = true
        };
        tag.AddThemeStyleboxOverride("panel", pill);
        tag.AddChild(HbrMemoryGardenTheme.Label(number.ToString(), 16, HbrMemoryGardenTheme.Ink2));
        return tag;
    }

    private void LayoutPanel()
    {
        if (_panel == null || _close == null) return;
        Vector2 viewport = Size;
        if (viewport.X <= 0 || viewport.Y <= 0) viewport = GetViewportRect().Size;
        Vector2 origin = HbrMemoryGardenTheme.Origin(viewport);
        Vector2 size = new(980, Math.Max(1, Math.Min(800, viewport.Y - 220)));
        _panel.Position = new Vector2((viewport.X - size.X) / 2, origin.Y + 170);
        _panel.Size = size;
        float width = Math.Max(1, size.X - 68);
        Place(_title, 34, 26, width, 44);
        Place(_hint, 34, 74, width, 48);
        Place(_scroll, 34, 128, width, size.Y - 128 - 136);
        Place(_close, (size.X - 300) / 2, size.Y - 128, 300, 112);
    }

    internal static string Text(string key) =>
        new LocString("cards", "HBRGO_MEMORY_GARDEN." + key).GetFormattedText();

    private static void Place(Control control, float x, float y, float width, float height)
    {
        control.Position = new Vector2(x, y);
        control.Size = new Vector2(Math.Max(1, width), Math.Max(1, height));
    }
}
