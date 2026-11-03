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
    private Control _defaultButton;
    private BaseButton _confirmButton;
    private CheckBox _consumeFullFusionCheckBox;
    private CheckBox _selectAllMaterialsCheckBox;
    private GridContainer _grid;
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
        AddPreviewMedallion(previewEntry);

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
            $"额外生命 {_targetEntry.Value.GetBonusMaxHp(_relic.Owner):0.##} → " +
            $"{previewEntry.GetBonusMaxHp(_relic.Owner):0.##}\n" +
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
        AddPreviewMedallion(displayed);

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
        _medallions.Clear();
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
        RefreshHeaderExtras();
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

    private string GetFusionEffectText(
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
                $"升级效果：每升1级，伤害增幅+10%、伤害减免+5%、最大生命值+{HbrResourceValueHelper.Fixed(_relic.Owner?.Creature, PhantomRosterEntry.LevelBonusMaxHp):0.##}、" +
                $"在场时召唤者掷骰出目+{PhantomRosterEntry.LevelSummonerRollBonus:0.##}。伤害减免合计上限50%。");
        }

        effects.AddRange(
            PhantomFusionEffectHelper.GetDescriptionLines(
                entry.FusionEffectData, _relic.Owner?.Creature));

        return string.Join('\n', effects);
    }

    private string GetFusionMaterialEffectText(
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
            HbrResourceValueHelper.Fixed(_relic.Owner?.Creature, preview.EarnedLevelBonusMaxHp),
            HbrResourceValueHelper.Fixed(_relic.Owner?.Creature, target.EarnedLevelBonusMaxHp));
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
                    target.FusionEffectData, _relic.Owner?.Creature)
                .ToHashSet(StringComparer.Ordinal);
        effects.AddRange(
            PhantomFusionEffectHelper.GetDescriptionLines(
                    preview.FusionEffectData, _relic.Owner?.Creature)
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
