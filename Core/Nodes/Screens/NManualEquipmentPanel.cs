using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The manual's loadout page, after the original アクセサリ装備 screen: totals
// on the left, one capsule per slot on the right, each accessory followed by
// its three "+" boxes. A picker lists owned items for the chosen slot.
// Panels, rows and colours follow the phone manual's page style.
public partial class NManualEquipmentPanel : Control
{
    private const double ConfirmSeconds = 3d;

    private static readonly HbrEquipmentStat[] StatOrder =
    [
        HbrEquipmentStat.Dp, HbrEquipmentStat.Strength, HbrEquipmentStat.Dexterity, HbrEquipmentStat.Vitality,
        HbrEquipmentStat.Spirit, HbrEquipmentStat.Intelligence, HbrEquipmentStat.Luck, HbrEquipmentStat.CriticalRate
    ];

    private ElectronicSoldierManual _manual;
    private VBoxContainer _root;
    private Label _level;
    private Label _materials;
    private Label _note;
    private Label _effects;
    private readonly Dictionary<HbrEquipmentStat, Label> _statValues = new();
    private ScrollContainer _scroll;
    private VBoxContainer _slots;
    private PanelContainer _picker;
    private Label _pickerTitle;
    private VBoxContainer _pickerRows;
    private Label _status;
    private HbrEquipmentSlot? _pickerSlot;
    private string _lastState;
    private string _confirmId;
    private double _confirmLeft;

