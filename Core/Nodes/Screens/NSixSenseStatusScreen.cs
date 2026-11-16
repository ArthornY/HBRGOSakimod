using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The Electronic Soldier Manual, drawn as HBR's terminal phone (ui-concepts/manual-phone-20261104):
// a home screen with the terminal buttons, then one page at a time inside the phone. Esc and the
// back button step back a page (home closes the manual); the left plate always closes it.
// Allocation, network confirmation and the reward entry's deferred close are unchanged.
public partial class NSixSenseStatusScreen : Control, IOverlayScreen
{
    private enum ManualPage { Home, Senses, CannonUpgrades, BattleStats, Equipment, Shop, Glossary, FateDice }

    private static NSixSenseStatusScreen _activeScreen;
    private ElectronicSoldierManual _manual;
    private SixSenseAttribute _selected = SixSenseAttribute.LeadershipCharm;
    private ManualPage _page = ManualPage.Home;
    private readonly Stack<ManualPage> _history = new();
    private bool _statsView;
    private int _statHighlight = -1;
    private NManualGlossaryPanel _glossaryPage;
    private NManualBattleStatsPanel _battleStatsPage;
    private NManualCannonUpgradesPanel _cannonsPage;
    private NManualEquipmentPanel _equipmentPage;
    private NManualShopPanel _shopPage;
    private NManualFateDicePanel _fateDicePage;
    private bool SupportsCannonUpgrades => _manual?.Owner?.Character is HbrgoHotaru;
    // Equipment belongs to the HBR numerics layer; old runs keep the old manual.
    private bool SupportsEquipment => _manual?.IsHbrNumericsEnabled == true;
    private bool SupportsFateDice => _manual?.UsesFateDice == true;
    private SixSenseAllocationDraft _draft;
    private Tween _fadeTween;
    private string _lastState;
    private double _refreshTimer;
    private Action _afterUserClosed;
    private bool _closeRequested;
    private int? _autoCloseAllocationRevision;
    private bool _relicsHidden;

