using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NManualCannonUpgradesPanel : Control
{
    private static readonly Color Accent = new("#ed72aa");
    private static readonly Color Muted = new("#a7afb7");
    private static readonly IReadOnlyList<HotaruCannonKind> Kinds = HotaruCannonKinds.Original;
    private readonly Dictionary<HotaruCannonKind, Button> _buttons = new();
    private Millsage _relic;
    private HotaruCannonKind _selected = HotaruCannonKind.Happiness;
    private bool _showRules = true;
    private Button _rulesButton;
    private GridContainer _navigation;
    private ScrollContainer _detailScroll;
    private TextureRect _icon;
    private Label _title;
    private Label _status;
    private MegaRichTextLabel _description;

    internal Control DefaultFocusedControl =>
        _showRules ? (Control)_rulesButton ?? this :
        _buttons.TryGetValue(_selected, out Button button) ? button : this;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutNavigation();
        Refresh(_relic);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutNavigation();
    }

    internal void Refresh(Millsage relic)
    {
        _relic = relic;
        if (_navigation == null) return;
        _rulesButton.SetPressedNoSignal(_showRules);
        StyleButton(_rulesButton, _showRules);
        foreach (HotaruCannonKind kind in Kinds)
        {
            int level = Level(kind);
            bool unlocked = IsUnlocked(kind);
            Button button = _buttons[kind];
            button.Text = HotaruCannonUpgrades.CannonTitle(kind) +
                $"\n{level}/{Millsage.MaximumCannonUpgradeLevel}" + (unlocked ? "" :
                    _relic?.WasCannonRetired(kind) == true ? " · 已永久失去" : " · 未拥有");
            button.TooltipText = $"查看{HotaruCannonUpgrades.CannonTitle(kind)}的全部升级效果";
            button.SetPressedNoSignal(!_showRules && kind == _selected);
            StyleButton(button, !_showRules && kind == _selected);
        }
        RefreshDescription();
    }

    private void BuildUi()
    {
        VBoxContainer layout = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(layout);
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layout.AddThemeConstantOverride("separation", 12);
        Label introduction = MakeLabel(
            "通用规则介绍倍率、磨损、恢复和活力。选择一门浮游炮，可同时查看当前等级、已获得与未解锁的升级效果。",
            17, Muted);
        introduction.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(introduction);

        _navigation = new GridContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _navigation.AddThemeConstantOverride("h_separation", 8);
        _navigation.AddThemeConstantOverride("v_separation", 8);
        layout.AddChild(_navigation);
        _rulesButton = new Button
        {
            Name = "CannonRules", Text = "通用规则", ToggleMode = true,
            CustomMinimumSize = new Vector2(0, 64), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand
        };
        _rulesButton.AddThemeFontSizeOverride("font_size", 16);
        _rulesButton.Pressed += () =>
        {
            bool changed = !_showRules;
            _showRules = true;
            Refresh(_relic);
            if (changed) _detailScroll.ScrollVertical = 0;
        };
        _navigation.AddChild(_rulesButton);
        foreach (HotaruCannonKind kind in Kinds)
        {
            Button button = new()
            {
                Name = $"Cannon{kind}",
                CustomMinimumSize = new Vector2(0, 64),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand,
                ToggleMode = true
            };
            button.AddThemeFontSizeOverride("font_size", 16);
            button.Pressed += () => Select(kind);
            _navigation.AddChild(button);
            _buttons.Add(kind, button);
        }

        PanelContainer detail = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detail.AddThemeStyleboxOverride("panel", Surface(new Color("#191b20"), new Color("#645562"), 1, 16));
        layout.AddChild(detail);
        _detailScroll = new ScrollContainer
        {
            Name = "CannonUpgradeDescriptionScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        detail.AddChild(_detailScroll);
        VBoxContainer content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        _detailScroll.AddChild(content);
        HBoxContainer heading = new();
        heading.AddThemeConstantOverride("separation", 14);
        content.AddChild(heading);
        _icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(64, 64),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore
        };
        heading.AddChild(_icon);
        VBoxContainer titles = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 6);
        heading.AddChild(titles);
        _title = MakeLabel("", 25, Colors.White);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titles.AddChild(_title);
        _status = MakeLabel("", 17, Accent);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titles.AddChild(_status);
        content.AddChild(new HSeparator());
        _description = new MegaRichTextLabel
        {
            Name = "CannonUpgradeDescription",
            BbcodeEnabled = true,
            AutoSizeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        _description.AddThemeFontSizeOverride("normal_font_size", 19);
        _description.AddThemeFontSizeOverride("bold_font_size", 19);
        _description.AddThemeColorOverride("default_color", new Color("#d7dce1"));
        foreach (string font in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
            _description.AddThemeFontOverride(font, GetThemeFont(font, "RichTextLabel"));
        content.AddChild(_description);
    }

    private void Select(HotaruCannonKind kind)
    {
        if (!Enum.IsDefined(kind)) return;
        bool changed = _showRules || _selected != kind;
        _showRules = false;
        _selected = kind;
        Refresh(_relic);
        if (changed) _detailScroll.ScrollVertical = 0;
    }

    private void RefreshDescription()
    {
        if (_showRules)
        {
            _icon.Texture = PreloadManager.Cache.GetTexture2D("res://images/relics/millsage.webp");
            _icon.Modulate = Colors.White;
            _title.Text = "浮游炮 · 通用规则";
            _status.Text = "倍率、磨损与恢复、升级、活力及五门炮的基础效果";
            _description.Text = Format(HotaruCannonSkillPowerHelper.Description(
                "rest_site_ui", "HOTARU_CANNON_UPGRADE.manualRules", _relic?.Owner).GetFormattedText());
            return;
        }
        int level = Level(_selected);
        bool unlocked = IsUnlocked(_selected);
        _icon.Texture = PreloadManager.Cache.GetTexture2D(HotaruCannonUpgrades.IconPath(_selected));
        _icon.Modulate = unlocked ? Colors.White : new Color(1f, 1f, 1f, 0.55f);
        _title.Text = HotaruCannonUpgrades.CannonTitle(_selected);
        _status.Text = $"当前升级 {level}/{Millsage.MaximumCannonUpgradeLevel}" +
            (!unlocked ? _relic?.WasCannonRetired(_selected) == true ? " · 已永久失去此浮游炮" : " · 尚未拥有此浮游炮" :
                level == Millsage.MaximumCannonUpgradeLevel ? " · 已获得全部升级" : " · 以下显示全部升级效果");
        _description.Text = BuildDescription(_relic, _selected);
    }

    // Reference pages only read saved levels; browsing must not grant or spend upgrade points.
    internal static string BuildDescription(Millsage relic, HotaruCannonKind kind)
    {
        int level = Math.Clamp(relic?.GetCannonUpgradeLevel(kind) ?? 0, 0, Millsage.MaximumCannonUpgradeLevel);
        bool unlocked = relic?.IsCannonUnlocked(kind) == true;
        StringBuilder text = new();
        if (relic?.WasCannonRetired(kind) == true)
            text.Append("[gold]已永久失去此炮；以下升级记录仅供查阅，效果不再生效。[/gold]\n\n");
        text.Append(Format(HotaruCannonUpgrades.DamageMultiplierText(level,
            relic?.GetCannonDamagePercent(kind) ?? Millsage.InitialCannonDamagePercent)));
        text.Append("\n\n");
        text.Append("[b]基础效果[/b]\n");
        text.Append(Format(HotaruCannonUpgrades.BaseText(kind, relic?.Owner)));
        for (int tier = 1; tier <= Millsage.MaximumCannonUpgradeLevel; tier++)
        {
            bool obtained = tier <= level;
            string status = obtained ? "已获得" : unlocked && tier == level + 1 ? "未解锁 · 下一项" : "未解锁";
            string color = obtained ? "#ed72aa" : "#a7afb7";
            text.Append($"\n\n[b][color={color}]第{tier}级 · {status}[/color][/b]\n");
            text.Append(Format(HotaruCannonUpgrades.UpgradeText(kind, tier, relic?.Owner)));
        }
        return text.ToString();
    }

    private int Level(HotaruCannonKind kind) =>
        Math.Clamp(_relic?.GetCannonUpgradeLevel(kind) ?? 0, 0, Millsage.MaximumCannonUpgradeLevel);

    private bool IsUnlocked(HotaruCannonKind kind) => _relic?.IsCannonUnlocked(kind) == true;

    private static string Format(string raw) =>
        HbrManualGlossaryPresentation.FormatDescription(raw, "hotaru_cannon");

    private void LayoutNavigation()
    {
        if (_navigation == null) return;
        _navigation.Columns = Size.X >= 880 ? 6 : Size.X >= 480 ? 3 : 2;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static void StyleButton(Button button, bool selected)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            Color background = selected ? new Color("#f2dbe6") :
                state == "normal" ? new Color("#25282e") : new Color("#3c303b");
            button.AddThemeStyleboxOverride(state,
                Surface(background, Accent, selected || state == "focus" ? 2 : 0, 8));
        }
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(color, selected ? new Color("#7c3058") : Colors.White);
    }

    private static StyleBoxFlat Surface(Color background, Color border, int width, int padding) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthRight = width,
        BorderWidthTop = width,
        BorderWidthBottom = width,
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5
    };
}
