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

// Hotaru's cannon upgrades in the phone manual's page style (ui-concepts/manual-phone-20261104,
// 浮游炮): the rules and the five cannons as a list of cards on the left, the chosen entry on the right.
public partial class NManualCannonUpgradesPanel : Control
{
    private const float ListWidth = 300f;
    private static readonly IReadOnlyList<HotaruCannonKind> Kinds = HotaruCannonKinds.Original;
    private readonly Dictionary<HotaruCannonKind, Button> _buttons = new();
    private readonly Dictionary<HotaruCannonKind, Label> _captions = new();
    private readonly Dictionary<HotaruCannonKind, TextureRect> _pictures = new();
    private Millsage _relic;
    private HotaruCannonKind _selected = HotaruCannonKind.Happiness;
    private bool _showRules = true;
    private Button _rulesButton;
    private ScrollContainer _listScroll;
    private VBoxContainer _navigation;
    private PanelContainer _detail;
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
        LayoutContent();
        Refresh(_relic);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutContent();
    }

    internal void Refresh(Millsage relic)
    {
        _relic = relic;
        if (_navigation == null) return;
        HbrManualPhoneStyle.ListEntry(_rulesButton, _showRules, 24);
        foreach (HotaruCannonKind kind in Kinds)
        {
            int level = Level(kind);
            bool unlocked = IsUnlocked(kind);
            bool selected = !_showRules && kind == _selected;
            _captions[kind].Text = HotaruCannonUpgrades.CannonTitle(kind) +
                $"  {level}/{Millsage.MaximumCannonUpgradeLevel}" + (unlocked ? "" :
                    _relic?.WasCannonRetired(kind) == true ? " · 已永久失去" : " · 未拥有");
            _captions[kind].AddThemeColorOverride("font_color", unlocked ? Colors.White : HbrManualPhoneStyle.Dim);
            _pictures[kind].Modulate = unlocked ? Colors.White : new Color(1f, 1f, 1f, 0.45f);
            _buttons[kind].TooltipText = $"查看{HotaruCannonUpgrades.CannonTitle(kind)}的全部升级效果";
            HbrManualPhoneStyle.ListEntry(_buttons[kind], selected, 21);
        }
        RefreshDescription();
    }

    private void BuildUi()
    {
        _listScroll = new ScrollContainer
        {
            Name = "CannonList", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FocusMode = FocusModeEnum.None
        };
        HbrManualPhoneStyle.Scroll(_listScroll);
        AddChild(_listScroll);
        _navigation = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _navigation.AddThemeConstantOverride("separation", 12);
        _listScroll.AddChild(_navigation);
        _rulesButton = new Button
        {
            Name = "CannonRules", Text = "通用规则", CustomMinimumSize = new Vector2(0, 64),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = "通用规则介绍倍率、磨损、恢复和活力。选择一门浮游炮，可同时查看当前等级、已获得与未解锁的升级效果。"
        };
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
            // .cn: the cannon art (280x70) over its caption in a 112px card.
            Button button = new()
            {
                Name = $"Cannon{kind}", CustomMinimumSize = new Vector2(0, 112),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            button.Pressed += () => Select(kind);
            TextureRect picture = new()
            {
                Texture = PreloadManager.Cache.GetTexture2D(HotaruCannonUpgrades.IconPath(kind)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new Vector2(10f, 10f), Size = new Vector2(ListWidth - 20f, 70f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            button.AddChild(picture);
            Label caption = HbrManualPhoneStyle.Label("", 21, Colors.White);
            caption.HorizontalAlignment = HorizontalAlignment.Center;
            caption.Position = new Vector2(0f, 80f);
            caption.Size = new Vector2(ListWidth, 28f);
            button.AddChild(caption);
            _navigation.AddChild(button);
            _buttons.Add(kind, button);
            _captions.Add(kind, caption);
            _pictures.Add(kind, picture);
        }

        _detail = new PanelContainer();
        _detail.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Panel(18, 22f, 18f));
        AddChild(_detail);
        _detailScroll = new ScrollContainer
        {
            Name = "CannonUpgradeDescriptionScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        HbrManualPhoneStyle.Scroll(_detailScroll);
        _detail.AddChild(_detailScroll);
        VBoxContainer content = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 10);
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
        titles.AddThemeConstantOverride("separation", 4);
        heading.AddChild(titles);
        // #c-detail h3 30px cyan, .st 22px pink.
        _title = HbrManualPhoneStyle.Label("", 30, HbrManualPhoneArt.Cyan, wrap: true);
        titles.AddChild(_title);
        _status = HbrManualPhoneStyle.Label("", 20, HbrManualPhoneArt.Pink, wrap: true);
        titles.AddChild(_status);
        content.AddChild(HbrManualPhoneStyle.Separator());
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
        _description.AddThemeFontSizeOverride("normal_font_size", 20);
        _description.AddThemeFontSizeOverride("bold_font_size", 20);
        _description.AddThemeColorOverride("default_color", HbrManualPhoneStyle.Text);
        _description.AddThemeConstantOverride("line_separation", 6);
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
            _status.Text = "灵魂之力、磨损与恢复、升级、活力及五门炮的基础效果";
            _description.Text = Format(HotaruCannonUpgrades.ManualRules(_relic?.Owner));
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
    // Obtained tiers are cyan; locked tiers are dimmed, as .line.locked in the preview.
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
        text.Append($"[b][color=#{HbrManualPhoneLayout.Cyan}]基础效果[/color][/b]\n");
        text.Append(Format(HotaruCannonUpgrades.BaseText(kind, relic?.Owner)));
        for (int tier = 1; tier <= Millsage.MaximumCannonUpgradeLevel; tier++)
        {
            bool obtained = tier <= level;
            string status = obtained ? "已获得" : unlocked && tier == level + 1 ? "未解锁 · 下一项" : "未解锁";
            string color = obtained ? "#" + HbrManualPhoneLayout.Cyan2 : "#" + HbrManualPhoneLayout.Muted;
            text.Append($"\n\n[b][color={color}]第{tier}级 · {status}[/color][/b]\n");
            string body = Format(HotaruCannonUpgrades.UpgradeText(kind, tier, relic?.Owner));
            text.Append(obtained ? body : $"[color=#dcebee80]{body}[/color]");
        }
        return text.ToString();
    }

    private int Level(HotaruCannonKind kind) =>
        Math.Clamp(_relic?.GetCannonUpgradeLevel(kind) ?? 0, 0, Millsage.MaximumCannonUpgradeLevel);

    private bool IsUnlocked(HotaruCannonKind kind) => _relic?.IsCannonUnlocked(kind) == true;

    private static string Format(string raw) =>
        HbrManualGlossaryPresentation.FormatDescription(raw, "hotaru_cannon");

    // #c-list 300px on the left, #c-detail beside it 16px apart.
    private void LayoutContent()
    {
        if (_navigation == null) return;
        bool wide = Size.X >= 650f;
        float listWidth = wide ? ListWidth : Size.X;
        float listHeight = wide ? Size.Y : Math.Min(Size.Y * 0.45f, 300f);
        _listScroll.Position = Vector2.Zero;
        _listScroll.Size = new Vector2(listWidth, listHeight);
        _detail.Position = wide ? new Vector2(ListWidth + 16f, 0f) : new Vector2(0f, listHeight + 12f);
        _detail.Size = wide
            ? new Vector2(Math.Max(0f, Size.X - ListWidth - 16f), Size.Y)
            : new Vector2(Size.X, Math.Max(0f, Size.Y - listHeight - 12f));
    }
}