    public NetScreenType ScreenType => NetScreenType.Room;
    // The phone draws its own blurred, dimmed backdrop; the 85% black shared backstop would leave
    // nothing to blur. The stack still keeps the (transparent) backstop catching clicks.
    public bool UseSharedBackstop => false;
    public Control DefaultFocusedControl => _page switch
    {
        ManualPage.Home => _home?.DefaultFocusedControl ?? this,
        ManualPage.Senses => _sensesPage?.DefaultFocusedControl ?? this,
        ManualPage.Glossary => _glossaryPage?.DefaultFocusedControl ?? (Control)_back ?? this,
        ManualPage.CannonUpgrades => _cannonsPage?.DefaultFocusedControl ?? (Control)_back ?? this,
        ManualPage.BattleStats => _battleStatsPage?.DefaultFocusedControl ?? (Control)_back ?? this,
        ManualPage.Equipment => _equipmentPage?.DefaultFocusedControl ?? (Control)_back ?? this,
        ManualPage.Shop => _shopPage?.DefaultFocusedControl ?? (Control)_back ?? this,
        ManualPage.FateDice => _fateDicePage?.DefaultFocusedControl ?? (Control)_back ?? this,
        _ => this
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
        _activeScreen = new NSixSenseStatusScreen
        {
            Name = "SixSenseStatusScreen", _manual = manual, _afterUserClosed = afterUserClosed,
            // The six-sense reward opens straight onto the senses page.
            _page = afterUserClosed != null ? ManualPage.Senses : ManualPage.Home
        };
        NOverlayStack.Instance.Push(_activeScreen);
        return true;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutRig();
        ManualPage start = _page;
        _page = ManualPage.Home;
        Refresh(_manual);
        if (start != ManualPage.Home) SelectPage(start, animate: false);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutRig();
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
        // Combat buffs do not change saved six-sense progression. Give the
        // visible stat panel its own small, cached refresh for live attributes.
        if (_page == ManualPage.BattleStats) _battleStatsPage?.Refresh(_manual);
        // Gold, network results and combat start/end change these pages too.
        if (_page == ManualPage.Equipment) _equipmentPage?.Refresh(_manual);
        if (_page == ManualPage.Shop) _shopPage?.Refresh(_manual);
        if (_page == ManualPage.FateDice) _fateDicePage?.Refresh(_manual);
        string state = $"{_manual.SixSensePoints}:{_manual.MemoryGardenUpgradePoints}:{_manual.SixSenseAllocationRevision}:{_manual.IsAllocationPending}|battle:{_manual.HbrBattleProgressData}|" + string.Join("|", ElectronicSoldierManual.AllSixSenseAttributes.Select(
            attribute => $"{_manual.GetSenseAmount(attribute)}:{_manual.GetEffectiveSenseAmount(attribute)}"));
        if (SupportsCannonUpgrades)
        {
            Millsage relic = _manual.Owner.GetRelic<Millsage>();
            state += "|cannons:" + (relic == null ? "none" : relic.Layers + ":" + relic.RetiredCannonsMask + ":" + relic.EndgameSign + ":" +
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
            Back();
        }
        else if (input is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true, Keycode: Key.F })
        {
            // Ctrl+F opens the glossary from any page and focuses its search box.
            if (_page != ManualPage.Glossary) SelectPage(ManualPage.Glossary);
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

    public override void _ExitTree() => RestoreRelics();

    // The relic row draws above overlays and would cover the top of the phone in a long run. Slide it
    // away while the manual is up, as the vanilla capstone screens do, and bring back only what was moved.
    private void HideRelics()
    {
        if (_relicsHidden || MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi?.RelicInventory is not { } relics) return;
        relics.AnimHide();
        _relicsHidden = true;
    }

    private void RestoreRelics()
    {
        if (!_relicsHidden) return;
        _relicsHidden = false;
        MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi?.RelicInventory?.AnimShow();
    }

    public void AfterOverlayOpened()
    {
        HideRelics();
        Modulate = Colors.Transparent;
        _fadeTween?.Kill();
        _fadeTween = CreateTween().SetParallel();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.15);
        // The phone rises 70px (at the rig's scale) into place, as the preview's #rig does.
        if (_rig != null)
        {
            _rig.Position = RigOrigin() + new Vector2(0f, HbrManualPhoneLayout.RigRise * _rig.Scale.Y);
            _fadeTween.TweenProperty(_rig, "position", RigOrigin(), HbrManualPhoneLayout.RigRiseDuration)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        }
    }

    public void AfterOverlayClosed()
    {
        RestoreRelics();
        _sensesPage?.StopRepeating();
        _draft?.Clear();
        _fadeTween?.Kill();
        if (ReferenceEquals(_activeScreen, this)) _activeScreen = null;
        Action continuation = _closeRequested ? _afterUserClosed : null;
        _afterUserClosed = null;
        _autoCloseAllocationRevision = null;
        this.QueueFreeSafely();
        if (continuation != null) Callable.From(continuation).CallDeferred();
    }

    public void AfterOverlayShown() { Visible = true; HideRelics(); Refresh(_manual); }

    public void AfterOverlayHidden()
    {
        RestoreRelics();
        _sensesPage?.StopRepeating();
        _glossaryPage?.ReleaseSearchFocus();
        Visible = false;
    }

    // Back one page: pages opened from another page return to it, the rest return home; home closes.
    private void Back()
    {
        if (_page == ManualPage.Home)
        {
            Close();
            return;
        }
        if (_history.Count > 0) SelectPage(_history.Pop(), remember: false);
        else SelectPage(ManualPage.Home, remember: false);
    }

    private void SelectPage(ManualPage page, bool animate = true, bool remember = true)
    {
        if (page == ManualPage.CannonUpgrades && !SupportsCannonUpgrades) return;
        if (page is ManualPage.Equipment or ManualPage.Shop && !SupportsEquipment) return;
        if (page == ManualPage.FateDice && !SupportsFateDice) return;
        _sensesPage?.StopRepeating();
        _glossaryPage?.ReleaseSearchFocus();
        if (remember && _page != ManualPage.Home && _page != page) _history.Push(_page);
        if (page == ManualPage.Home) _history.Clear();
        if (page == ManualPage.Glossary && _glossaryPage == null)
            _glossaryPage = HostPanel(new NManualGlossaryPanel
            {
                Name = "ManualGlossary", HbrNumericsEnabled = _manual?.IsHbrNumericsEnabled == true,
                CountChanged = RefreshHeader
            });
        if (page == ManualPage.CannonUpgrades && _cannonsPage == null)
            _cannonsPage = HostPanel(new NManualCannonUpgradesPanel { Name = "ManualCannonUpgrades" });
        if (page == ManualPage.BattleStats && _battleStatsPage == null)
            _battleStatsPage = HostPanel(new NManualBattleStatsPanel { Name = "ManualBattleStats" });
        if (page == ManualPage.Equipment && _equipmentPage == null)
            _equipmentPage = HostPanel(new NManualEquipmentPanel { Name = "ManualEquipment" });
        if (page == ManualPage.Shop && _shopPage == null)
            _shopPage = HostPanel(new NManualShopPanel { Name = "ManualShop" });
        if (page == ManualPage.FateDice && _fateDicePage == null)
            _fateDicePage = HostPanel(new NManualFateDicePanel { Name = "ManualFateDice" });

        ManualPage previous = _page;
        _page = page;
        // The stat picked on the home dock or the senses page stays lit until home is reached again.
        if (page == ManualPage.Home) _statHighlight = -1;
        if (page == ManualPage.BattleStats) _battleStatsPage.Highlight(_statHighlight);
        _sensesPage.Visible = page == ManualPage.Senses;
        if (_glossaryPage != null) _glossaryPage.Visible = page == ManualPage.Glossary;
        if (_cannonsPage != null) _cannonsPage.Visible = page == ManualPage.CannonUpgrades;
        if (_battleStatsPage != null) _battleStatsPage.Visible = page == ManualPage.BattleStats;
        if (_equipmentPage != null) _equipmentPage.Visible = page == ManualPage.Equipment;
        if (_shopPage != null) _shopPage.Visible = page == ManualPage.Shop;
        if (_fateDicePage != null) _fateDicePage.Visible = page == ManualPage.FateDice;
        ShowPage(previous, page, animate);
        // Reuse the same draft and controls while switching reference pages.
        Refresh(_manual);
        if (page == ManualPage.Glossary) _glossaryPage.FocusSearch();
        else DefaultFocusedControl?.GrabFocus();
    }

    private T HostPanel<T>(T panel) where T : Control
    {
        _body.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return panel;
    }

    private void Refresh(ElectronicSoldierManual manual)
    {
        if (_draft == null || !ReferenceEquals(_manual, manual))
        {
            _autoCloseAllocationRevision = null;
            _sensesPage?.StopRepeating();
            _draft = manual == null ? null : new SixSenseAllocationDraft(manual);
        }
        _manual = manual;
        if (_manual == null || _home == null) return;
        if ((!SupportsCannonUpgrades && _page == ManualPage.CannonUpgrades) ||
            (!SupportsEquipment && _page is ManualPage.Equipment or ManualPage.Shop) ||
            (!SupportsFateDice && _page == ManualPage.FateDice))
        {
            SelectPage(ManualPage.Home, remember: false);
            return;
        }
        _cannonsPage?.Refresh(SupportsCannonUpgrades ? _manual.Owner.GetRelic<Millsage>() : null);
        _battleStatsPage?.Refresh(_manual);
        _equipmentPage?.Refresh(_manual);
        _shopPage?.Refresh(_manual);
        _fateDicePage?.Refresh(_manual);
        _glossaryPage?.Refresh(_manual.IsHbrNumericsEnabled);
        _draft.Reconcile();
        bool editable = !_closeRequested && SixSenseNetActions.CanAllocate(_manual) && !_manual.IsAllocationPending;
        _home.Refresh(_manual, _draft,
            HbrManualPhoneLayout.HomeButtons(SupportsEquipment, SupportsCannonUpgrades, SupportsFateDice));
        string detail = _statsView && _manual.IsHbrNumericsEnabled
            ? BuildStatDescription(_selected)
            : BuildBattleGrowthDescription(_selected) + BuildSenseRule(_selected, _manual.IsHbrNumericsEnabled);
        _sensesPage.Refresh(_manual, _draft, _selected, _statsView, editable && _page == ManualPage.Senses, detail);
        RefreshHeader();
        SetExitState(_manual.IsAllocationPending ? ExitState.Disabled : _exitHovered ? ExitState.Active : ExitState.Normal);
    }

    private string BuildBattleGrowthDescription(SixSenseAttribute attribute)
    {
        if (!_manual.IsHbrNumericsEnabled) return string.Empty;
        string stat = HbrBattleStatsHelper.GetBattleStatTitle(attribute);
        decimal perLevel = HbrBattleStatsHelper.SenseStatGainPerLevel;
        decimal current = _manual.GetEffectiveSenseAmount(attribute) * perLevel;
        string text = $"战斗属性：每级使{stat} +{perLevel}，按有效等级结算。\n" +
            $"已确认 Lv.{_manual.GetSenseAmount(attribute)}：{stat} +{current.ToString("0.##", CultureInfo.InvariantCulture)}。\n";
        if (_draft.Added(attribute) > 0)
        {
            decimal planned = _manual.GetEffectiveSenseAmount(attribute, _draft.Level(attribute)) * perLevel;
            text += $"待确认：此项加成将为 +{planned.ToString("0.##", CultureInfo.InvariantCulture)}；确认前战斗属性不变。\n";
        }
        return text;
    }

    // The battle stat view's description of the stat on the selected sense's axis.
    private string BuildStatDescription(SixSenseAttribute attribute)
    {
        int index = HbrManualPhoneData.StatIndex(attribute);
        string stat = HbrBattleStatsHelper.GetBattleStatTitle(attribute);
        decimal value = HbrManualPhoneData.StatValue(HbrManualPhoneData.Stats(_manual), index);
        decimal effective = _manual.GetEffectiveSenseAmount(attribute);
        string text = $"{stat}：{HbrManualPhoneData.StatUses[index]}。\n" +
            $"来自六感：{ElectronicSoldierManual.GetSenseTitle(attribute)} Lv.{_manual.GetSenseAmount(attribute)} → " +
            $"+{HbrManualPhoneData.Number(HbrManualPhoneData.SenseBonus(_manual, attribute))}" +
            $"（有效等级 {HbrManualPhoneData.Number(effective)} × {HbrBattleStatsHelper.SenseStatGainPerLevel}）。\n" +
            $"战斗等级 Lv.{_manual.HbrBattleLevel}：基础值 {HbrManualPhoneData.Number(HbrManualPhoneData.BaseStat(_manual))}" +
            $"（初始 {HbrManualPhoneData.Number(HbrBattleStatsHelper.InitialStat)}，每级 +{HbrBattleStatsHelper.StatGainPerLevel}）。\n";
        decimal gain = HbrManualPhoneData.PendingGain(_manual, _draft, attribute);
        if (gain > 0)
            text += $"待确认：确认后为 {HbrManualPhoneData.Number(value + gain)}；确认前战斗属性不变。\n";
        return text;
    }

    // Six senses are level points only (2026-11-09): cards carry no attribute and
    // enemies no resistance. What a level still does besides the battle stat above.
    private static string BuildSenseRule(SixSenseAttribute attribute, bool hbrNumericsEnabled) =>
        new LocString("card_keywords", $"HBRGO_SIX_SENSE.{attribute}." +
            (hbrNumericsEnabled ? "hbrMenuDescription" : "menuDescription")).GetFormattedText();

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
        _sensesPage?.StopRepeating();
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
        _sensesPage?.StopRepeating();
        _draft?.Clear();
        if (_manual?.IsAllocationPending != true) NOverlayStack.Instance?.Remove(this);
        else Refresh(_manual);
    }
}
