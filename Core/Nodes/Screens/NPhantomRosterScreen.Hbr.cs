using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// HBR divergence-style layout for the phantom roster: medallion grids instead of card grids,
// a back button that steps up one view at a time, and HBR buttons. Roster logic, previews and
// requests stay in NPhantomRosterScreen.cs.
public partial class NPhantomRosterScreen
{
    private const float RosterDiameter = 190f;
    private const float MaterialDiameter = 160f;
    private const float PreviewDiameter = 170f;
    private const float GridGap = 24f;

    private readonly Dictionary<int, NPhantomMedallion> _medallions = new();
    private readonly List<(TextureRect Ring, float Speed)> _spinners = new();
    private Label _titleEnglish;
    private HBoxContainer _chips;
    private TextureButton _backButton;
    private HBoxContainer _toolsRow;
    private Label _selectionCount;
    private ScrollContainer _gridScroll;

    private void BuildShell()
    {
        TextureRect sky = new()
        {
            Texture = PhantomRosterArt.Load(PhantomRosterArt.NightSky),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Stop
        };
        SetFullRect(sky);
        AddChild(sky);
        ColorRect dim = new()
        {
            Color = new Color(0.02f, 0.015f, 0.05f, 0.58f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        SetFullRect(dim);
        AddChild(dim);
        AddCenteredRing(PhantomRosterArt.RingLine, 1015f, new Vector2(180f, 40f), 0.028f, 0.45f);
        AddCenteredRing(PhantomRosterArt.RingDots, 640f, new Vector2(180f, 40f), -0.045f, 0.3f);

        _backButton = new TextureButton
        {
            TextureNormal = PhantomRosterArt.Load(PhantomRosterArt.BackButton),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            Position = new Vector2(40f, TopBarInset + 14f),
            Size = new Vector2(104f, 104f),
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = "返回上一层"
        };
        _backButton.MouseEntered += () => _backButton.Modulate = new Color(1.25f, 1.05f, 1.2f);
        _backButton.MouseExited += () => _backButton.Modulate = Colors.White;
        _backButton.Pressed += GoBack;
        AddChild(_backButton);

        _title = PhantomRosterArt.MakeLabel(string.Empty, 44, Colors.White, 6);
        _title.Position = new Vector2(168f, TopBarInset + 12f);
        _title.Size = new Vector2(1180f, 62f);
        AddChild(_title);
        _titleEnglish = PhantomRosterArt.MakeLabel(string.Empty, 17, PhantomRosterArt.Muted, 3);
        _titleEnglish.Position = new Vector2(172f, TopBarInset + 72f);
        _titleEnglish.Size = new Vector2(600f, 26f);
        AddChild(_titleEnglish);
        AddChild(new NinePatchRect
        {
            Texture = PhantomRosterArt.Load(PhantomRosterArt.TitleLine),
            PatchMarginLeft = 2,
            PatchMarginRight = 55,
            Position = new Vector2(168f, TopBarInset + 102f),
            Size = new Vector2(720f, 10f),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _subtitle = PhantomRosterArt.MakeLabel(string.Empty, 19, PhantomRosterArt.Muted, 4);
        _subtitle.Position = new Vector2(170f, TopBarInset + 116f);
        _subtitle.Size = new Vector2(1300f, 52f);
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _subtitle.VerticalAlignment = VerticalAlignment.Top;
        AddChild(_subtitle);

        _chips = new HBoxContainer
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -1180f,
            OffsetRight = -170f,
            OffsetTop = TopBarInset + 34f,
            OffsetBottom = TopBarInset + 84f,
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _chips.AddThemeConstantOverride("separation", 12);
        AddChild(_chips);

        TextureButton closeButton = new()
        {
            TextureNormal = PhantomRosterArt.Load(PhantomRosterArt.CircleButton),
            TextureHover = PhantomRosterArt.Load(PhantomRosterArt.CircleButtonActive),
            TextureFocused = PhantomRosterArt.Load(PhantomRosterArt.CircleButtonActive),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -144f,
            OffsetRight = -40f,
            OffsetTop = TopBarInset + 14f,
            OffsetBottom = TopBarInset + 118f,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = "关闭召唤列表"
        };
        Label cross = PhantomRosterArt.MakeLabel("✕", 40, Colors.White);
        cross.HorizontalAlignment = HorizontalAlignment.Center;
        cross.SetAnchorsPreset(LayoutPreset.FullRect);
        closeButton.AddChild(cross);
        closeButton.Pressed += Close;
        AddChild(closeButton);

        _body = new Control
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 60f,
            OffsetTop = TopBarInset + 176f,
            OffsetRight = -60f,
            OffsetBottom = -168f,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass
        };
        AddChild(_body);
        HBoxContainer footer = new()
        {
            AnchorTop = 1f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 60f,
            OffsetTop = -160f,
            OffsetRight = -60f,
            OffsetBottom = -12f,
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = MouseFilterEnum.Pass
        };
        footer.AddThemeConstantOverride("separation", 40);
        _footer = footer;
        AddChild(_footer);
    }

    private void AddCenteredRing(string path, float size, Vector2 shift, float speed, float alpha)
    {
        TextureRect ring = PhantomRosterArt.Image(path, Vector2.Zero, Vector2.One * size);
        ring.AnchorLeft = ring.AnchorRight = ring.AnchorTop = ring.AnchorBottom = 0.5f;
        ring.OffsetLeft = shift.X - size / 2f;
        ring.OffsetRight = shift.X + size / 2f;
        ring.OffsetTop = shift.Y - size / 2f;
        ring.OffsetBottom = shift.Y + size / 2f;
        ring.Modulate = new Color(1f, 1f, 1f, alpha);
        AddChild(ring);
        _spinners.Add((ring, speed));
    }

    public override void _Process(double delta)
    {
        _spinners.RemoveAll(spinner => !GodotObject.IsInstanceValid(spinner.Ring));
        foreach ((TextureRect ring, float speed) in _spinners)
        {
            ring.PivotOffset = ring.Size / 2f;
            ring.Rotation += (float)delta * speed;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed("ui_cancel"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        GoBack();
    }

    // The back button and Esc step up one view; the close button always closes.
    private void GoBack()
    {
        switch (_view)
        {
            case RosterView.EffectSelection:
                ReturnToUpgradeSelection();
                break;
            case RosterView.Upgrade or RosterView.Devour when _targetEntry != null:
                ShowDetail(_targetEntry.Value);
                break;
            case RosterView.Detail:
                ShowRoster();
                break;
            default:
                Close();
                break;
        }
    }

    private void RefreshHeaderExtras()
    {
        if (_titleEnglish != null)
        {
            _titleEnglish.Text = _view switch
            {
                RosterView.Roster => "PHANTOM ROSTER",
                RosterView.Detail => "PHANTOM DETAIL",
                RosterView.Upgrade => "FUSION",
                RosterView.Devour => "DEVOUR",
                _ => "FUSION EFFECTS"
            };
        }

        if (_backButton != null)
        {
            _backButton.TooltipText = _view == RosterView.Roster ? "关闭召唤列表" : "返回上一层";
        }

        if (_chips == null || _relic == null)
        {
            return;
        }

        foreach (Node child in _chips.GetChildren())
        {
            _chips.RemoveChild(child);
            child.QueueFree();
        }

        IReadOnlyList<PhantomRosterEntry> entries = _relic.GetRosterEntries();
        AddChip($"幻影  {entries.Count}", null, null);
        AddChip(
            _relic.HasUsedFullUpgradeInheritance ? "完整融合次数  已消耗" : "完整融合次数  1",
            null,
            "整局只能使用一次，融合时按100%继承升级与强化效果。");
        PhantomRosterEntry? active = entries
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(entry => entry?.InstanceId == _relic.ActiveRosterEntryId);
        if (active != null)
        {
            AddChip($"召唤中  {CreateDisplayCard(active.Value).Title}", PhantomRosterArt.ActiveStar, null);
        }
    }

    private void AddChip(string text, string icon, string tooltip)
    {
        PanelContainer chip = new() { TooltipText = tooltip ?? string.Empty, MouseFilter = MouseFilterEnum.Pass };
        chip.AddThemeStyleboxOverride("panel", PhantomRosterArt.Panel(
            new Color(0.08f, 0.05f, 0.15f, 0.78f), new Color(1f, 0.65f, 0.84f, 0.45f), 24));
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        chip.AddChild(row);
        if (icon != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = PhantomRosterArt.Load(icon),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(28f, 28f),
                MouseFilter = MouseFilterEnum.Ignore
            });
        }

        row.AddChild(PhantomRosterArt.MakeLabel(text, 20, Colors.White, 4));
        _chips.AddChild(chip);
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

        IReadOnlyList<PhantomRosterEntry> entries = _relic.GetRosterEntries();
        if (entries.Count == 0)
        {
            VBoxContainer empty = new() { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            SetFullRect(empty);
            empty.AddThemeConstantOverride("separation", 20);
            empty.AddChild(new TextureRect
            {
                Texture = PhantomRosterArt.Load(PhantomRosterArt.EmptyLock),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(120f, 120f),
                Modulate = new Color(1f, 1f, 1f, 0.8f),
                MouseFilter = MouseFilterEnum.Ignore
            });
            Label emptyLabel = PhantomRosterArt.MakeLabel(
                "本局尚未击败可召唤的怪物。战斗时将默认召唤 SmallHopper。", 26, PhantomRosterArt.Muted);
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            empty.AddChild(emptyLabel);
            _body.AddChild(empty);
            _defaultButton = _backButton;
            return;
        }

        _displayCards.AddRange(entries.Select(CreateDisplayCard));
        CreateGrid(new Rect2(0f, 0f, 1f, 1f), 0f, RosterDiameter);
        PopulateGrid(checkable: false);
    }

    private void ShowDetail(PhantomRosterEntry entry)
    {
        PhantomRosterEntry? current = _relic.GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(candidate => candidate?.InstanceId == entry.InstanceId);
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

        VBoxContainer stage = new()
        {
            AnchorRight = 0.39f,
            AnchorBottom = 1f,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        stage.AddThemeConstantOverride("separation", 6);
        _body.AddChild(stage);
        Control circle = new()
        {
            CustomMinimumSize = new Vector2(520f, 440f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        stage.AddChild(circle);
        Vector2 center = new(260f, 230f);
        AddDetailRing(circle, PhantomRosterArt.RingOuter, 540f, center, 0.07f);
        AddDetailRing(circle, PhantomRosterArt.RingInner, 410f, center, -0.1f);
        Control portrait = NPhantomMedallion.CreatePortraitCircle(PhantomRosterArt.GetPortrait(current.Value.MonsterId), 330f);
        portrait.Position = center - Vector2.One * 165f;
        circle.AddChild(portrait);
        circle.AddChild(PhantomRosterArt.Image(PhantomRosterArt.FrameActive, center - Vector2.One * 208f, Vector2.One * 416f));
        if (current.Value.InstanceId == _relic.ActiveRosterEntryId)
        {
            Label tag = PhantomRosterArt.MakeLabel("召唤中", 22, Colors.White, 3);
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            tag.AddThemeStyleboxOverride("normal", PhantomRosterArt.Panel(PhantomRosterArt.Pink, new Color(1f, 0.8f, 0.9f), 18));
            tag.Position = new Vector2(center.X - 60f, 0f);
            tag.Size = new Vector2(120f, 40f);
            circle.AddChild(tag);
        }

        Label name = PhantomRosterArt.MakeLabel(card.Title.Split(" Lv.")[0], 40, Colors.White, 6);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(name);
        Label level = PhantomRosterArt.MakeLabel(
            card.LevelDescription + (card.PhantomUpgradeLevel > 0 ? $"    融合 +{card.PhantomUpgradeLevel}" : string.Empty),
            25,
            PhantomRosterArt.Gold);
        level.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(level);
        HBoxContainer experienceText = new() { CustomMinimumSize = new Vector2(480f, 0f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        experienceText.AddChild(PhantomRosterArt.MakeLabel(
            $"经验  {current.Value.ExperienceIntoLevel} / {current.Value.ExperienceRequiredForNextLevel}", 20, PhantomRosterArt.Muted, 4));
        experienceText.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        experienceText.AddChild(PhantomRosterArt.MakeLabel($"累计  {current.Value.Experience}", 20, PhantomRosterArt.Muted, 4));
        stage.AddChild(experienceText);
        ProgressBar experience = CreateExperienceBar(current.Value);
        experience.CustomMinimumSize = new Vector2(480f, 16f);
        experience.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        stage.AddChild(experience);

        VBoxContainer details = AddScrollPanel(new Rect2(0.42f, 0f, 0.58f, 1f));
        AddSection(details, "成长与基础属性");
        GridContainer stats = new() { Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
        stats.AddThemeConstantOverride("h_separation", 40);
        stats.AddThemeConstantOverride("v_separation", 2);
        details.AddChild(stats);
        string statsText = StripBbcode(card.GetBaseStatsRichText(_relic.Owner?.GetRelic<ElectronicSoldierManual>()));
        foreach (string line in statsText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int split = line.IndexOf('：');
            stats.AddChild(CreateStatRow(
                split > 0 ? line[..split] : line,
                split > 0 ? line[(split + 1)..] : string.Empty));
        }

        string fusionEffect = GetFusionEffectText(current.Value);
        AddSection(details, "融合效果");
        foreach (string line in string.IsNullOrEmpty(fusionEffect)
                     ? new[] { "尚无融合效果" }
                     : fusionEffect.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            details.AddChild(CreateEffectLine(line, !string.IsNullOrEmpty(fusionEffect)));
        }

        bool canFuse = _relic.CanFuseRosterEntries && _relic.GetRosterEntries().Count > 1;
        string reason = !_relic.CanFuseRosterEntries
            ? "当前不能融合"
            : _relic.GetRosterEntries().Count <= 1
                ? "召唤列表中只有这一只幻影"
                : string.Empty;
        TextureButton fuse = AddActionButton("融合", ShowUpgrade, primary: true, disabled: !canFuse);
        TextureButton devour = AddActionButton("吞噬", ShowDevour, primary: false, disabled: !canFuse);
        fuse.TooltipText = devour.TooltipText = reason;
        _defaultButton = canFuse ? fuse : _backButton;
    }

    private void AddDetailRing(Control parent, string path, float size, Vector2 center, float speed)
    {
        TextureRect ring = PhantomRosterArt.Image(path, center - Vector2.One * size / 2f, Vector2.One * size);
        ring.Modulate = new Color(1f, 1f, 1f, 0.55f);
        parent.AddChild(ring);
        _spinners.Add((ring, speed));
    }

    private void ShowMaterialSelection(RosterView materialView, bool preserveSelection)
    {
        bool isDevour = materialView == RosterView.Devour;
        if (_targetEntry == null || !_relic.CanFuseRosterEntries)
        {
            ShowRoster();
            return;
        }

        PhantomRosterEntry? current = _relic.GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(candidate => candidate?.InstanceId == _targetEntry.Value.InstanceId);
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

        IReadOnlyList<PhantomRosterEntry> materials = _relic.GetRosterEntries()
            .Where(entry => entry.InstanceId != current.Value.InstanceId)
            .ToArray();
        _selectedMaterialIds.RemoveWhere(id => materials.All(entry => entry.InstanceId != id));
        _displayCards.AddRange(materials.Select(material => isDevour
            ? CreateDevourMaterialCard(material)
            : CreateFusionMaterialCard(material)));

        _toolsRow = new HBoxContainer
        {
            AnchorRight = 0.69f,
            OffsetBottom = 60f,
            MouseFilter = MouseFilterEnum.Pass
        };
        _toolsRow.AddThemeConstantOverride("separation", 16);
        _body.AddChild(_toolsRow);
        AddSelectAllMaterialsCheckBox();
        if (!isDevour)
        {
            AddFullFusionCheckBox();
        }

        _selectionCount = PhantomRosterArt.MakeLabel(string.Empty, 20, PhantomRosterArt.Muted, 4);
        _toolsRow.AddChild(_selectionCount);
        CreateGrid(new Rect2(0f, 0f, 0.69f, 1f), 76f, MaterialDiameter);
        PopulateGrid(checkable: true, isDevour);
        BuildSidePanel(isDevour ? "吞噬后效果" : "融合后效果");
        _confirmButton = AddActionButton(
            isDevour ? "确定吞噬" : "确定融合",
            isDevour ? ConfirmDevour : ConfirmUpgrade,
            primary: !isDevour,
            disabled: true);
        RefreshUpgradePreview();
    }

    private void AddFullFusionCheckBox()
    {
        CheckBox checkBox = new()
        {
            Text = "消耗完整融合次数（继承100%）",
            ButtonPressed = _consumeFullFusion,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        PhantomRosterArt.StyleCheckBox(checkBox, 19);
        checkBox.Toggled += OnFullFusionToggled;
        _toolsRow.AddChild(checkBox);
        _consumeFullFusionCheckBox = checkBox;
        RefreshFullFusionCheckBoxState();
    }

    private void AddSelectAllMaterialsCheckBox()
    {
        CheckBox checkBox = new()
        {
            Text = "全选",
            TooltipText = "勾选全部材料幻影，取消勾选则清空选择。",
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        PhantomRosterArt.StyleCheckBox(checkBox, 21);
        checkBox.Toggled += OnSelectAllMaterialsToggled;
        _toolsRow.AddChild(checkBox);
        _selectAllMaterialsCheckBox = checkBox;
        RefreshSelectAllMaterialsCheckBoxState();
    }

    private void OnRosterMedallionPressed(int entryId)
    {
        PhantomRosterEntry? entry = _relic.GetRosterEntries()
            .Cast<PhantomRosterEntry?>()
            .FirstOrDefault(candidate => candidate?.InstanceId == entryId);
        if (entry != null)
        {
            ShowDetail(entry.Value);
        }
    }

    private void OnMaterialMedallionPressed(int entryId)
    {
        if (_view != RosterView.Upgrade && _view != RosterView.Devour)
        {
            return;
        }

        if (!_selectedMaterialIds.Add(entryId))
        {
            _selectedMaterialIds.Remove(entryId);
        }

        if (_medallions.TryGetValue(entryId, out NPhantomMedallion medallion))
        {
            medallion.Selected = _selectedMaterialIds.Contains(entryId);
        }

        RefreshSelectionCount();
        RefreshUpgradePreview();
    }

    private void OnSelectAllMaterialsToggled(bool selected)
    {
        if ((_view != RosterView.Upgrade && _view != RosterView.Devour) || _grid == null)
        {
            return;
        }

        _selectedMaterialIds.Clear();
        if (selected)
        {
            _selectedMaterialIds.UnionWith(_displayCards.Select(card => card.RosterEntryId));
        }

        foreach ((int id, NPhantomMedallion medallion) in _medallions)
        {
            medallion.Selected = _selectedMaterialIds.Contains(id);
        }

        RefreshSelectionCount();
        RefreshUpgradePreview();
    }

    private void RefreshSelectionCount()
    {
        if (_selectionCount != null && GodotObject.IsInstanceValid(_selectionCount))
        {
            _selectionCount.Text = $"已选 {_selectedMaterialIds.Count} / {_displayCards.Count}";
        }
    }

    // Material descriptions depend on the full-fusion toggle, so the grid is rebuilt in place.
    private void RefreshFusionMaterialCards()
    {
        if (_view != RosterView.Upgrade || _targetEntry == null || _grid == null)
        {
            return;
        }

        _displayCards.Clear();
        _displayCards.AddRange(_relic.GetRosterEntries()
            .Where(entry => entry.InstanceId != _targetEntry.Value.InstanceId)
            .Select(CreateFusionMaterialCard));
        PopulateGrid(checkable: true);
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
            PhantomFusionEffectHelper.GetEffectTypes(_targetEntry.Value.FusionEffectData);
        IReadOnlyList<PhantomFusionEffectType> allTypes =
            PhantomFusionEffectHelper.GetEffectTypes(unrestrictedPreview.FusionEffectData);
        if (preserveSelection)
        {
            HashSet<PhantomFusionEffectType> available = allTypes.ToHashSet();
            _selectedEffectTypes.RemoveWhere(type => !available.Contains(type));
        }
        else
        {
            _selectedEffectTypes.Clear();
            foreach (PhantomFusionEffectType type in currentTypes.Concat(allTypes))
            {
                if (_selectedEffectTypes.Count >= FusionSlotCount)
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
            AnchorRight = 0.69f,
            AnchorBottom = 1f,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _body.AddChild(effectScroll);
        VBoxContainer effectList = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        effectList.AddThemeConstantOverride("separation", 12);
        effectScroll.AddChild(effectList);

        HashSet<PhantomFusionEffectType> currentTypeSet = currentTypes.ToHashSet();
        foreach (PhantomFusionEffectType type in currentTypes.Concat(allTypes.Where(type => !currentTypeSet.Contains(type))))
        {
            bool isCurrent = currentTypeSet.Contains(type);
            string description = PhantomFusionEffectHelper.GetEffectDescription(
                unrestrictedPreview.FusionEffectData, type, _relic.Owner?.Creature);
            bool isSelected = _selectedEffectTypes.Contains(type);
            Button option = CreateOptionButton(GetEffectSelectionOptionText(isCurrent, description), isSelected);
            option.ButtonPressed = isSelected;
            option.TooltipText = description;
            option.Toggled += pressed => OnEffectTypeToggled(type, isCurrent, description, pressed, option);
            effectList.AddChild(option);
            _defaultButton ??= option;
        }

        VBoxContainer side = BuildSidePanel("保留后效果");
        _toolsRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Pass };
        side.AddChild(_toolsRow);
        side.MoveChild(_toolsRow, 1);
        AddFullFusionCheckBox();
        _confirmButton = AddActionButton("应用并融合", ConfirmEffectSelection, primary: true);
        RefreshEffectSelectionPreview();
    }

    private void CreateGrid(Rect2 anchors, float top, float diameter)
    {
        _gridScroll = new ScrollContainer
        {
            AnchorLeft = anchors.Position.X,
            AnchorTop = anchors.Position.Y,
            AnchorRight = anchors.Position.X + anchors.Size.X,
            AnchorBottom = anchors.Position.Y + anchors.Size.Y,
            OffsetTop = top,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _body.AddChild(_gridScroll);
        _grid = new GridContainer { Columns = 6, MouseFilter = MouseFilterEnum.Pass };
        _grid.AddThemeConstantOverride("h_separation", (int)GridGap);
        _grid.AddThemeConstantOverride("v_separation", (int)GridGap);
        _gridScroll.AddChild(_grid);
        GridContainer grid = _grid;
        ScrollContainer scroll = _gridScroll;
        float cell = NPhantomMedallion.WidthFor(diameter);
        void FitColumns()
        {
            if (GodotObject.IsInstanceValid(grid) && GodotObject.IsInstanceValid(scroll))
            {
                grid.Columns = Math.Max(1, (int)((scroll.Size.X - 12f + GridGap) / (cell + GridGap)));
            }
        }

        scroll.Resized += FitColumns;
        Callable.From(FitColumns).CallDeferred();
    }

    private void PopulateGrid(bool checkable, bool isDevour = false)
    {
        if (_grid == null)
        {
            return;
        }

        int scroll = _gridScroll?.ScrollVertical ?? 0;
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        _medallions.Clear();
        Dictionary<int, PhantomRosterEntry> entries = _relic.GetRosterEntries().ToDictionary(entry => entry.InstanceId);
        ElectronicSoldierManual manual = _relic.Owner?.GetRelic<ElectronicSoldierManual>();
        foreach (PhantomChoiceHbrgoSaki card in _displayCards.OrderByPhantomGrowth())
        {
            if (!entries.TryGetValue(card.RosterEntryId, out PhantomRosterEntry entry))
            {
                continue;
            }

            int used = PhantomFusionEffectHelper.GetEffectTypes(entry.FusionEffectData).Count;
            NPhantomMedallion medallion = NPhantomMedallion.Create(
                card,
                entry,
                checkable ? MaterialDiameter : RosterDiameter,
                entry.InstanceId == _relic.ActiveRosterEntryId,
                checkable,
                checkable
                    ? $"素材经验 {entry.ExperienceValue}"
                    : "融合 " + new string('●', used) + new string('○', Math.Max(0, entry.FusionSlotCount - used)),
                checkable
                    ? MaterialTooltip(entry, isDevour)
                    : StripBbcode(card.GetBaseStatsRichText(manual)));
            medallion.Selected = checkable && _selectedMaterialIds.Contains(entry.InstanceId);
            medallion.Pressed += checkable ? OnMaterialMedallionPressed : OnRosterMedallionPressed;
            _grid.AddChild(medallion);
            _medallions[entry.InstanceId] = medallion;
            _defaultButton ??= medallion;
        }

        if (_gridScroll != null)
        {
            ScrollContainer gridScroll = _gridScroll;
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(gridScroll))
                {
                    gridScroll.ScrollVertical = scroll;
                }
            }).CallDeferred();
        }

        RefreshSelectionCount();
    }

    private string MaterialTooltip(PhantomRosterEntry material, bool isDevour)
    {
        if (isDevour)
        {
            return "吞噬后效果：获得该素材可提供经验的2倍。\n不会获得素材的强化效果、融合次数或成长属性。";
        }

        return _targetEntry is { } target &&
            _relic.TryGetUpgradePreview(
                target.InstanceId,
                new[] { material.InstanceId },
                _consumeFullFusion && material.HasEarnedUpgradeEffects,
                out PhantomRosterEntry preview,
                out _)
            ? StripBbcode(GetFusionMaterialEffectText(target, preview))
            : string.Empty;
    }

    private VBoxContainer BuildSidePanel(string title)
    {
        PanelContainer panel = new()
        {
            AnchorLeft = 0.715f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Pass
        };
        panel.AddThemeStyleboxOverride("panel", PhantomRosterArt.Panel(
            new Color(0.09f, 0.05f, 0.17f, 0.84f), new Color(1f, 0.65f, 0.84f, 0.35f), 20));
        _body.AddChild(panel);
        VBoxContainer box = new() { MouseFilter = MouseFilterEnum.Pass };
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);
        Label heading = PhantomRosterArt.MakeLabel(title, 27, PhantomRosterArt.Gold);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(heading);
        _upgradePreviewSlot = new CenterContainer
        {
            CustomMinimumSize = new Vector2(0f, NPhantomMedallion.HeightFor(PreviewDiameter)),
            MouseFilter = MouseFilterEnum.Pass
        };
        box.AddChild(_upgradePreviewSlot);
        _selectionSummary = PhantomRosterArt.MakeLabel(string.Empty, 19, new Color(0.88f, 0.85f, 0.94f), 4);
        _selectionSummary.HorizontalAlignment = HorizontalAlignment.Center;
        _selectionSummary.VerticalAlignment = VerticalAlignment.Top;
        _selectionSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _selectionSummary.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(_selectionSummary);
        return box;
    }

    private void AddPreviewMedallion(PhantomRosterEntry entry)
    {
        if (_upgradePreviewSlot == null)
        {
            return;
        }

        PhantomChoiceHbrgoSaki card = CreateDisplayCard(entry);
        int used = PhantomFusionEffectHelper.GetEffectTypes(entry.FusionEffectData).Count;
        NPhantomMedallion medallion = NPhantomMedallion.Create(
            card,
            entry,
            PreviewDiameter,
            entry.InstanceId == _relic.ActiveRosterEntryId,
            checkable: false,
            $"融合槽位 {used}/{entry.FusionSlotCount}",
            StripBbcode(card.GetBaseStatsRichText(_relic.Owner?.GetRelic<ElectronicSoldierManual>())));
        medallion.FocusMode = FocusModeEnum.None;
        medallion.MouseDefaultCursorShape = CursorShape.Arrow;
        _upgradePreviewSlot.AddChild(medallion);
    }

    private VBoxContainer AddScrollPanel(Rect2 anchors)
    {
        PanelContainer panel = new()
        {
            AnchorLeft = anchors.Position.X,
            AnchorRight = anchors.Position.X + anchors.Size.X,
            AnchorBottom = anchors.Position.Y + anchors.Size.Y,
            MouseFilter = MouseFilterEnum.Pass
        };
        panel.AddThemeStyleboxOverride("panel", PhantomRosterArt.Panel(
            new Color(0.09f, 0.05f, 0.17f, 0.84f), new Color(1f, 0.65f, 0.84f, 0.35f), 20));
        _body.AddChild(panel);
        ScrollContainer scroll = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        panel.AddChild(scroll);
        VBoxContainer box = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(box);
        return box;
    }

    private static void AddSection(VBoxContainer parent, string title)
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new ColorRect
        {
            Color = PhantomRosterArt.Pink,
            CustomMinimumSize = new Vector2(6f, 26f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        });
        row.AddChild(PhantomRosterArt.MakeLabel(title, 25, PhantomRosterArt.PinkLight));
        parent.AddChild(row);
    }

    private static Control CreateStatRow(string label, string value)
    {
        HBoxContainer row = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(440f, 44f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(PhantomRosterArt.MakeLabel(label, 20, PhantomRosterArt.Muted, 4));
        Label valueLabel = PhantomRosterArt.MakeLabel(value, 22, Colors.White, 4);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(valueLabel);
        return row;
    }

    private static Control CreateEffectLine(string text, bool accent)
    {
        PanelContainer line = new() { MouseFilter = MouseFilterEnum.Ignore };
        StyleBoxFlat style = PhantomRosterArt.Panel(
            accent ? new Color(1f, 0.37f, 0.7f, 0.08f) : Colors.Transparent,
            PhantomRosterArt.Pink,
            12,
            0);
        style.BorderWidthLeft = accent ? 3 : 0;
        line.AddThemeStyleboxOverride("panel", style);
        Label label = PhantomRosterArt.MakeLabel(text, 20, accent ? Colors.White : PhantomRosterArt.Muted, 4);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.AddChild(label);
        return line;
    }

    private TextureButton AddActionButton(string text, Action pressed, bool primary, bool disabled = false)
    {
        TextureButton button = PhantomRosterArt.MakeButton(text, primary);
        button.Disabled = disabled;
        button.Pressed += pressed;
        _footer.AddChild(button);
        return button;
    }

    private static ProgressBar CreateExperienceBar(PhantomRosterEntry entry)
    {
        ProgressBar bar = new()
        {
            MinValue = 0d,
            MaxValue = Math.Max(1, entry.ExperienceRequiredForNextLevel),
            Value = entry.ExperienceIntoLevel,
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        bar.AddThemeStyleboxOverride("background", PhantomRosterArt.Panel(
            new Color(1f, 1f, 1f, 0.12f), new Color(1f, 0.65f, 0.84f, 0.35f), 8));
        bar.AddThemeStyleboxOverride("fill", PhantomRosterArt.Panel(PhantomRosterArt.Pink, PhantomRosterArt.Pink, 8, 0));
        return bar;
    }

    private static Button CreateOptionButton(string text, bool accent)
    {
        Button button = new()
        {
            Text = text,
            ToggleMode = true,
            Alignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 76f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.AddThemeFontSizeOverride("font_size", 21);
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(color, Colors.White);
        }

        ApplyButtonAccent(button, accent);
        return button;
    }

    private static void ApplyButtonAccent(Button button, bool accent)
    {
        Color background = accent ? new Color(0.45f, 0.12f, 0.32f, 0.62f) : new Color(0.08f, 0.05f, 0.15f, 0.78f);
        Color border = accent ? PhantomRosterArt.Pink : new Color(1f, 0.65f, 0.84f, 0.3f);
        StyleBoxFlat normal = PhantomRosterArt.Panel(background, border, 18, accent ? 2 : 1);
        StyleBoxFlat hover = PhantomRosterArt.Panel(background.Lightened(0.08f), PhantomRosterArt.PinkLight, 18, 2);
        foreach (string state in new[] { "normal", "pressed" })
        {
            button.AddThemeStyleboxOverride(state, normal);
        }

        foreach (string state in new[] { "hover", "hover_pressed", "focus" })
        {
            button.AddThemeStyleboxOverride(state, hover);
        }
    }

    private static string StripBbcode(string text) =>
        Regex.Replace(text ?? string.Empty, @"\[/?[a-zA-Z_]+(=[^\]]*)?\]", string.Empty);
}
