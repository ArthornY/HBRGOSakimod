using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NPhantomRosterScreen : Control, IOverlayScreen
{
    private const float TopBarInset = 80f;

    private enum RosterView
    {
        Roster,
        Detail,
        Upgrade,
        Devour,
        EffectSelection
    }

    private static NPhantomRosterScreen _activeScreen;

    private readonly List<PhantomChoiceHbrgoSaki> _displayCards = new();
    private readonly HashSet<int> _selectedMaterialIds = new();
    private readonly HashSet<PhantomFusionEffectType>
        _selectedEffectTypes = new();

    private MomentOfReign _relic;
    private Control _body;
    private Control _footer;
    private Label _title;
    private Label _subtitle;
    private Label _selectionSummary;
    private Button _defaultButton;
    private Button _confirmButton;
    private CheckBox _consumeFullFusionCheckBox;
    private CheckBox _selectAllMaterialsCheckBox;
    private NCardGrid _grid;
    private Control _upgradePreviewSlot;
    private PhantomRosterEntry? _targetEntry;
    private PhantomRosterEntry? _unrestrictedUpgradePreview;
    private RosterView _view;
    private bool _updatingEffectButtons;
    private bool _updatingFullFusionCheckBox;
    private bool _consumeFullFusion;
    private Tween _fadeTween;
    private bool _relicInventoryHidden;
    private Control _overclockGauge;
    private bool _overclockGaugeVisibilityCaptured;
    private bool _overclockGaugeWasVisible;
    private Control _rollHistoryWidget;
    private bool _rollHistoryVisibilityCaptured;
    private bool _rollHistoryWasVisible;

    private int FusionSlotCount => _targetEntry?.FusionSlotCount ?? 0;

    public NetScreenType ScreenType => NetScreenType.Room;

    public bool UseSharedBackstop => true;

    public Control DefaultFocusedControl =>
        (Control)_defaultButton ?? this;

    public static void Show(MomentOfReign relic)
    {
        NOverlayStack overlayStack = NOverlayStack.Instance;
        if (overlayStack == null)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_activeScreen) &&
            !_activeScreen.IsQueuedForDeletion() &&
            _activeScreen.IsInsideTree())
        {
            _activeScreen._relic = relic;
            _activeScreen.ShowRoster();
            return;
        }

        NPhantomRosterScreen screen = new()
        {
            Name = "PhantomRosterScreen",
            _relic = relic
        };
        _activeScreen = screen;
        overlayStack.Push(screen);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetFullRect(this);
        BuildShell();
        ShowRoster();
    }

    public override void _ExitTree()
    {
        _fadeTween?.Kill();
        RestoreCombatWidgets();
        RestoreRelicInventory();
        if (ReferenceEquals(_activeScreen, this))
        {
            _activeScreen = null;
        }
    }

    public void AfterOverlayOpened()
    {
        HideRelicInventory();
        HideCombatWidgets();
        Modulate = Colors.Transparent;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
    }

    public void AfterOverlayClosed()
    {
        _fadeTween?.Kill();
        RestoreCombatWidgets();
        RestoreRelicInventory();
        this.QueueFreeSafely();
    }

    public void AfterOverlayShown()
    {
        Visible = true;
        HideRelicInventory();
        HideCombatWidgets();
    }

    public void AfterOverlayHidden()
    {
        Visible = false;
    }

    private void BuildShell()
    {
        ColorRect backdrop = new()
        {
            Color = new Color(0.018f, 0.022f, 0.028f, 0.94f),
            MouseFilter = MouseFilterEnum.Stop
        };
        SetFullRect(backdrop);
        AddChild(backdrop);

        ColorRect headerBand = new()
        {
            AnchorRight = 1f,
            AnchorBottom = 0.105f,
            OffsetTop = TopBarInset,
            OffsetBottom = TopBarInset,
            Color = new Color(0.055f, 0.066f, 0.078f, 0.98f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(headerBand);

        ColorRect headerRule = new()
        {
            AnchorTop = 0.105f,
            AnchorRight = 1f,
            AnchorBottom = 0.108f,
            OffsetTop = TopBarInset,
            OffsetBottom = TopBarInset,
            Color = new Color(0.95f, 0.52f, 0.68f, 0.88f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(headerRule);

        _title = CreateLabel(string.Empty, 34, Colors.White);
        _title.AnchorLeft = 0.2f;
        _title.AnchorTop = 0.014f;
        _title.AnchorRight = 0.72f;
        _title.AnchorBottom = 0.062f;
        _title.OffsetTop = TopBarInset;
        _title.OffsetBottom = TopBarInset;
        AddChild(_title);

        _subtitle = CreateLabel(
            string.Empty,
            19,
            new Color(0.76f, 0.79f, 0.82f));
        _subtitle.AnchorLeft = 0.2f;
        _subtitle.AnchorTop = 0.058f;
        _subtitle.AnchorRight = 0.82f;
        _subtitle.AnchorBottom = 0.1f;
        _subtitle.OffsetTop = TopBarInset;
        _subtitle.OffsetBottom = TopBarInset;
        AddChild(_subtitle);

        Button closeButton = CreateButton("关闭", accent: false);
        closeButton.AnchorLeft = 0.88f;
        closeButton.AnchorTop = 0.025f;
        closeButton.AnchorRight = 0.96f;
        closeButton.AnchorBottom = 0.082f;
        closeButton.OffsetTop = TopBarInset;
        closeButton.OffsetBottom = TopBarInset;
        closeButton.Pressed += Close;
        AddChild(closeButton);

        _body = new Control
        {
            AnchorLeft = 0.035f,
            AnchorTop = 0.115f,
            AnchorRight = 0.965f,
            AnchorBottom = 0.86f,
            OffsetTop = TopBarInset,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass
        };
        AddChild(_body);

        _footer = new Control
        {
            AnchorLeft = 0.035f,
            AnchorTop = 0.875f,
            AnchorRight = 0.965f,
            AnchorBottom = 0.975f,
            MouseFilter = MouseFilterEnum.Pass
        };
        AddChild(_footer);
    }

    private void ShowRoster()
    {
        if (_body == null || _relic == null)
        {
            return;
        }

        _view = RosterView.Roster;
        _targetEntry = null;
        _unrestrictedUpgradePreview = null;
        _selectedMaterialIds.Clear();
        _selectedEffectTypes.Clear();
        _consumeFullFusion = false;
        ClearDynamicUi();
        SetHeader(
            "幻影召唤列表",
            "选择一张本局击败的怪物牌，查看成长状态或进行融合。重复击败会保留为不同卡牌。");

        IReadOnlyList<PhantomRosterEntry> entries =
            _relic.GetRosterEntries();
        if (entries.Count == 0)
        {
            Label emptyLabel = CreateLabel(
                "本局尚未击败可召唤的怪物。战斗时将默认召唤 SmallHopper。",
                25,
                new Color(0.82f, 0.84f, 0.86f));
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            emptyLabel.VerticalAlignment = VerticalAlignment.Center;
            SetFullRect(emptyLabel);
            _body.AddChild(emptyLabel);
        }
        else
        {
            _displayCards.AddRange(entries.Select(CreateDisplayCard));
            _grid = CreateCardGrid();
            SetFullRect(_grid);
            _grid.Connect(
                NCardGrid.SignalName.HolderPressed,
                Callable.From<NCardHolder>(OnRosterCardPressed));
            _grid.Connect(
                NCardGrid.SignalName.HolderAltPressed,
                Callable.From<NCardHolder>(OnRosterCardPressed));
            _body.AddChild(_grid);
            SetGridCardsDeferred(_grid, _displayCards);
        }

        AddFooterButton(
            "关闭",
            0.41f,
            0.59f,
            Close,
            accent: false,
            isDefault: true);
    }

    private void ShowDetail(PhantomRosterEntry entry)
    {
        PhantomRosterEntry? current = _relic.GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(candidate =>
                candidate?.InstanceId == entry.InstanceId);
        if (current == null)
        {
            ShowRoster();
            return;
        }

        _view = RosterView.Detail;
        _targetEntry = current;
        _unrestrictedUpgradePreview = null;
        _selectedMaterialIds.Clear();
        _selectedEffectTypes.Clear();
        _consumeFullFusion = false;
        ClearDynamicUi();

        PhantomChoiceHbrgoSaki card = CreateDisplayCard(current.Value);
        _displayCards.Add(card);
        SetHeader(
            card.Title,
            "怪物牌详情。经验、基础等级与融合效果保留在本局召唤列表中；临时等级仅本场战斗有效。");

        Control cardSlot = CreateCenteredSlot(0.29f, 0.5f);
        _body.AddChild(cardSlot);
        AddPreviewCard(cardSlot, card, 1.05f);

        ScrollContainer detailsScroll = new()
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.06f,
            AnchorRight = 0.91f,
            AnchorBottom = 0.94f,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _body.AddChild(detailsScroll);

        VBoxContainer details = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 0f)
        };
        details.AddThemeConstantOverride("separation", 9);
        detailsScroll.AddChild(details);

        Label level = CreateLabel(
            $"等级  {card.LevelDescription}",
            30,
            new Color(1f, 0.78f, 0.34f));
        details.AddChild(level);

        Label experience = CreateLabel(
            $"经验  {current.Value.ExperienceIntoLevel} / " +
            current.Value.ExperienceRequiredForNextLevel +
            $"    累计 {current.Value.Experience}",
            21,
            new Color(0.83f, 0.86f, 0.9f));
        details.AddChild(experience);
        details.AddChild(CreateExperienceBar(current.Value));

        Label statsTitle = CreateLabel(
            "成长与基础属性",
            26,
            new Color(0.98f, 0.56f, 0.7f));
        details.AddChild(statsTitle);

        RichTextLabel stats = CreateRichTextLabel(
            card.GetBaseStatsRichText(
                _relic.Owner?.GetRelic<ElectronicSoldierManual>()),
            21,
            Colors.White);
        stats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stats.CustomMinimumSize = new Vector2(0f, 255f);
        details.AddChild(stats);

        string fusionEffect = GetFusionEffectText(current.Value);
        if (!string.IsNullOrEmpty(fusionEffect))
        {
            Label effectTitle = CreateLabel(
                "融合效果",
                25,
                new Color(0.62f, 0.84f, 1f));
            details.AddChild(effectTitle);

            Label effect = CreateLabel(
                fusionEffect,
                20,
                Colors.White);
            effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            details.AddChild(effect);
        }

        AddFooterButton(
            "返回列表",
            0.12f,
            0.3f,
            ShowRoster,
            accent: false,
            isDefault: true);
        AddFooterButton(
            "融合",
            0.41f,
            0.59f,
            ShowUpgrade,
            accent: true,
            disabled: !_relic.CanFuseRosterEntries ||
                _relic.GetRosterEntries().Count <= 1);
        AddFooterButton(
            "吞噬",
            0.7f,
            0.88f,
            ShowDevour,
            accent: true,
            disabled: !_relic.CanFuseRosterEntries ||
                _relic.GetRosterEntries().Count <= 1);
    }

    private void ShowUpgrade()
    {
        _consumeFullFusion = false;
        ShowMaterialSelection(
            RosterView.Upgrade,
            preserveSelection: false);
    }

    private void ShowDevour()
    {
        _consumeFullFusion = false;
        ShowMaterialSelection(
            RosterView.Devour,
            preserveSelection: false);
    }

    private void ReturnToUpgradeSelection() => ShowMaterialSelection(
        RosterView.Upgrade,
        preserveSelection: true);

    private void ShowMaterialSelection(
        RosterView materialView,
        bool preserveSelection)
    {
        bool isDevour = materialView == RosterView.Devour;
        if (_targetEntry == null || !_relic.CanFuseRosterEntries)
        {
            ShowRoster();
            return;
        }

        PhantomRosterEntry? current = _relic.GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(candidate =>
                candidate?.InstanceId == _targetEntry.Value.InstanceId);
        if (current == null)
        {
            ShowRoster();
            return;
        }

        _view = materialView;
        _targetEntry = current;
        if (!preserveSelection)
        {
            _selectedMaterialIds.Clear();
        }

        _selectedEffectTypes.Clear();
        _unrestrictedUpgradePreview = null;
        ClearDynamicUi();
        SetHeader(
            isDevour ? "选择吞噬材料" : "选择融合材料",
            isDevour
                ? "吞噬只提供双倍融合素材经验，不获得素材的强化效果。"
                : "材料提供经验与对应的强化效果；已成长材料会额外继承升级和强化效果，不含六感。");

        IReadOnlyList<PhantomRosterEntry> materials =
            _relic.GetRosterEntries()
                .Where(entry =>
                    entry.InstanceId != current.Value.InstanceId)
                .ToArray();
        _selectedMaterialIds.RemoveWhere(id =>
            materials.All(entry => entry.InstanceId != id));
        _displayCards.AddRange(
            materials.Select(material => isDevour
                ? CreateDevourMaterialCard(material)
                : CreateFusionMaterialCard(material)));

        Control gridArea = new()
        {
            AnchorRight = 0.72f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Pass
        };
        _body.AddChild(gridArea);
        _grid = CreateCardGrid();
        SetFullRect(_grid);
        _grid.Connect(
            NCardGrid.SignalName.HolderPressed,
            Callable.From<NCardHolder>(OnMaterialCardPressed));
        _grid.Connect(
            NCardGrid.SignalName.HolderAltPressed,
            Callable.From<NCardHolder>(OnMaterialCardPressed));
        gridArea.AddChild(_grid);
        SetGridCardsDeferred(
            _grid,
            _displayCards,
            _selectedMaterialIds);

        ColorRect divider = new()
        {
            AnchorLeft = 0.735f,
            AnchorTop = 0.05f,
            AnchorRight = 0.737f,
            AnchorBottom = 0.94f,
            Color = new Color(0.95f, 0.52f, 0.68f, 0.62f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _body.AddChild(divider);

        Label previewTitle = CreateLabel(
            isDevour ? "吞噬后效果" : "融合后效果",
            27,
            new Color(1f, 0.78f, 0.34f));
        previewTitle.AnchorLeft = 0.75f;
        previewTitle.AnchorTop = 0.03f;
        previewTitle.AnchorRight = 0.98f;
        previewTitle.AnchorBottom = 0.1f;
        previewTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(previewTitle);

        _upgradePreviewSlot = CreateCenteredSlot(0.865f, 0.48f);
        _body.AddChild(_upgradePreviewSlot);

        _selectionSummary = CreateLabel(
            string.Empty,
            18,
            new Color(0.83f, 0.85f, 0.87f));
        _selectionSummary.AnchorLeft = 0.75f;
        _selectionSummary.AnchorTop = 0.73f;
        _selectionSummary.AnchorRight = 0.98f;
        _selectionSummary.AnchorBottom = 0.97f;
        _selectionSummary.HorizontalAlignment = HorizontalAlignment.Center;
        _selectionSummary.VerticalAlignment = VerticalAlignment.Center;
        _selectionSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(_selectionSummary);

        AddSelectAllMaterialsCheckBox();
        if (!isDevour)
        {
            AddFullFusionCheckBox();
        }

        AddFooterButton(
            "返回详情",
            0.23f,
            0.41f,
            () => ShowDetail(_targetEntry.Value),
            accent: false,
            isDefault: true);
        _confirmButton = AddFooterButton(
            isDevour ? "确定吞噬" : "确定融合",
            0.59f,
            0.77f,
            isDevour ? ConfirmDevour : ConfirmUpgrade,
            accent: true,
            disabled: true);
        if (!isDevour)
        {
            _confirmButton.AnchorTop = 0.4f;
            _confirmButton.AnchorBottom = 0.98f;
        }

        RefreshUpgradePreview();
    }

    private void OnRosterCardPressed(NCardHolder holder)
    {
        if (holder.CardModel is not PhantomChoiceHbrgoSaki card)
        {
            return;
        }

        PhantomRosterEntry? entry = _relic.GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(candidate =>
                candidate?.InstanceId == card.RosterEntryId);
        if (entry != null)
        {
            ShowDetail(entry.Value);
        }
    }

    private void OnMaterialCardPressed(NCardHolder holder)
    {
        if ((_view != RosterView.Upgrade &&
             _view != RosterView.Devour) ||
            holder.CardModel is not PhantomChoiceHbrgoSaki card)
        {
            return;
        }

        if (_selectedMaterialIds.Add(card.RosterEntryId))
        {
            _grid.HighlightCard(card);
        }
        else
        {
            _selectedMaterialIds.Remove(card.RosterEntryId);
            _grid.UnhighlightCard(card);
        }

        RefreshUpgradePreview();
    }

    private void AddFullFusionCheckBox()
    {
        CheckBox checkBox = new()
        {
            Text = "消耗完整融合次数（继承100%）",
            AnchorLeft = 0.55f,
            AnchorTop = -0.12f,
            AnchorRight = 0.81f,
            AnchorBottom = 0.3f,
            ButtonPressed = _consumeFullFusion,
            Alignment = HorizontalAlignment.Center,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        checkBox.AddThemeFontSizeOverride("font_size", 18);
        checkBox.AddThemeColorOverride("font_color", Colors.White);
        checkBox.AddThemeColorOverride(
            "font_disabled_color",
            new Color(0.52f, 0.54f, 0.56f));
        checkBox.Toggled += OnFullFusionToggled;
        _footer.AddChild(checkBox);
        _consumeFullFusionCheckBox = checkBox;
        RefreshFullFusionCheckBoxState();
    }

    private void AddSelectAllMaterialsCheckBox()
    {
        CheckBox checkBox = new()
        {
            Text = "全选",
            TooltipText = "勾选全部材料幻影，取消勾选则清空选择。",
            AnchorLeft = 0.02f,
            AnchorTop = 0.18f,
            AnchorRight = 0.19f,
            AnchorBottom = 0.88f,
            Alignment = HorizontalAlignment.Center,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        checkBox.AddThemeFontSizeOverride("font_size", 22);
        checkBox.AddThemeColorOverride("font_color", Colors.White);
        checkBox.AddThemeColorOverride(
            "font_disabled_color", new Color(0.52f, 0.54f, 0.56f));
        checkBox.Toggled += OnSelectAllMaterialsToggled;
        _footer.AddChild(checkBox);
        _selectAllMaterialsCheckBox = checkBox;
        RefreshSelectAllMaterialsCheckBoxState();
    }

    private void OnSelectAllMaterialsToggled(bool selected)
    {
        if ((_view != RosterView.Upgrade && _view != RosterView.Devour) ||
            _grid == null) return;

        // Include off-screen cards in the virtual grid. Only update changed
        // selections: native HighlightCard stores duplicates if called twice.
        foreach (PhantomChoiceHbrgoSaki card in _displayCards)
        {
            if (selected)
            {
                if (_selectedMaterialIds.Add(card.RosterEntryId))
                    _grid.HighlightCard(card);
            }
            else if (_selectedMaterialIds.Remove(card.RosterEntryId))
            {
                _grid.UnhighlightCard(card);
            }
        }
        if (!selected) _selectedMaterialIds.Clear();
        RefreshUpgradePreview();
    }

    private void RefreshSelectAllMaterialsCheckBoxState()
    {
        if (_selectAllMaterialsCheckBox == null) return;
        _selectAllMaterialsCheckBox.Disabled = _displayCards.Count == 0;
        _selectAllMaterialsCheckBox.SetPressedNoSignal(
            _displayCards.Count > 0 && _selectedMaterialIds.SetEquals(
                _displayCards.Select(card => card.RosterEntryId)));
    }

    private bool RefreshFullFusionCheckBoxState()
    {
        if (_consumeFullFusionCheckBox == null)
        {
            return false;
        }

        bool hasInheritableMaterial = _targetEntry != null &&
            _relic.GetRosterEntries().Any(entry =>
                _selectedMaterialIds.Contains(entry.InstanceId) &&
                entry.HasEarnedUpgradeEffects);
        bool canConsume =
            !_relic.HasUsedFullUpgradeInheritance &&
            hasInheritableMaterial;
        bool changed = false;
        if (!canConsume && _consumeFullFusion)
        {
            changed = true;
            _consumeFullFusion = false;
            _updatingFullFusionCheckBox = true;
            _consumeFullFusionCheckBox.ButtonPressed = false;
            _updatingFullFusionCheckBox = false;
        }

        _consumeFullFusionCheckBox.Disabled = !canConsume;
        _consumeFullFusionCheckBox.Text =
            _relic.HasUsedFullUpgradeInheritance
                ? "完整融合次数已消耗"
                : _consumeFullFusion
                    ? "✓ 消耗完整融合次数（继承100%）"
                    : "消耗完整融合次数（继承100%）";
        _consumeFullFusionCheckBox.TooltipText = canConsume
            ? "勾选后，本次升级与强化效果按100%继承，并消耗整局唯一的完整融合次数。"
            : _relic.HasUsedFullUpgradeInheritance
                ? "本局的完整融合次数已经消耗。"
                : "所选材料没有可完整继承的升级或强化效果。";
        return changed;
    }

    private void OnFullFusionToggled(bool enabled)
    {
        if (_updatingFullFusionCheckBox)
        {
            return;
        }

        _consumeFullFusion = enabled;
        RefreshFullFusionCheckBoxState();
        if (_view == RosterView.EffectSelection &&
            _targetEntry != null &&
            _relic.TryGetUpgradePreview(
                _targetEntry.Value.InstanceId,
                _selectedMaterialIds,
                _consumeFullFusion,
                out PhantomRosterEntry preview,
                out _))
        {
            ShowFusionEffectSelection(
                preview,
                preserveSelection: true);
            return;
        }

        if (_view == RosterView.Upgrade)
        {
            RefreshFusionMaterialCards();
            RefreshUpgradePreview();
        }
    }

    private void RefreshFusionMaterialCards()
    {
        if (_view != RosterView.Upgrade ||
            _targetEntry == null ||
            _grid == null)
        {
            return;
        }

        _displayCards.Clear();
        _displayCards.AddRange(
            _relic.GetRosterEntries()
                .Where(entry =>
                    entry.InstanceId !=
                    _targetEntry.Value.InstanceId)
                .Select(CreateFusionMaterialCard));
        SetGridCardsDeferred(
            _grid,
            _displayCards,
            _selectedMaterialIds);
    }

    private void RefreshUpgradePreview()
    {
        RefreshSelectAllMaterialsCheckBoxState();
        if (_targetEntry == null || _upgradePreviewSlot == null)
        {
            return;
        }

        bool fullFusionSelectionChanged =
            RefreshFullFusionCheckBoxState();
        if (fullFusionSelectionChanged && _view == RosterView.Upgrade)
        {
            RefreshFusionMaterialCards();
        }

        foreach (Node child in _upgradePreviewSlot.GetChildren())
        {
            _upgradePreviewSlot.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }

        bool isDevour = _view == RosterView.Devour;
        PhantomRosterEntry calculatedPreview = default;
        bool usesFullInheritance = false;
        bool hasPreview = _selectedMaterialIds.Count > 0 &&
            (isDevour
                ? _relic.TryGetDevourPreview(
                    _targetEntry.Value.InstanceId,
                    _selectedMaterialIds,
                    out calculatedPreview)
                : _relic.TryGetUpgradePreview(
                    _targetEntry.Value.InstanceId,
                    _selectedMaterialIds,
                    _consumeFullFusion,
                    out calculatedPreview,
                    out usesFullInheritance));
        PhantomRosterEntry previewEntry = hasPreview
            ? calculatedPreview
            : _targetEntry.Value;
        PhantomChoiceHbrgoSaki previewCard =
            CreateDisplayCard(previewEntry);
        AddPreviewCard(_upgradePreviewSlot, previewCard, 0.78f);

        if (!hasPreview)
        {
            _selectionSummary.Text = _selectedMaterialIds.Count == 0
                ? "尚未选择材料"
                : isDevour
                    ? "无法计算本次吞噬，请重新选择材料。"
                    : "无法计算本次融合，请重新选择材料。";
            _confirmButton.Disabled = true;
            return;
        }

        int experienceGain = Math.Max(
            0,
            previewEntry.Experience - _targetEntry.Value.Experience);
        if (isDevour)
        {
            _selectionSummary.Text =
                $"材料 {_selectedMaterialIds.Count} 张  双倍经验 +{experienceGain}\n" +
                $"等级 Lv.{_targetEntry.Value.Level} → Lv.{previewEntry.Level}\n" +
                "不获得素材的强化效果、融合次数或成长属性";
            _confirmButton.Disabled = false;
            return;
        }

        bool hasInheritableMaterial = _relic.GetRosterEntries()
            .Any(entry =>
                _selectedMaterialIds.Contains(entry.InstanceId) &&
                entry.HasEarnedUpgradeEffects);
        string inheritanceText = hasInheritableMaterial
            ? usesFullInheritance
                ? "消耗完整融合次数：升级/强化效果继承100%（不含六感）"
                : "不消耗完整融合次数：升级/强化效果继承50%（不含六感）"
            : "材料没有升级或强化效果，无额外继承";
        int effectCount = PhantomFusionEffectHelper
            .GetEffectTypes(previewEntry.FusionEffectData)
            .Count;
        string effectLimitText = effectCount >
            FusionSlotCount
                ? $"\n强化效果 {effectCount} 种，确认后请选择最多保留{FusionSlotCount}种"
                : $"\n强化效果 {effectCount}/" +
                  FusionSlotCount;
        _selectionSummary.Text =
            $"材料 {_selectedMaterialIds.Count} 张  经验 +{experienceGain}\n" +
            $"等级 Lv.{_targetEntry.Value.Level} → Lv.{previewEntry.Level}\n" +
            $"伤害增幅 {_targetEntry.Value.DamageBonusPercent:0.##}% → " +
            $"{previewEntry.DamageBonusPercent:0.##}%\n" +
            $"伤害减免 {_targetEntry.Value.DamageReductionPercent:0.##}% → " +
            $"{previewEntry.DamageReductionPercent:0.##}%\n" +
            $"召唤者出目 +{_targetEntry.Value.SummonerRollBonus:0.##} → " +
            $"+{previewEntry.SummonerRollBonus:0.##}\n" +
            $"额外生命 {_targetEntry.Value.BonusMaxHp:0.##} → " +
            $"{previewEntry.BonusMaxHp:0.##}\n" +
            inheritanceText + effectLimitText;
        _confirmButton.Disabled = false;
    }

    private void ConfirmUpgrade()
    {
        if (_targetEntry == null ||
            _selectedMaterialIds.Count == 0)
        {
            return;
        }

        if (!_relic.TryGetUpgradePreview(
                _targetEntry.Value.InstanceId,
                _selectedMaterialIds,
                _consumeFullFusion,
                out PhantomRosterEntry preview,
                out _))
        {
            ShowRequestError("融合");
            return;
        }

        IReadOnlyList<PhantomFusionEffectType> effectTypes =
            PhantomFusionEffectHelper.GetEffectTypes(
                preview.FusionEffectData);
        if (effectTypes.Count >
            FusionSlotCount)
        {
            ShowFusionEffectSelection(preview);
            return;
        }

        bool requested = _relic.RequestUpgradeRosterEntry(
            _targetEntry.Value.InstanceId,
            _selectedMaterialIds.ToArray(),
            effectTypes,
            _consumeFullFusion);
        if (requested)
        {
            Close();
            return;
        }

        ShowRequestError("融合");
    }

    private void ConfirmDevour()
    {
        if (_targetEntry == null ||
            _selectedMaterialIds.Count == 0)
        {
            return;
        }

        bool requested = _relic.RequestDevourRosterEntries(
            _targetEntry.Value.InstanceId,
            _selectedMaterialIds.ToArray());
        if (requested)
        {
            Close();
            return;
        }

        ShowRequestError("吞噬");
    }

    private void ShowRequestError(string action)
    {
        _selectionSummary.Text =
            $"{action}请求未能提交，请重新打开列表后再试。";
        _selectionSummary.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            new Color(1f, 0.48f, 0.45f));
    }

    private void ShowFusionEffectSelection(
        PhantomRosterEntry unrestrictedPreview,
        bool preserveSelection = false)
    {
        if (_targetEntry == null)
        {
            ShowRoster();
            return;
        }

        _view = RosterView.EffectSelection;
        _unrestrictedUpgradePreview = unrestrictedPreview;
        IReadOnlyList<PhantomFusionEffectType> currentTypes =
            PhantomFusionEffectHelper.GetEffectTypes(
                _targetEntry.Value.FusionEffectData);
        IReadOnlyList<PhantomFusionEffectType> allTypes =
            PhantomFusionEffectHelper.GetEffectTypes(
                unrestrictedPreview.FusionEffectData);
        if (preserveSelection)
        {
            HashSet<PhantomFusionEffectType> available =
                allTypes.ToHashSet();
            _selectedEffectTypes.RemoveWhere(type =>
                !available.Contains(type));
        }
        else
        {
            _selectedEffectTypes.Clear();
            foreach (PhantomFusionEffectType type in currentTypes)
            {
                if (_selectedEffectTypes.Count >=
                    FusionSlotCount)
                {
                    break;
                }

                _selectedEffectTypes.Add(type);
            }

            foreach (PhantomFusionEffectType type in allTypes)
            {
                if (_selectedEffectTypes.Count >=
                    FusionSlotCount)
                {
                    break;
                }

                _selectedEffectTypes.Add(type);
            }
        }

        ClearDynamicUi();
        SetHeader(
            "选择保留的强化效果",
            $"此幻影最多拥有{FusionSlotCount}种不同强化效果。高亮色表示已选定；取消已有项并选择新增项可进行替换，未选中的新增项不会应用。");

        ScrollContainer effectScroll = new()
        {
            AnchorLeft = 0.03f,
            AnchorTop = 0.04f,
            AnchorRight = 0.69f,
            AnchorBottom = 0.96f,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _body.AddChild(effectScroll);

        VBoxContainer effectList = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 0f)
        };
        effectList.AddThemeConstantOverride("separation", 8);
        effectScroll.AddChild(effectList);

        HashSet<PhantomFusionEffectType> currentTypeSet =
            currentTypes.ToHashSet();
        IEnumerable<PhantomFusionEffectType> orderedTypes =
            currentTypes.Concat(
                allTypes.Where(type => !currentTypeSet.Contains(type)));
        foreach (PhantomFusionEffectType type in orderedTypes)
        {
            bool isCurrent = currentTypeSet.Contains(type);
            string description =
                PhantomFusionEffectHelper.GetEffectDescription(
                    unrestrictedPreview.FusionEffectData,
                    type);
            bool isSelected = _selectedEffectTypes.Contains(type);
            Button option = CreateButton(
                GetEffectSelectionOptionText(
                    isCurrent,
                    description),
                accent: isSelected);
            option.ToggleMode = true;
            option.ButtonPressed = isSelected;
            option.Alignment = HorizontalAlignment.Left;
            option.CustomMinimumSize = new Vector2(0f, 62f);
            option.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            option.TooltipText = description;
            option.Toggled += pressed =>
                OnEffectTypeToggled(
                    type,
                    isCurrent,
                    description,
                    pressed,
                    option);
            effectList.AddChild(option);
        }

        Label previewTitle = CreateLabel(
            "保留后效果",
            27,
            new Color(1f, 0.78f, 0.34f));
        previewTitle.AnchorLeft = 0.72f;
        previewTitle.AnchorTop = 0.03f;
        previewTitle.AnchorRight = 0.98f;
        previewTitle.AnchorBottom = 0.1f;
        previewTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _body.AddChild(previewTitle);

        _upgradePreviewSlot = CreateCenteredSlot(0.85f, 0.47f);
        _body.AddChild(_upgradePreviewSlot);

        _selectionSummary = CreateLabel(
            string.Empty,
            18,
            new Color(0.83f, 0.85f, 0.87f));
        _selectionSummary.AnchorLeft = 0.72f;
        _selectionSummary.AnchorTop = 0.72f;
        _selectionSummary.AnchorRight = 0.98f;
        _selectionSummary.AnchorBottom = 0.97f;
        _selectionSummary.HorizontalAlignment = HorizontalAlignment.Center;
        _selectionSummary.VerticalAlignment = VerticalAlignment.Center;
        _selectionSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(_selectionSummary);

        AddFullFusionCheckBox();

        AddFooterButton(
            "返回材料",
            0.23f,
            0.41f,
            ReturnToUpgradeSelection,
            accent: false,
            isDefault: true);
        _confirmButton = AddFooterButton(
            "应用并融合",
            0.59f,
            0.77f,
            ConfirmEffectSelection,
            accent: true);
        _confirmButton.AnchorTop = 0.4f;
        _confirmButton.AnchorBottom = 0.98f;
        RefreshEffectSelectionPreview();
    }

    private void OnEffectTypeToggled(
        PhantomFusionEffectType type,
        bool isCurrent,
        string description,
        bool pressed,
        Button option)
    {
        if (_updatingEffectButtons)
        {
            return;
        }

        if (pressed && !_selectedEffectTypes.Contains(type))
        {
            if (_selectedEffectTypes.Count >=
                FusionSlotCount)
            {
                _updatingEffectButtons = true;
                option.ButtonPressed = false;
                _updatingEffectButtons = false;
                ApplyButtonAccent(option, accent: false);
                option.Text = GetEffectSelectionOptionText(
                    isCurrent,
                    description);
                _selectionSummary.Text =
                    $"最多保留{FusionSlotCount}种强化效果。请先取消一个已选效果。";
                return;
            }

            _selectedEffectTypes.Add(type);
        }
        else if (!pressed)
        {
            _selectedEffectTypes.Remove(type);
        }

        ApplyButtonAccent(option, pressed);
        option.Text = GetEffectSelectionOptionText(
            isCurrent,
            description);
        RefreshEffectSelectionPreview();
    }

    private static string GetEffectSelectionOptionText(
        bool isCurrent,
        string description)
    {
        string state = isCurrent ? "已有" : "新增";
        return $"{state}  {description}";
    }

    private void RefreshEffectSelectionPreview()
    {
        if (_targetEntry == null ||
            _unrestrictedUpgradePreview == null ||
            _upgradePreviewSlot == null)
        {
            return;
        }

        foreach (Node child in _upgradePreviewSlot.GetChildren())
        {
            _upgradePreviewSlot.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }

        bool hasPreview = _relic.TryGetLimitedUpgradePreview(
            _targetEntry.Value.InstanceId,
            _selectedMaterialIds,
            _selectedEffectTypes,
            _consumeFullFusion,
            out PhantomRosterEntry preview,
            out _);
        PhantomRosterEntry displayed = hasPreview
            ? preview
            : _targetEntry.Value;
        AddPreviewCard(
            _upgradePreviewSlot,
            CreateDisplayCard(displayed),
            0.78f);

        int availableCount = PhantomFusionEffectHelper.GetEffectTypes(
                _unrestrictedUpgradePreview.Value.FusionEffectData)
            .Count;
        int ignoredCount = Math.Max(
            0,
            availableCount - _selectedEffectTypes.Count);
        int minimumRetainedCount = Math.Min(
            FusionSlotCount,
            PhantomFusionEffectHelper.GetEffectTypes(
                    _targetEntry.Value.FusionEffectData)
                .Count);
        bool preservesExistingCount =
            _selectedEffectTypes.Count >= minimumRetainedCount;
        _selectionSummary.Text = !preservesExistingCount
            ? $"至少保留 {minimumRetainedCount} 种效果；" +
              "移除已有项时，请选择等量新增项进行替换。"
            : hasPreview
                ? $"已保留 {_selectedEffectTypes.Count}/" +
                  $"{FusionSlotCount} 种\n" +
                  $"不应用 {ignoredCount} 种\n" +
                  "取消已有效果后，可选择新增效果进行替换"
                : "无法计算强化效果，请返回材料选择后重试。";
        _confirmButton.Disabled =
            !hasPreview || !preservesExistingCount;
    }

    private void ConfirmEffectSelection()
    {
        if (_targetEntry == null ||
            _selectedMaterialIds.Count == 0 ||
            _selectedEffectTypes.Count >
                FusionSlotCount ||
            _selectedEffectTypes.Count < Math.Min(
                FusionSlotCount,
                PhantomFusionEffectHelper.GetEffectTypes(
                        _targetEntry.Value.FusionEffectData)
                    .Count))
        {
            return;
        }

        bool requested = _relic.RequestUpgradeRosterEntry(
            _targetEntry.Value.InstanceId,
            _selectedMaterialIds.ToArray(),
            _selectedEffectTypes.ToArray(),
            _consumeFullFusion);
        if (requested)
        {
            Close();
            return;
        }

        ShowRequestError("融合");
    }

    private void ClearDynamicUi()
    {
        foreach (Node child in _body.GetChildren())
        {
            _body.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }

        foreach (Node child in _footer.GetChildren())
        {
            _footer.RemoveChildSafely(child);
            child.QueueFreeSafely();
        }

        _displayCards.Clear();
        _grid = null;
        _selectionSummary = null;
        _upgradePreviewSlot = null;
        _confirmButton = null;
        _consumeFullFusionCheckBox = null;
        _selectAllMaterialsCheckBox = null;
        _defaultButton = null;
    }

    private void SetHeader(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
    }

    private PhantomChoiceHbrgoSaki CreateDisplayCard(
        PhantomRosterEntry entry)
    {
        PhantomChoiceHbrgoSaki card =
            (PhantomChoiceHbrgoSaki)ModelDb
                .Card<PhantomChoiceHbrgoSaki>()
                .ToMutable();
        card.SetRosterEntry(entry);
        card.Owner = _relic.Owner;
        return card;
    }

    private PhantomChoiceHbrgoSaki CreateFusionMaterialCard(
        PhantomRosterEntry material)
    {
        PhantomChoiceHbrgoSaki card = CreateDisplayCard(material);
        if (_targetEntry is not { } target ||
            !_relic.TryGetUpgradePreview(
                target.InstanceId,
                new[] { material.InstanceId },
                _consumeFullFusion &&
                    material.HasEarnedUpgradeEffects,
                out PhantomRosterEntry preview,
                out _))
        {
            return card;
        }

        card.SetDescriptionOverride(
            GetFusionMaterialEffectText(target, preview));
        return card;
    }

    private PhantomChoiceHbrgoSaki CreateDevourMaterialCard(
        PhantomRosterEntry material)
    {
        PhantomChoiceHbrgoSaki card = CreateDisplayCard(material);
        card.SetDescriptionOverride(
            "[gold]吞噬后效果[/gold]：\n" +
            "获得该素材可提供经验的2倍。\n" +
            "不会获得素材的强化效果、融合次数或成长属性。");
        return card;
    }

    private static NCardGrid CreateCardGrid()
    {
        NCardGrid grid = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("cards/card_grid"))
            .Instantiate<NCardGrid>(PackedScene.GenEditState.Disabled);
        grid.ClipContents = true;
        return grid;
    }

    private static void SetGridCardsDeferred(
        NCardGrid grid,
        IReadOnlyList<PhantomChoiceHbrgoSaki> cards,
        IReadOnlyCollection<int> selectedEntryIds = null)
    {
        PhantomChoiceHbrgoSaki[] snapshot = cards.OrderByPhantomGrowth().ToArray();
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(grid) ||
                !grid.IsInsideTree())
            {
                return;
            }

            grid.SetCards(
                snapshot,
                PileType.None,
                new List<SortingOrders> { SortingOrders.Ascending });
            // Selection may change before this deferred rebuild runs. Read the
            // current set and normalize highlights instead of restoring a stale
            // snapshot (or adding a second highlight after a quick select-all).
            HashSet<int> selected = selectedEntryIds?.ToHashSet() ?? new();
            foreach (PhantomChoiceHbrgoSaki card in snapshot)
            {
                grid.UnhighlightCard(card);
                if (selected.Contains(card.RosterEntryId))
                    grid.HighlightCard(card);
            }
        }).CallDeferred();
    }

    private static Control CreateCenteredSlot(float anchorX, float anchorY)
    {
        return new Control
        {
            AnchorLeft = anchorX,
            AnchorTop = anchorY,
            AnchorRight = anchorX,
            AnchorBottom = anchorY,
            MouseFilter = MouseFilterEnum.Pass
        };
    }

    private static void AddPreviewCard(
        Control slot,
        PhantomChoiceHbrgoSaki card,
        float scale)
    {
        NCard cardNode = NCard.Create(card);
        NPreviewCardHolder holder = NPreviewCardHolder.Create(
            cardNode,
            showHoverTips: true,
            scaleOnHover: false);
        if (cardNode == null || holder == null)
        {
            return;
        }

        holder.SetClickable(isClickable: false);
        holder.SetCardScale(Vector2.One * scale);
        slot.AddChild(holder);
        cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
    }

    private Button AddFooterButton(
        string text,
        float anchorLeft,
        float anchorRight,
        Action pressed,
        bool accent,
        bool disabled = false,
        bool isDefault = false)
    {
        Button button = CreateButton(text, accent);
        button.AnchorLeft = anchorLeft;
        button.AnchorTop = 0.18f;
        button.AnchorRight = anchorRight;
        button.AnchorBottom = 0.88f;
        button.Disabled = disabled;
        button.Pressed += pressed;
        _footer.AddChild(button);
        if (isDefault)
        {
            _defaultButton = button;
        }

        return button;
    }

    private static ProgressBar CreateExperienceBar(
        PhantomRosterEntry entry)
    {
        ProgressBar bar = new()
        {
            MinValue = 0d,
            MaxValue = Math.Max(
                1,
                entry.ExperienceRequiredForNextLevel),
            Value = entry.ExperienceIntoLevel,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, 28f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride(
            "background",
            new StyleBoxFlat
            {
                BgColor = new Color(0.09f, 0.11f, 0.14f, 0.98f),
                BorderColor = new Color(0.32f, 0.36f, 0.42f),
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4
            });
        bar.AddThemeStyleboxOverride(
            "fill",
            new StyleBoxFlat
            {
                BgColor = new Color(0.95f, 0.45f, 0.68f, 0.98f),
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            });
        return bar;
    }

    private static string GetFusionEffectText(
        PhantomRosterEntry entry)
    {
        List<string> effects = new();
        if (entry.LevelUps > 0 ||
            entry.InheritedLevelDamageBonusPercent > 0m ||
            entry.InheritedLevelDamageReductionPercent > 0m ||
            entry.InheritedLevelBonusMaxHp > 0m ||
            entry.InheritedLevelSummonerRollBonus > 0m)
        {
            effects.Add(
                "升级效果：每升1级，伤害增幅+10%、伤害减免+5%、最大生命值+5、" +
                $"在场时召唤者掷骰出目+{PhantomRosterEntry.LevelSummonerRollBonus:0.##}。伤害减免合计上限50%。");
        }

        effects.AddRange(
            PhantomFusionEffectHelper.GetDescriptionLines(
                entry.FusionEffectData));

        return string.Join('\n', effects);
    }

    private static string GetFusionMaterialEffectText(
        PhantomRosterEntry target,
        PhantomRosterEntry preview)
    {
        List<string> effects = new();
        List<string> inheritedStats = new();
        AddPositiveDifference(
            inheritedStats,
            "伤害增幅",
            preview.EarnedLevelDamageBonusPercent,
            target.EarnedLevelDamageBonusPercent,
            "%");
        AddPositiveDifference(
            inheritedStats,
            "伤害减免",
            preview.EarnedLevelDamageReductionPercent,
            target.EarnedLevelDamageReductionPercent,
            "%");
        AddPositiveDifference(
            inheritedStats,
            "最大生命值",
            preview.EarnedLevelBonusMaxHp,
            target.EarnedLevelBonusMaxHp);
        AddPositiveDifference(
            inheritedStats,
            "召唤者掷骰出目",
            preview.SummonerRollBonus,
            target.SummonerRollBonus);
        if (inheritedStats.Count > 0)
        {
            effects.Add(
                "升级与继承：" +
                string.Join("，", inheritedStats) +
                "。");
        }

        HashSet<string> currentEffects =
            PhantomFusionEffectHelper.GetDescriptionLines(
                    target.FusionEffectData)
                .ToHashSet(StringComparer.Ordinal);
        effects.AddRange(
            PhantomFusionEffectHelper.GetDescriptionLines(
                    preview.FusionEffectData)
                .Where(effect => !currentEffects.Contains(effect)));

        if (effects.Count == 0)
        {
            effects.Add("本次融合仅提供经验。");
        }

        return "[gold]融合后效果[/gold]：\n" +
            string.Join('\n', effects);
    }

    private static void AddPositiveDifference(
        ICollection<string> parts,
        string label,
        decimal after,
        decimal before,
        string suffix = "")
    {
        decimal difference = Math.Max(0m, after - before);
        if (difference <= 0m)
        {
            return;
        }

        parts.Add(
            $"{label}+" +
            PhantomChoiceHbrgoSaki.FormatNumber(difference) +
            suffix);
    }

    private static Button CreateButton(string text, bool accent)
    {
        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 22);
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride(
            "font_disabled_color",
            new Color(0.52f, 0.54f, 0.56f));
        ApplyButtonAccent(button, accent);
        button.AddThemeStyleboxOverride(
            "disabled",
            CreateButtonStyle(
                new Color(0.09f, 0.1f, 0.11f, 0.9f),
                new Color(0.26f, 0.28f, 0.3f)));
        return button;
    }

    private static void ApplyButtonAccent(Button button, bool accent)
    {
        Color normal = accent
            ? new Color(0.62f, 0.16f, 0.32f, 0.98f)
            : new Color(0.16f, 0.19f, 0.22f, 0.98f);
        Color border = accent
            ? new Color(1f, 0.6f, 0.72f)
            : new Color(0.58f, 0.62f, 0.66f);
        button.AddThemeStyleboxOverride(
            "normal",
            CreateButtonStyle(normal, border));
        button.AddThemeStyleboxOverride(
            "hover",
            CreateButtonStyle(normal.Lightened(0.12f), border.Lightened(0.12f)));
        button.AddThemeStyleboxOverride(
            "pressed",
            CreateButtonStyle(
                accent ? normal : normal.Darkened(0.12f),
                accent ? border : Colors.White));
        button.AddThemeStyleboxOverride(
            "focus",
            CreateButtonStyle(normal, Colors.White));
    }

    private static StyleBoxFlat CreateButtonStyle(
        Color background,
        Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginBottom = 10f,
            ContentMarginLeft = 20f,
            ContentMarginRight = 20f,
            ContentMarginTop = 10f
        };
    }

    private static Label CreateLabel(
        string text,
        int fontSize,
        Color color)
    {
        Label label = new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride(
            ThemeConstants.Label.FontSize,
            fontSize);
        label.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            color);
        label.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            new Color(0.01f, 0.012f, 0.016f));
        label.AddThemeConstantOverride(
            ThemeConstants.Label.OutlineSize,
            6);
        return label;
    }

    private static RichTextLabel CreateRichTextLabel(
        string text,
        int fontSize,
        Color color)
    {
        RichTextLabel label = new()
        {
            BbcodeEnabled = true,
            Text = text,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride(
            ThemeConstants.RichTextLabel.NormalFontSize,
            fontSize);
        label.AddThemeColorOverride(
            ThemeConstants.RichTextLabel.DefaultColor,
            color);
        label.AddThemeColorOverride(
            ThemeConstants.RichTextLabel.FontOutlineColor,
            new Color(0.01f, 0.012f, 0.016f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    private void Close()
    {
        NOverlayStack.Instance?.Remove(this);
    }

    private void HideCombatWidgets()
    {
        NCombatUi combatUi = NCombatRoom.Instance?.Ui;
        if (combatUi == null)
        {
            return;
        }

        if (!_overclockGaugeVisibilityCaptured &&
            HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                combatUi,
                HBRGOSakiCombatUiIntegration.GaugeAttachmentId,
                out NOverclockGauge gauge))
        {
            _overclockGauge = gauge;
            _overclockGaugeWasVisible = gauge.Visible;
            _overclockGaugeVisibilityCaptured = true;
        }

        if (!_rollHistoryVisibilityCaptured &&
            HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                combatUi,
                HBRGOSakiCombatUiIntegration.RollHistoryWidgetAttachmentId,
                out NRollHistoryWidget historyWidget))
        {
            _rollHistoryWidget = historyWidget;
            _rollHistoryWasVisible = historyWidget.Visible;
            _rollHistoryVisibilityCaptured = true;
        }

        if (GodotObject.IsInstanceValid(_overclockGauge))
        {
            _overclockGauge.Hide();
        }

        if (GodotObject.IsInstanceValid(_rollHistoryWidget))
        {
            _rollHistoryWidget.Hide();
        }
    }

    private void RestoreCombatWidgets()
    {
        if (_overclockGaugeVisibilityCaptured &&
            GodotObject.IsInstanceValid(_overclockGauge))
        {
            _overclockGauge.Visible = _overclockGaugeWasVisible;
        }

        if (_rollHistoryVisibilityCaptured &&
            GodotObject.IsInstanceValid(_rollHistoryWidget))
        {
            _rollHistoryWidget.Visible = _rollHistoryWasVisible;
        }

        _overclockGauge = null;
        _overclockGaugeVisibilityCaptured = false;
        _rollHistoryWidget = null;
        _rollHistoryVisibilityCaptured = false;
    }

    private void HideRelicInventory()
    {
        if (_relicInventoryHidden ||
            NRun.Instance?.GlobalUi?.RelicInventory is not { } inventory)
        {
            return;
        }

        inventory.HideImmediately();
        _relicInventoryHidden = true;
    }

    private void RestoreRelicInventory()
    {
        if (!_relicInventoryHidden)
        {
            return;
        }

        NRun.Instance?.GlobalUi?.RelicInventory?.AnimShow();
        _relicInventoryHidden = false;
    }

    private static void SetFullRect(Control control)
    {
        control.SetAnchorsPreset(LayoutPreset.FullRect);
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
    }
}
