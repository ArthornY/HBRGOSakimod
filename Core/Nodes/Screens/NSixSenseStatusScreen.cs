using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NSixSenseStatusScreen : Control, IOverlayScreen
{
    private static readonly Color Accent = new("#ed72aa");
    private static readonly Color Muted = new("#a7afb7");
    private static NSixSenseStatusScreen _activeScreen;
    private ElectronicSoldierManual _manual;
    private SixSenseAttribute _selected = SixSenseAttribute.LeadershipCharm;
    private readonly Dictionary<SixSenseAttribute, Button> _buttons = new();
    private PanelContainer _panel;
    private Control _body;
    private GridContainer _sections;
    private Button _sensesTab;
    private Button _glossaryTab;
    private Control _pageHost;
    private Control _sensesPage;
    private NManualGlossaryPanel _glossaryPage;
    private HBoxContainer _footer;
    private enum ManualPage { Senses, CannonUpgrades, Glossary }
    private ManualPage _page;
    private Button _cannonsTab;
    private NManualCannonUpgradesPanel _cannonsPage;
    private bool SupportsCannonUpgrades => _manual?.Owner?.Character is HbrgoHotaru;
    private ScrollContainer _sidebar;
    private GridContainer _navigation;
    private VBoxContainer _main;
    private ScrollContainer _mainScroll;
    private SixSenseAllocationDraft _draft;
    private Label _points;
    private Button _confirm;
    private Label _summary;
    private Label _selectionTitle;
    private RichTextLabel _details;
    private Button _closeButton;
    private SixSenseRadar _radar;
    private Tween _fadeTween;
    private string _lastState;
    private double _refreshTimer;
    private Action _afterUserClosed;
    private bool _closeRequested;
    private int? _autoCloseAllocationRevision;

    public NetScreenType ScreenType => NetScreenType.Room;
    public bool UseSharedBackstop => true;
    public Control DefaultFocusedControl => _page switch
    {
        ManualPage.Glossary => _glossaryPage?.DefaultFocusedControl ?? (Control)_glossaryTab ?? this,
        ManualPage.CannonUpgrades => _cannonsPage?.DefaultFocusedControl ?? (Control)_cannonsTab ?? this,
        _ => (Control)_closeButton ?? this
    };

    public static void Show(ElectronicSoldierManual manual) => TryShow(manual);

    internal static bool TryShow(ElectronicSoldierManual manual, Action afterUserClosed = null)
    {
        if (NOverlayStack.Instance == null || manual == null) return false;
        if (GodotObject.IsInstanceValid(_activeScreen) &&
            !_activeScreen.IsQueuedForDeletion() && _activeScreen.IsInsideTree())
        {
            _activeScreen.Refresh(manual);
            _activeScreen._afterUserClosed += afterUserClosed;
            return true;
        }
        _activeScreen = new NSixSenseStatusScreen { Name = "SixSenseStatusScreen", _manual = manual, _afterUserClosed = afterUserClosed };
        NOverlayStack.Instance.Push(_activeScreen);
        return true;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutPanel();
        Refresh(_manual);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPanel();
    }

    public override void _Process(double delta)
    {
        if (ShouldCloseAfterAllocation())
        {
            Close();
            return;
        }
        if (_closeRequested && _manual?.IsAllocationPending != true)
        {
            NOverlayStack.Instance?.Remove(this);
            return;
        }
        _refreshTimer += delta;
        if (_refreshTimer < 0.2 || _manual == null) return;
        _refreshTimer = 0;
        string state = $"{_manual.SixSensePoints}:{_manual.MemoryGardenUpgradePoints}:{_manual.SixSenseAllocationRevision}:{_manual.IsAllocationPending}|" + string.Join("|", ElectronicSoldierManual.AllSixSenseAttributes.Select(
            attribute => $"{_manual.GetSenseAmount(attribute)}:{_manual.GetEffectiveSenseAmount(attribute)}"));
        if (SupportsCannonUpgrades)
        {
            Millsage relic = _manual.Owner.GetRelic<Millsage>();
            state += "|cannons:" + (relic == null ? "none" : relic.Layers + ":" +
                string.Join(":", Enum.GetValues<HotaruCannonKind>().Select(kind =>
                    $"{relic.GetCannonUpgradeLevel(kind)}/{relic.GetCannonDamagePercent(kind)}")));
        }
        if (state != _lastState)
        {
            _lastState = state;
            Refresh(_manual);
        }
    }

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input.IsActionPressed("ui_cancel") ||
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
        else if (_page == ManualPage.Glossary && input is InputEventKey
                 { Pressed: true, Echo: false, CtrlPressed: true, Keycode: Key.F })
        {
            _glossaryPage?.FocusSearch();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        // Text editing and native control navigation run first; remaining keys
        // must not become card, map or other gameplay shortcuts behind the manual.
        if (IsVisibleInTree() && input is InputEventKey)
            GetViewport().SetInputAsHandled();
    }

    public void AfterOverlayOpened()
    {
        Modulate = Colors.Transparent;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.15);
    }

    public void AfterOverlayClosed()
    {
        _radar?.StopRepeating();
        _draft?.Clear();
        _fadeTween?.Kill();
        if (ReferenceEquals(_activeScreen, this)) _activeScreen = null;
        Action continuation = _closeRequested ? _afterUserClosed : null;
        _afterUserClosed = null;
        _autoCloseAllocationRevision = null;
        this.QueueFreeSafely();
        if (continuation != null) Callable.From(continuation).CallDeferred();
    }

    public void AfterOverlayShown() { Visible = true; Refresh(_manual); }
    public void AfterOverlayHidden()
    {
        _radar?.StopRepeating();
        _glossaryPage?.ReleaseSearchFocus();
        Visible = false;
    }

    private void BuildUi()
    {
        _panel = new PanelContainer { Name = "SixSensePanel" };
        _panel.AddThemeStyleboxOverride("panel", PanelStyle());
        AddChild(_panel);
        VBoxContainer layout = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 16);
        _panel.AddChild(layout);

        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 14);
        layout.AddChild(header);
        header.AddChild(new TextureRect
        {
            Texture = PreloadManager.Cache.GetTexture2D(
                ImageHelper.GetImagePath("relics/electronic_soldier_manual.png")),
            CustomMinimumSize = new Vector2(42, 42),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        });
        Label title = MakeLabel("电子军人手册", 26);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.AddChild(title);
        _summary = MakeLabel("", 18, Muted);
        header.AddChild(_summary);
        _closeButton = new Button
        {
            Text = "×", TooltipText = "关闭", CustomMinimumSize = new Vector2(44, 44),
            FocusMode = FocusModeEnum.All
        };
        _closeButton.AddThemeFontSizeOverride("font_size", 28);
        StyleButton(_closeButton, false);
        _closeButton.Pressed += Close;
        header.AddChild(_closeButton);
        layout.AddChild(new HSeparator());

        _body = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        _body.Resized += LayoutBody;
        layout.AddChild(_body);
        _sections = new GridContainer { Columns = 1 };
        _sections.AddThemeConstantOverride("v_separation", 8);
        _sections.AddThemeConstantOverride("h_separation", 8);
        _body.AddChild(_sections);
        Button MakeSection(string text, ManualPage page)
        {
            Button button = new()
            {
                Text = text, CustomMinimumSize = new Vector2(0, 48),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All
            };
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Pressed += () => SelectPage(page);
            StyleButton(button, page == ManualPage.Senses);
            _sections.AddChild(button);
            return button;
        }
        _sensesTab = MakeSection("六感", ManualPage.Senses);
        _cannonsTab = MakeSection("浮游炮", ManualPage.CannonUpgrades);
        _cannonsTab.Visible = SupportsCannonUpgrades;
        _glossaryTab = MakeSection("词条全解", ManualPage.Glossary);
        _pageHost = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        _body.AddChild(_pageHost);
        _sensesPage = new Control { MouseFilter = MouseFilterEnum.Pass };
        _pageHost.AddChild(_sensesPage);
        _sensesPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _sidebar = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _sensesPage.AddChild(_sidebar);
        _navigation = new GridContainer
        {
            Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _navigation.AddThemeConstantOverride("v_separation", 8);
        _navigation.AddThemeConstantOverride("h_separation", 8);
        _sidebar.AddChild(_navigation);
        foreach (SixSenseAttribute attribute in ElectronicSoldierManual.AllSixSenseAttributes)
        {
            Button button = new()
            {
                Name = $"Sense{attribute}",
                CustomMinimumSize = new Vector2(0, 54),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left,
                TooltipText = ElectronicSoldierManual.GetSenseShortDescription(attribute)
            };
            button.AddThemeFontSizeOverride("font_size", 19);
            button.Pressed += () => { _selected = attribute; Refresh(_manual); };
            _navigation.AddChild(button);
            _buttons.Add(attribute, button);
        }

        _mainScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _sensesPage.AddChild(_mainScroll);
        _main = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _main.AddThemeConstantOverride("separation", 8);
        _mainScroll.AddChild(_main);
        HBoxContainer chartHeader = new();
        _main.AddChild(chartHeader);
        Label overview = MakeLabel("六感总览", 22);
        overview.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        chartHeader.AddChild(overview);
        chartHeader.AddChild(MakeLabel($"{ElectronicSoldierManual.SixSenseBonusThreshold}级兼具 · {ElectronicSoldierManual.MaxSixSense}级上限", 17, Accent));
        _points = MakeLabel("", 20, Accent);
        _main.AddChild(_points);
        _radar = new SixSenseRadar
        {
            Name = "SixSenseRadar", Manual = _manual,
            CustomMinimumSize = new Vector2(0, 340),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
            Adjust = AdjustPoint
        };
        _main.AddChild(_radar);
        _main.AddChild(new HSeparator());
        _selectionTitle = MakeLabel("", 22);
        _main.AddChild(_selectionTitle);
        _details = new RichTextLabel
        {
            BbcodeEnabled = true, ScrollActive = true, FitContent = false,
            CustomMinimumSize = new Vector2(0, 118),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _details.AddThemeFontSizeOverride("normal_font_size", 19);
        _details.AddThemeFontSizeOverride("bold_font_size", 19);
        _details.AddThemeColorOverride("default_color", new Color("#d7dce1"));
        _main.AddChild(_details);

        _footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        layout.AddChild(_footer);
        _confirm = new Button { Text = "确定", CustomMinimumSize = new Vector2(112, 44), Disabled = true };
        _confirm.AddThemeFontSizeOverride("font_size", 20);
        StyleButton(_confirm, false);
        _confirm.Pressed += ConfirmAllocation;
        _footer.AddChild(_confirm);
    }

    private void LayoutPanel()
    {
        if (_panel == null || Size.X <= 0 || Size.Y <= 0) return;
        _panel.Size = new Vector2(Math.Min(1200, Math.Max(0, Size.X - 48)),
            Math.Min(820, Math.Max(0, Size.Y - 48)));
        _panel.Position = (Size - _panel.Size) / 2f;
        LayoutBody();
    }

    private void LayoutBody()
    {
        if (_body == null || _main == null) return;
        bool stackSections = _body.Size.X < 640;
        int sectionCount = SupportsCannonUpgrades ? 3 : 2;
        _sections.Columns = stackSections ? Math.Clamp((int)(_body.Size.X / 128f), 1, sectionCount) : 1;
        _sections.Position = Vector2.Zero;
        if (stackSections)
        {
            int rows = (sectionCount + _sections.Columns - 1) / _sections.Columns;
            float sectionHeight = rows * 48 + (rows - 1) * 8;
            _sections.Size = new Vector2(_body.Size.X, sectionHeight);
            _pageHost.Position = new Vector2(0, sectionHeight + 12);
            _pageHost.Size = new Vector2(_body.Size.X, Math.Max(0, _body.Size.Y - sectionHeight - 12));
        }
        else
        {
            _sections.Size = new Vector2(120, _body.Size.Y);
            _pageHost.Position = new Vector2(136, 0);
            _pageHost.Size = new Vector2(Math.Max(0, _body.Size.X - 136), _body.Size.Y);
        }
        Vector2 contentSize = _pageHost.Size;
        bool narrow = contentSize.X < 760;
        _navigation.Columns = narrow ? 2 : 1;
        if (narrow)
        {
            float sidebarHeight = Math.Min(184, contentSize.Y * 0.32f);
            _sidebar.Position = Vector2.Zero;
            _sidebar.Size = new Vector2(contentSize.X, sidebarHeight);
            _mainScroll.Position = new Vector2(0, sidebarHeight + 12);
            _mainScroll.Size = new Vector2(contentSize.X, Math.Max(0, contentSize.Y - sidebarHeight - 12));
        }
        else
        {
            _sidebar.Position = Vector2.Zero;
            _sidebar.Size = new Vector2(224, contentSize.Y);
            _mainScroll.Position = new Vector2(248, 0);
            _mainScroll.Size = new Vector2(Math.Max(0, contentSize.X - 248), contentSize.Y);
        }
    }

    private void SelectPage(ManualPage page)
    {
        if (page == ManualPage.CannonUpgrades && !SupportsCannonUpgrades) return;
        _radar?.StopRepeating();
        _glossaryPage?.ReleaseSearchFocus();
        if (page == ManualPage.Glossary && _glossaryPage == null)
        {
            _glossaryPage = new NManualGlossaryPanel { Name = "ManualGlossary" };
            _pageHost.AddChild(_glossaryPage);
            _glossaryPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        if (page == ManualPage.CannonUpgrades && _cannonsPage == null)
        {
            _cannonsPage = new NManualCannonUpgradesPanel { Name = "ManualCannonUpgrades" };
            _pageHost.AddChild(_cannonsPage);
            _cannonsPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        _page = page;
        _sensesPage.Visible = page == ManualPage.Senses;
        if (_glossaryPage != null) _glossaryPage.Visible = page == ManualPage.Glossary;
        if (_cannonsPage != null) _cannonsPage.Visible = page == ManualPage.CannonUpgrades;
        _footer.Visible = page == ManualPage.Senses;
        StyleButton(_sensesTab, page == ManualPage.Senses);
        StyleButton(_cannonsTab, page == ManualPage.CannonUpgrades);
        StyleButton(_glossaryTab, page == ManualPage.Glossary);
        // Reuse the same draft and controls while switching reference pages.
        Refresh(_manual);
        LayoutBody();
        if (page == ManualPage.Glossary) _glossaryPage.FocusSearch();
        else if (page == ManualPage.CannonUpgrades) _cannonsPage.DefaultFocusedControl?.GrabFocus();
        else _sensesTab.GrabFocus();
    }

    private void Refresh(ElectronicSoldierManual manual)
    {
        if (_draft == null || !ReferenceEquals(_manual, manual))
        {
            _autoCloseAllocationRevision = null;
            _radar?.StopRepeating();
            _draft = manual == null ? null : new SixSenseAllocationDraft(manual);
        }
        _manual = manual;
        if (_manual == null || _summary == null) return;
        bool sectionChanged = _cannonsTab.Visible != SupportsCannonUpgrades;
        _cannonsTab.Visible = SupportsCannonUpgrades;
        if (!SupportsCannonUpgrades && _page == ManualPage.CannonUpgrades)
        {
            SelectPage(ManualPage.Senses);
            return;
        }
        _cannonsPage?.Refresh(SupportsCannonUpgrades ? _manual.Owner.GetRelic<Millsage>() : null);
        _draft.Reconcile();
        int total = ElectronicSoldierManual.AllSixSenseAttributes.Sum(_draft.Level);
        int unlocked = ElectronicSoldierManual.AllSixSenseAttributes.Count(sense => _draft.Level(sense) >= ElectronicSoldierManual.SixSenseBonusThreshold);
        bool editable = !_closeRequested && SixSenseNetActions.CanAllocate(_manual) && !_manual.IsAllocationPending;
        _points.Text = $"六感点数  {_draft.Remaining}" + (_draft.Total > 0
            ? $"   {(_manual.IsAllocationPending ? "正在确认" : "待确认")} +{_draft.Total}" : "");
        _confirm.Disabled = !editable || _draft.Total == 0;
        _summary.Text = $"总等级 {total}\n兼具 {unlocked}/6\n庭园升级点 {_manual.MemoryGardenUpgradePoints}";
        foreach ((SixSenseAttribute attribute, Button button) in _buttons)
        {
            int level = _draft.Level(attribute);
            button.Text = $"{ElectronicSoldierManual.GetSenseTitle(attribute)}  {level}/99";
            StyleButton(button, attribute == _selected);
        }
        string name = ElectronicSoldierManual.GetSenseTitle(_selected);
        int amount = _draft.Level(_selected);
        _selectionTitle.Text = $"{name}   Lv.{amount}" + (_draft.Added(_selected) > 0 ? $"  (+{_draft.Added(_selected)})" : "");
        _details.Text = BuildEffectDescription(_selected,
            _manual.GetEffectiveSenseAmount(_selected, amount)).GetFormattedText();
        _radar.Manual = _manual;
        _radar.Draft = _draft;
        _radar.Editable = editable && _page == ManualPage.Senses;
        _radar.Selected = _selected;
        _radar.RefreshButtons();
        _radar.QueueRedraw();
        if (sectionChanged) LayoutBody();
    }

    private static LocString BuildEffectDescription(SixSenseAttribute attribute, decimal level)
    {
        LocString description = new("card_keywords", $"HBRGO_SIX_SENSE.{attribute}.menuDescription");
        decimal normal = SixSenseCombatHelper.EffectValue(attribute, level, false);
        decimal enhanced = SixSenseCombatHelper.EffectValue(attribute, level, true);
        if (attribute == SixSenseAttribute.Madness)
        {
            normal *= 100m;
            enhanced *= 100m;
        }
        description.Add("normal", normal.ToString("0.############################", CultureInfo.InvariantCulture));
        description.Add("enhanced", enhanced.ToString("0.############################", CultureInfo.InvariantCulture));
        description.Add("loss", SixSenseCombatHelper.ResistanceLoss(attribute));
        return description;
    }

    private bool AdjustPoint(SixSenseAttribute attribute, int direction)
    {
        if (_page != ManualPage.Senses || _closeRequested || _manual == null || _manual.IsAllocationPending || !SixSenseNetActions.CanAllocate(_manual) ||
            !_draft.Adjust(attribute, direction)) return false;
        _selected = attribute;
        Refresh(_manual);
        return true;
    }

    private void ConfirmAllocation()
    {
        _radar.StopRepeating();
        _draft.Reconcile();
        int revision = _manual.SixSenseAllocationRevision;
        if (SixSenseNetActions.RequestAllocation(_manual, _draft.Snapshot()))
        {
            if (_afterUserClosed != null) _autoCloseAllocationRevision = revision;
        }
        Refresh(_manual);
    }

    private bool ShouldCloseAfterAllocation()
    {
        if (!_autoCloseAllocationRevision.HasValue || _manual == null || _manual.IsAllocationPending)
            return false;
        int revision = _autoCloseAllocationRevision.Value;
        _autoCloseAllocationRevision = null;
        return _manual.SixSenseAllocationRevision > revision;
    }

    private void Close()
    {
        _closeRequested = true;
        _radar?.StopRepeating();
        _draft?.Clear();
        if (_manual?.IsAllocationPending != true) NOverlayStack.Instance?.Remove(this);
        else Refresh(_manual);
    }

    private static Label MakeLabel(string text, int size, Color? color = null)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Colors.White);
        return label;
    }

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color("#191b20"), BorderColor = new Color("#645562"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 20, ContentMarginBottom = 20,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8
    };

    private static void StyleButton(Button button, bool selected)
    {
        foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
        {
            Color background = selected ? Colors.White :
                state == "normal" ? new Color("#25282e") : new Color("#3c303b");
            button.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = background, BorderColor = Accent,
                BorderWidthLeft = selected || state == "focus" ? 3 : 0,
                ContentMarginLeft = 12, ContentMarginRight = 12,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            });
        }
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(color, selected ? new Color("#7c3058") : Colors.White);
    }

    private partial class SixSenseRadar : Control
    {
        public ElectronicSoldierManual Manual { get; set; }
        internal SixSenseAllocationDraft Draft { get; set; }
        public SixSenseAttribute Selected { get; set; }
        public bool Editable { get; set; }
        public Func<SixSenseAttribute, int, bool> Adjust { get; set; }
        private readonly Dictionary<SixSenseAttribute, (PointButton Minus, PointButton Plus)> _adjusters = new();

        public override void _Ready()
        {
            foreach (SixSenseAttribute sense in ElectronicSoldierManual.AllSixSenseAttributes)
            {
                PointButton Make(int direction)
                {
                    var button = new PointButton
                    {
                        Name = $"{sense}{(direction > 0 ? "Plus" : "Minus")}",
                        Text = direction > 0 ? "+" : "−",
                        TooltipText = (direction > 0 ? "增加" : "撤回") + ElectronicSoldierManual.GetSenseTitle(sense),
                        Size = new Vector2(28, 28), CustomMinimumSize = new Vector2(28, 28),
                        Attempt = () => Adjust?.Invoke(sense, direction) == true
                    };
                    button.AddThemeFontSizeOverride("font_size", 22);
                    StyleButton(button, false);
                    foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
                    {
                        var style = (StyleBoxFlat)button.GetThemeStylebox(state).Duplicate();
                        style.ContentMarginLeft = 0;
                        style.ContentMarginRight = 0;
                        button.AddThemeStyleboxOverride(state, style);
                    }
                    AddChild(button);
                    return button;
                }
                _adjusters[sense] = (Make(-1), Make(1));
            }
            RefreshButtons();
        }

        public void StopRepeating()
        {
            foreach (var pair in _adjusters.Values) { pair.Minus.Stop(); pair.Plus.Stop(); }
        }

        private float Radius => SixSenseRadarGeometry.Radius(Size);

        public void RefreshButtons()
        {
            foreach ((SixSenseAttribute sense, var pair) in _adjusters)
            {
                bool draft = Draft?.Added(sense) > 0;
                pair.Minus.Visible = Editable && draft;
                pair.Plus.Visible = Editable && (Draft?.Remaining > 0 || draft);
                pair.Plus.Disabled = Draft == null || Draft.Remaining == 0 || Draft.Level(sense) >= ElectronicSoldierManual.MaxSixSense;
                if (!pair.Minus.Visible) pair.Minus.Stop();
                if (!pair.Plus.Visible || pair.Plus.Disabled) pair.Plus.Stop();
                Vector2 center = SixSenseRadarGeometry.LabelCenter(Size, (int)sense);
                float halfWidth = GetThemeDefaultFont().GetStringSize(ElectronicSoldierManual.GetSenseTitle(sense), fontSize: 18).X / 2f;
                pair.Minus.Position = center + new Vector2(-halfWidth - 34, -24);
                pair.Plus.Position = center + new Vector2(halfWidth + 6, -24);
            }
        }

        public override void _Notification(int what)
        {
            if (what == NotificationResized) { RefreshButtons(); QueueRedraw(); }
        }

        public override void _Draw()
        {
            if (Manual == null || Size.X < 200 || Size.Y < 120) return;
            Vector2 center = Size / 2f;
            float radius = Radius;
            if (radius <= 0) return;
            foreach (int level in new[] { 20, 40, 60, 80, 99 })
            {
                Vector2[] ring = SixSenseRadarGeometry.Ring(center, radius * level / 99f);
                DrawPolyline(CloseLoop(ring), level == 20
                    ? new Color("#956078") : new Color("#454b55"), level == 20 ? 2f : 1f, true);
            }
            for (int i = 0; i < 6; i++)
                DrawLine(center, center + SixSenseRadarGeometry.Direction(i) * radius, new Color("#454b55"), 1, true);

            int[] values = ElectronicSoldierManual.AllSixSenseAttributes.Select(sense => Draft?.Level(sense) ?? Manual.GetSenseAmount(sense)).ToArray();
            Vector2[] points = SixSenseRadarGeometry.Values(center, radius, values, ElectronicSoldierManual.MaxSixSense);
            // Draw separate sectors so zero-valued adjacent axes never produce a degenerate polygon.
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = points[i] - center, b = points[(i + 1) % 6] - center;
                if (Math.Abs(a.Cross(b)) > 0.01f)
                    DrawColoredPolygon(new[] { center, points[i], points[(i + 1) % 6] }, new Color(0.93f, 0.45f, 0.67f, 0.28f));
            }
            DrawPolyline(CloseLoop(points), Accent, 2f, true);
            Font font = GetThemeDefaultFont();
            for (int i = 0; i < 6; i++)
            {
                SixSenseAttribute attribute = ElectronicSoldierManual.AllSixSenseAttributes[i];
                Vector2 labelCenter = SixSenseRadarGeometry.LabelCenter(Size, i);
                string title = ElectronicSoldierManual.GetSenseTitle(attribute);
                string value = $"{values[i]}/99";
                Color color = attribute == Selected ? Accent : Colors.White;
                DrawCircle(points[i], 3f, color);
                float width = font.GetStringSize(title, fontSize: 18).X;
                DrawString(font, labelCenter + new Vector2(-width / 2f, -2), title, fontSize: 18, modulate: color);
                width = font.GetStringSize(value, fontSize: 16).X;
                DrawString(font, labelCenter + new Vector2(-width / 2f, 20), value, fontSize: 16, modulate: Muted);
            }
        }

        private static Vector2[] CloseLoop(Vector2[] points) => points.Concat(new[] { points[0] }).ToArray();
    }

    private partial class PointButton : Button
    {
        internal Func<bool> Attempt { get; set; }
        private readonly SixSensePointRepeat _repeat = new();
        private bool _holding;

        public override void _Ready()
        {
            ButtonDown += () => { _repeat.Reset(); _holding = !Disabled && Attempt?.Invoke() == true; };
            ButtonUp += Stop;
            MouseExited += Stop;
            FocusExited += Stop;
        }

        internal void Stop() => _holding = false;

        public override void _Process(double delta)
        {
            if (!_holding) return;
            if (!IsVisibleInTree() || Disabled || !IsPressed() || !GetWindow().HasFocus()) { Stop(); return; }
            int count = _repeat.Advance(delta);
            for (int i = 0; i < count && _holding; i++)
                if (Attempt?.Invoke() != true) Stop();
        }
    }
}