    internal Control DefaultFocusedControl => _scroll ?? (Control)this;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        Refresh(_manual);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutPicker();
    }

    public override void _Process(double delta)
    {
        if (_confirmId == null) return;
        _confirmLeft -= delta;
        if (_confirmLeft > 0d) return;
        _confirmId = null;
        _lastState = null;
        Refresh(_manual);
    }

    internal void Refresh(ElectronicSoldierManual manual)
    {
        _manual = manual;
        if (_slots == null || manual == null) return;
        HbrEquipmentState state = manual.Equipment;
        bool editable = HbrEquipmentNetActions.CanRequest(manual) && !manual.IsEquipmentPending;
        bool unlocked = ElectronicSoldierManual.IsEquipmentLoadoutUnlocked;
        string key = $"{state.Revision}|{state.Items.Length}|{editable}|{unlocked}|{_confirmId}|{_pickerSlot}";
        if (key == _lastState) return;
        _lastState = key;

        HbrEquipmentBonus bonus = HbrEquipmentRules.Totals(state);
        foreach ((HbrEquipmentStat stat, Label label) in _statValues)
            label.Text = HbrEquipmentText.Value(stat, bonus[stat]);
        _effects.Text = string.Join("\n", HbrEquipmentText.EffectLines(HbrEquipmentRules.EffectTotals(state)));
        _effects.Visible = _effects.Text.Length > 0;
        _level.Text = $"饰品 Lv.{HbrEquipmentRules.AccessoryLevel(state)}";
        _materials.Text = $"{HbrEquipmentCatalog.PlusReleaseName} ×{state.PlusRelease}　{HbrEquipmentCatalog.PlusChangeName} ×{state.PlusChange}";
        _note.Text = unlocked
            ? "点击装备栏选择要装备的物品。「？」用青绿琥珀之露解锁+栏，已解锁的+栏可以用阳石之露重新抽选。"
            : HbrEquipmentCommands.LoadoutLocked + "。商店仍可购买，战斗结束后再调整装备。";

        HbrEquipmentArt.Clear(_slots);
        AddSection("增幅器");
        AddSlotRow(HbrEquipmentSlot.Booster, state, editable, unlocked);
        // Chips follow the equipped booster: one row per chip slot it has, chips fitted over slots, and
        // 0/0 without a booster. Trim returns chips in closed slots to the inventory, so none are hidden.
        int chipSlots = HbrEquipmentRules.ChipSlotCount(state);
        int chipsFitted = 0;
        for (int i = 0; i < chipSlots; i++)
            if (state.FindItem(state.At(HbrEquipmentSlot.Chip(i))) != null) chipsFitted++;
        AddSection($"芯片　{chipsFitted}/{chipSlots}");
        for (int i = 0; i < chipSlots; i++)
            AddSlotRow(HbrEquipmentSlot.Chip(i), state, editable, unlocked);
        AddSection("饰品");
        foreach (HbrEquipmentSlot slot in HbrEquipmentSlot.Accessories) AddSlotRow(slot, state, editable, unlocked);
        AddSection($"吊饰　{HbrEquipmentRules.OpenCharmSlots(state)}/{HbrEquipmentCatalog.CharmSlots}");
        for (int i = 0; i < HbrEquipmentCatalog.CharmSlots; i++)
            AddSlotRow(HbrEquipmentSlot.Charm(i), state, editable, unlocked);

        if (!unlocked) _pickerSlot = null;
        RefreshPicker(state, editable);
    }

    private void BuildUi()
    {
        _root = new VBoxContainer();
        _root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _root.AddThemeConstantOverride("separation", 10);
        AddChild(_root);

        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 12);
        _root.AddChild(header);
        header.AddChild(HbrEquipmentArt.MakeLabel("饰品装备", 26, Colors.White));
        Label english = HbrEquipmentArt.MakeLabel("Accessory Equipment", 15, HbrManualPhoneArt.Muted, 2);
        english.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(english);
        _level = HbrEquipmentArt.MakeLabel("", 21, HbrManualPhoneArt.Pink);
        _level.MouseFilter = MouseFilterEnum.Stop;
        _level.TooltipText = "饰品 Lv. = 戒指、耳坠、手环、项链的星级之和 + 全部「+」值之和的一半。\n" +
            $"饰品 Lv. 达到 {string.Join("、", HbrEquipmentCatalog.CharmSlotLevels)} 时，依次解锁 6 个吊饰栏。";
        header.AddChild(_level);
        _materials = HbrEquipmentArt.MakeLabel("", 15, HbrManualPhoneArt.Muted, 3);
        header.AddChild(_materials);

        HBoxContainer body = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        _root.AddChild(body);

        // The stat column (totals, effect lines, the conversion rule) can outgrow the page with many
        // effects; it scrolls on its own instead of pushing the slot list below the phone screen.
        ScrollContainer statsScroll = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        HbrManualPhoneStyle.Scroll(statsScroll);
        body.AddChild(statsScroll);
        PanelContainer stats = new() { CustomMinimumSize = new Vector2(250, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stats.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Panel(18, 16f, 12f));
        statsScroll.AddChild(stats);
        VBoxContainer statsLayout = new();
        statsLayout.AddThemeConstantOverride("separation", 8);
        stats.AddChild(statsLayout);
        statsLayout.AddChild(HbrEquipmentArt.MakeLabel("能力", 20, HbrManualPhoneArt.Cyan));
        GridContainer grid = new() { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 6);
        statsLayout.AddChild(grid);
        foreach (HbrEquipmentStat stat in StatOrder)
        {
            grid.AddChild(HbrEquipmentArt.MakeLabel(HbrEquipmentText.StatName(stat), 17, HbrManualPhoneArt.Muted, 3));
            Label value = HbrEquipmentArt.MakeLabel("+0", 18, Colors.White, 3);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddChild(value);
            _statValues[stat] = value;
        }
        _effects = HbrEquipmentArt.MakeLabel("", 15, HbrEquipmentArt.Gold, 3);
        _effects.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statsLayout.AddChild(_effects);
        Label explain = HbrEquipmentArt.MakeLabel(
            HbrEquipmentText.ScaleRule() + "幻影与浮游炮沿用本体属性，也会得到这些加成。", 14, HbrManualPhoneArt.Muted, 2);
        explain.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statsLayout.AddChild(explain);
        _note = HbrEquipmentArt.MakeLabel("", 14, HbrManualPhoneStyle.Text, 2);
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statsLayout.AddChild(_note);

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All
        };
        HbrManualPhoneStyle.Scroll(_scroll);
        body.AddChild(_scroll);
        _slots = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _slots.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(_slots);

        _status = HbrEquipmentArt.MakeLabel("", 15, HbrEquipmentArt.Gold, 3);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _root.AddChild(_status);

        _picker = new PanelContainer { Visible = false };
        _picker.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Fill(new Color(0.024f, 0.078f, 0.125f, 0.97f), 18,
            HbrManualPhoneArt.Cyan2, 2, 16f, 12f));
        AddChild(_picker);
        VBoxContainer pickerLayout = new();
        pickerLayout.AddThemeConstantOverride("separation", 8);
        _picker.AddChild(pickerLayout);
        HBoxContainer pickerHeader = new();
        pickerLayout.AddChild(pickerHeader);
        _pickerTitle = HbrEquipmentArt.MakeLabel("", 21, Colors.White);
        _pickerTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pickerHeader.AddChild(_pickerTitle);
        Button close = HbrEquipmentArt.MakeCapsule("关闭", 17, 44f);
        close.CustomMinimumSize = new Vector2(108, 44);
        close.Pressed += () => { _pickerSlot = null; _lastState = null; Refresh(_manual); };
        pickerHeader.AddChild(close);
        ScrollContainer pickerScroll = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        HbrManualPhoneStyle.Scroll(pickerScroll);
        pickerLayout.AddChild(pickerScroll);
        _pickerRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pickerRows.AddThemeConstantOverride("separation", 6);
        pickerScroll.AddChild(_pickerRows);
    }

    private void LayoutPicker()
    {
        if (_picker == null) return;
        float left = Size.X >= 640f ? 264f : 0f;
        HbrManualPhoneStyle.Pin(_picker, Size, left, 44f, Math.Max(0f, Size.X - left), Math.Max(0f, Size.Y - 80f));
    }

    private void AddSection(string title)
    {
        Label label = HbrEquipmentArt.MakeLabel(title, 18, HbrManualPhoneArt.Cyan, 3);
        _slots.AddChild(label);
    }

    private void AddSlotRow(HbrEquipmentSlot slot, HbrEquipmentState state, bool editable, bool unlocked)
    {
        bool open = HbrEquipmentRules.IsSlotOpen(state, slot);
        HbrEquipmentItem item = open ? state.FindItem(state.At(slot)) : null;
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        Button row = HbrEquipmentArt.MakeCapsule(string.Empty, 17, 76f);
        row.Disabled = !open;
        row.TooltipText = template != null ? HbrEquipmentText.Tooltip(item)
            : open ? $"选择{HbrEquipmentText.KindName(slot.Kind)}" : ClosedReason(slot);
        row.Pressed += () =>
        {
            if (!unlocked) { _status.Text = HbrEquipmentCommands.LoadoutLocked; return; }
            if (!editable) return;
            _pickerSlot = slot;
            _lastState = null;
            Refresh(_manual);
        };
        HBoxContainer layout = new() { MouseFilter = MouseFilterEnum.Ignore };
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layout.OffsetLeft = 24;
        layout.OffsetRight = -24;
        layout.AddThemeConstantOverride("separation", 12);
        row.AddChild(layout);
        if (template != null)
        {
            layout.AddChild(HbrEquipmentArt.MakeItemIcon(template.Icon, template.Rarity, 56f));
            VBoxContainer text = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            text.AddChild(HbrEquipmentArt.MakeLabel(HbrEquipmentText.Name(item), 19, Colors.White));
            text.AddChild(MakeDetail(HbrEquipmentText.ItemText(item)));
            layout.AddChild(text);
            if (template.PlusSlots > 0) layout.AddChild(MakePlusBoxes(item, template, state, editable, unlocked));
        }
        else
        {
            Label empty = HbrEquipmentArt.MakeLabel(open ? $"{HbrEquipmentText.KindName(slot.Kind)}　未装备" : ClosedReason(slot),
                18, open ? HbrManualPhoneArt.Muted : HbrManualPhoneStyle.Dim, 3);
            empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            layout.AddChild(empty);
        }
        if (open) layout.AddChild(HbrEquipmentArt.MakeLabel("＞", 22, Colors.White));
        _slots.AddChild(row);
    }

    // Effect lines can be long; the row's tooltip carries the full text.
    private static Label MakeDetail(string text)
    {
        Label detail = HbrEquipmentArt.MakeLabel(text, 14, HbrManualPhoneArt.Muted, 2);
        detail.ClipText = true;
        detail.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return detail;
    }

    private static string ClosedReason(HbrEquipmentSlot slot) => slot.Kind == HbrEquipmentKind.Chip
        ? "增幅器的芯片栏不足"
        : $"饰品 Lv.{HbrEquipmentCatalog.CharmSlotLevels[Math.Clamp(slot.Index, 0, HbrEquipmentCatalog.CharmSlots - 1)]} 解锁";

    // Up to three boxes: an opened "+" shows its stat; the next box is "？"
    // and opens with 青绿琥珀之露; later boxes wait until the earlier ones open.
    private Control MakePlusBoxes(HbrEquipmentItem item, HbrEquipmentTemplate template, HbrEquipmentState state,
        bool editable, bool unlocked)
    {
        HBoxContainer boxes = new() { MouseFilter = MouseFilterEnum.Ignore };
        boxes.AddThemeConstantOverride("separation", 6);
        bool canTouch = editable && (unlocked || !state.IsEquipped(item.Id));
        for (int index = 0; index < template.PlusSlots; index++)
        {
            bool opened = index < item.Plus.Length;
            bool next = index == item.Plus.Length;
            string id = $"plus:{item.Id}:{index}";
            bool confirming = _confirmId == id;
            string text = opened ? HbrEquipmentText.PlusSlotText(item, index) : "？";
            Button box = new()
            {
                Text = confirming ? "确定" : text,
                CustomMinimumSize = new Vector2(92, 40),
                FocusMode = FocusModeEnum.All,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            box.AddThemeFontSizeOverride("font_size", 14);
            StyleBoxTexture style = new() { Texture = HbrEquipmentArt.Load(opened ? HbrEquipmentArt.PlusSlotOn : HbrEquipmentArt.PlusSlotOff) };
            style.TextureMarginLeft = 16;
            style.TextureMarginRight = 16;
            style.TextureMarginTop = 14;
            style.TextureMarginBottom = 14;
            foreach (string look in new[] { "normal", "hover", "pressed", "focus", "disabled" })
                box.AddThemeStyleboxOverride(look, style);
            foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
                box.AddThemeColorOverride(color, opened ? Colors.White : HbrManualPhoneArt.Cyan);
            box.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.35f));
            if (opened)
            {
                box.Disabled = !canTouch || state.PlusChange <= 0;
                box.TooltipText = state.PlusChange > 0
                    ? $"消耗 1 个{HbrEquipmentCatalog.PlusChangeName}，重新抽选这个+栏的效果。原效果不会保留。"
                    : $"{HbrEquipmentCatalog.PlusChangeName}不足";
            }
            else
            {
                box.Disabled = !canTouch || !next || state.PlusRelease <= 0;
                box.TooltipText = !next ? "先解锁前面的+栏"
                    : state.PlusRelease > 0 ? $"消耗 1 个{HbrEquipmentCatalog.PlusReleaseName}，解锁这个+栏并随机获得一项属性。"
                    : $"{HbrEquipmentCatalog.PlusReleaseName}不足";
            }
            int itemId = item.Id;
            int slotIndex = index;
            box.Pressed += () =>
            {
                if (_confirmId != id)
                {
                    _confirmId = id;
                    _confirmLeft = ConfirmSeconds;
                    _lastState = null;
                    Refresh(_manual);
                    return;
                }
                _confirmId = null;
                Send(new HbrEquipmentCommand
                {
                    Revision = _manual.Equipment.Revision,
                    Op = opened ? HbrEquipmentOp.ChangePlus : HbrEquipmentOp.OpenPlus,
                    ItemId = itemId,
                    Index = slotIndex
                }, opened ? "+栏效果已重新抽选。" : "+栏已解锁。");
            };
            boxes.AddChild(box);
        }
        return boxes;
    }

    private void RefreshPicker(HbrEquipmentState state, bool editable)
    {
        _picker.Visible = _pickerSlot.HasValue;
        LayoutPicker();
        if (_pickerSlot is not { } slot) return;
        HbrEquipmentArt.Clear(_pickerRows);
        _pickerTitle.Text = $"选择{HbrEquipmentText.KindName(slot.Kind)}";
        int current = state.At(slot);
        if (current > 0)
        {
            Button remove = HbrEquipmentArt.MakeCapsule("卸下当前装备", 18, 52f);
            remove.Disabled = !editable;
            remove.Pressed += () => Send(new HbrEquipmentCommand
            {
                Revision = _manual.Equipment.Revision,
                Op = HbrEquipmentOp.Unequip,
                Slot = slot.Position
            }, "已卸下。", closePicker: true);
            _pickerRows.AddChild(remove);
        }
        List<HbrEquipmentItem> candidates = state.Items
            .Where(item => HbrEquipmentCatalog.Find(item.Template).Kind == slot.Kind && item.Id != current)
            .OrderBy(item => state.IsEquipped(item.Id))
            .ThenByDescending(item => HbrEquipmentCatalog.Find(item.Template).Rarity)
            .ThenByDescending(item => HbrEquipmentRules.DisplayPlus(item)).ToList();
        if (candidates.Count == 0)
        {
            Label empty = HbrEquipmentArt.MakeLabel(
                $"没有可装备的{HbrEquipmentText.KindName(slot.Kind)}。可以在战斗缴获中获得，或在「商店」购买、炼金。", 17,
                HbrManualPhoneArt.Muted, 3);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _pickerRows.AddChild(empty);
            return;
        }
        foreach (HbrEquipmentItem item in candidates)
        {
            HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item.Template);
            Button row = HbrEquipmentArt.MakeCapsule(string.Empty, 17, 74f);
            row.Disabled = !editable;
            string plus = item.Plus.Length > 0 ? "　+栏：" + HbrEquipmentText.PlusSlotsText(item) : "";
            row.TooltipText = HbrEquipmentText.Tooltip(item) + plus;
            int itemId = item.Id;
            row.Pressed += () => Send(new HbrEquipmentCommand
            {
                Revision = _manual.Equipment.Revision,
                Op = HbrEquipmentOp.Equip,
                ItemId = itemId,
                Slot = slot.Position
            }, $"已装备{HbrEquipmentText.Name(item)}。", closePicker: true);
            HBoxContainer layout = new() { MouseFilter = MouseFilterEnum.Ignore };
            layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            layout.OffsetLeft = 24;
            layout.OffsetRight = -24;
            layout.AddThemeConstantOverride("separation", 12);
            row.AddChild(layout);
            layout.AddChild(HbrEquipmentArt.MakeItemIcon(template.Icon, template.Rarity, 54f));
            VBoxContainer text = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            text.AddChild(HbrEquipmentArt.MakeLabel(HbrEquipmentText.Name(item) + (state.IsEquipped(item.Id) ? "　（装备中）" : ""), 18, Colors.White));
            text.AddChild(MakeDetail(HbrEquipmentText.ItemText(item) + plus));
            layout.AddChild(text);
            _pickerRows.AddChild(row);
        }
    }

    private void Send(HbrEquipmentCommand command, string done, bool closePicker = false)
    {
        HbrEquipmentOutcome check = HbrEquipmentCommands.Evaluate(_manual.Equipment, command, _manual.EquipmentTier,
            ElectronicSoldierManual.IsEquipmentLoadoutUnlocked, _manual.Owner?.Gold ?? 0);
        if (!check.Succeeded)
        {
            _status.Text = check.Error;
            return;
        }
        if (!HbrEquipmentNetActions.Request(_manual, command))
        {
            _status.Text = "现在无法操作，请稍后再试。";
            return;
        }
        _status.Text = done;
        if (closePicker) _pickerSlot = null;
        _lastState = null;
        Refresh(_manual);
    }
}
