using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The manual's 命运骰子 page (design v3, sections 4 and 5). On top, the dice with one deep red crack drawn on
// per crack, its numbers, and a strip of the five sockets (fragments in their category rings, empty, cracked,
// not opened yet). Below, one row per slot, the bag, and the fragments of the merchant the player stands in,
// each fragment with its ringed icon. Outside combat a fragment goes on or off with one press; buying asks
// for a second press, like the equipment shop. After a rebuild the focus stays on the same entry, or the one
// now in its place, so a controller can keep going down the list.
public partial class NManualFateDicePanel : Control
{
    private const int TitleSize = 26;
    private const int TextSize = 20;
    private const int SmallSize = 17;
    private const float BadgeSize = 148f;
    private const float StripIcon = 44f;
    private const float RowIcon = 64f;
    private const float ConfirmSeconds = 3f;
    private const string KeyMeta = "hbr_fate_key";

    private static readonly Color CrackText = new("E0444A");
    private static readonly Color RowWash = new(0.588f, 0.941f, 0.98f, 0.03f);

    private readonly Dictionary<string, Button> _buttons = new();
    private readonly List<string> _order = new();
    private ScrollContainer _scroll;
    private VBoxContainer _list;
    private Button _firstButton;
    private string _signature;
    private ElectronicSoldierManual _manual;
    private string _confirmBuy;
    private float _confirmLeft;

    internal Control DefaultFocusedControl => _firstButton ?? (Control)_scroll ?? this;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _scroll = new ScrollContainer
        {
            Name = "Scroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        AddChild(_scroll);
        _scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        HbrManualPhoneStyle.Scroll(_scroll);
        _list = new VBoxContainer { Name = "List", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        _scroll.AddChild(_list);
        if (_manual != null) Rebuild();
    }

    public override void _Process(double delta)
    {
        if (_confirmBuy == null) return;
        _confirmLeft -= (float)delta;
        if (_confirmLeft > 0f && IsVisibleInTree()) return;
        _confirmBuy = null;
        _signature = null;
        Refresh(_manual);
    }

    internal void Refresh(ElectronicSoldierManual manual)
    {
        _manual = manual;
        if (_list == null) return;
        string signature = manual?.UsesFateDice != true ? "none"
            : $"{manual.FateDice.Serialize()}|{manual.Owner.Gold}|{ElectronicSoldierManual.IsFateDiceLoadoutUnlocked}|" +
              $"{manual.IsFateDicePending}|{manual.CurrentFateShopKey}|{manual.IsHbrNumericsEnabled}|{_confirmBuy}";
        if (signature == _signature) return;
        _signature = signature;
        Rebuild();
    }

    private void Rebuild()
    {
        (string focusKey, int focusIndex) = FocusedEntry();
        foreach (Node child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        _firstButton = null;
        _buttons.Clear();
        _order.Clear();
        if (_manual?.UsesFateDice != true)
        {
            _list.AddChild(Text("命运骰子只属于丰川祥子（31O）。", TextSize, HbrManualPhoneStyle.Dim, wrap: true));
            return;
        }

        HbrFateDiceState state = _manual.FateDice;
        bool unlocked = ElectronicSoldierManual.IsFateDiceLoadoutUnlocked;
        bool pending = _manual.IsFateDicePending;
        bool hbr = _manual.IsHbrNumericsEnabled;

        _list.AddChild(Header(state, unlocked));
        AddSlots(state, hbr, unlocked && !pending);
        AddBag(state, hbr, unlocked, pending);
        AddShop(state, hbr, pending);
        RestoreFocus(focusKey, focusIndex);
    }

    // ---- the dice ------------------------------------------------------------------------------------------

    private Control Header(HbrFateDiceState state, bool unlocked)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Panel(18, 18f, 14f));
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 18);
        panel.AddChild(row);
        row.AddChild(NHbrFateDiceBadge.Create(BadgeSize, state.Cracks));

        var side = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        side.AddThemeConstantOverride("separation", 8);
        row.AddChild(side);

        var stats = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        stats.AddThemeConstantOverride("separation", 28);
        int keptMaximum = SwordheartPower.GetKeptMaximum(_manual.Owner.Creature);
        stats.AddChild(Stat("槽位", $"{state.Slots}/{HbrFateDiceState.MaxSlots}", Colors.White));
        stats.AddChild(Stat("裂痕", $"{state.Cracks}", state.Cracks > 0 ? CrackText : Colors.White));
        stats.AddChild(Stat("剑心", $"{state.Swordheart}/{keptMaximum}", new Color("F8D36A")));
        side.AddChild(stats);
        side.AddChild(SlotStrip(state));

        if (state.RawPenalty > 0)
            side.AddChild(Text($"槽位全被裂痕占满：原始出目 −{state.RawPenalty}。", SmallSize, CrackText, wrap: true));
        var lines = new List<string>
        {
            $"每次关闭魔剑打完终结技后加一道裂痕；战斗结束时仍开着也算。紧急撤出不加。槽位全被占满后，每多一道，原始出目 −{HbrFateDiceState.CrackRawPenalty}。",
            $"开局 {HbrFateDiceState.BaseSlots} 个槽位，每打过一个首领多 1 个，最多 {HbrFateDiceState.MaxSlots} 个。" +
            "休息处可以修骰子，一次修一道裂痕。",
            "剑心跨战斗保留。"
        };
        if (!unlocked) lines.Add("战斗中不能装卸碎片。");
        side.AddChild(Text(string.Join("\n", lines), SmallSize, HbrManualPhoneStyle.Paragraph, wrap: true));
        return panel;
    }

    private static Control Stat(string caption, string value, Color color)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(HbrManualPhoneStyle.Label(caption, SmallSize, HbrManualPhoneStyle.Dim));
        box.AddChild(HbrManualPhoneStyle.Heading(value, 32, color));
        return box;
    }

    // Five sockets in slot order: fragments, empty slots, cracked slots, then the ones a boss has yet to open.
    private static Control SlotStrip(HbrFateDiceState state)
    {
        var strip = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        strip.AddThemeConstantOverride("separation", 8);
        for (int slot = 0; slot < HbrFateDiceState.MaxSlots; slot++)
        {
            NHbrFateSlotIcon icon = slot < state.Equipped.Length
                ? NHbrFateSlotIcon.ForFragment(HbrFateDiceCatalog.Find(state.Equipped[slot]), StripIcon)
                : slot < state.Capacity
                    ? NHbrFateSlotIcon.Create(NHbrFateSlotIcon.Look.Empty, StripIcon)
                    : slot < state.Slots
                        ? NHbrFateSlotIcon.Create(NHbrFateSlotIcon.Look.Cracked, StripIcon, crackSeed: slot)
                        : NHbrFateSlotIcon.Create(NHbrFateSlotIcon.Look.Locked, StripIcon);
            strip.AddChild(icon);
        }
        return strip;
    }

    // ---- slots, bag, merchant ------------------------------------------------------------------------------

    private void AddSlots(HbrFateDiceState state, bool hbr, bool canChange)
    {
        _list.AddChild(Text($"槽位（{state.Slots}）", TitleSize, Colors.White));
        for (int slot = 0; slot < state.Slots; slot++)
        {
            if (slot < state.Equipped.Length)
            {
                string id = state.Equipped[slot];
                HbrFateFragment fragment = HbrFateDiceCatalog.Find(id);
                if (fragment == null) continue;
                Button unequip = ActionButton("卸下", "unequip:" + id, canChange,
                    () => Send(new HbrFateDiceCommand { Op = HbrFateDiceOp.Unequip, Fragment = id }), secondary: true);
                _list.AddChild(FragmentRow(fragment, hbr, unequip));
            }
            else if (slot < state.Capacity)
            {
                string hint = state.Bag.Length > 0 ? "从下面的背包里选一片装上。" : "精英战、首领战和商人会给碎片。";
                _list.AddChild(Row(NHbrFateSlotIcon.Create(NHbrFateSlotIcon.Look.Empty, RowIcon), "空槽",
                    HbrManualPhoneStyle.Dim, null, default, hint, null));
            }
            else
            {
                _list.AddChild(Row(NHbrFateSlotIcon.Create(NHbrFateSlotIcon.Look.Cracked, RowIcon, crackSeed: slot), "裂痕",
                    CrackText, null, default, "这一格被裂痕占住，不能装碎片。休息处可以修骰子，一次修一道。", null));
            }
        }
        if (state.Slots < HbrFateDiceState.MaxSlots)
            _list.AddChild(Text($"还有 {HbrFateDiceState.MaxSlots - state.Slots} 个槽位没开：每打过一个首领开 1 个。",
                SmallSize, HbrManualPhoneStyle.Dim, wrap: true));
    }

    private void AddBag(HbrFateDiceState state, bool hbr, bool unlocked, bool pending)
    {
        _list.AddChild(Text($"背包（{state.Bag.Length}）", TitleSize, Colors.White));
        if (state.Bag.Length == 0)
        {
            _list.AddChild(Text("背包里没有碎片。精英战、首领战和商人会给。", SmallSize, HbrManualPhoneStyle.Dim, wrap: true));
            return;
        }
        bool full = state.Equipped.Length >= state.Capacity;
        if (unlocked && full)
            _list.AddChild(Text(state.Capacity > 0 ? "槽位装满了：先卸下一片，才能装上新的。" : "槽位全被裂痕占住：修好骰子才能装碎片。",
                SmallSize, HbrManualPhoneStyle.Dim, wrap: true));
        bool canEquip = unlocked && !pending && !full;
        foreach (string id in state.Bag)
        {
            HbrFateFragment fragment = HbrFateDiceCatalog.Find(id);
            if (fragment == null) continue;
            string fragmentId = id;
            Button equip = ActionButton("装上", "equip:" + id, canEquip,
                () => Send(new HbrFateDiceCommand { Op = HbrFateDiceOp.Equip, Fragment = fragmentId }));
            _list.AddChild(FragmentRow(fragment, hbr, equip));
        }
    }

    private void AddShop(HbrFateDiceState state, bool hbr, bool pending)
    {
        IReadOnlyList<HbrFateShopOffer> shop = _manual.CurrentFateShop;
        if (shop.Count == 0) return;
        string shopKey = _manual.CurrentFateShopKey;
        _list.AddChild(Text("本店碎片", TitleSize, Colors.White));
        _list.AddChild(Text("商人的货架上也摆着这几片，在哪边买都一样。", SmallSize, HbrManualPhoneStyle.Dim, wrap: true));
        foreach (HbrFateShopOffer offer in shop)
        {
            HbrFateFragment fragment = HbrFateDiceCatalog.Find(offer.Fragment);
            if (fragment == null) continue;
            HbrFateShopOffer entry = offer;
            var side = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            side.AddThemeConstantOverride("separation", 6);
            if (entry.Sold)
            {
                side.AddChild(HbrManualPhoneStyle.Label("已售出", SmallSize, HbrManualPhoneStyle.Dim));
            }
            else
            {
                HBoxContainer price = HbrEquipmentArt.MakePrice(entry.Price, 19);
                price.Alignment = BoxContainer.AlignmentMode.Center;
                if (_manual.Owner.Gold < entry.Price)
                    price.GetChild<Label>(price.GetChildCount() - 1).AddThemeColorOverride("font_color", CrackText);
                side.AddChild(price);
                string reason = pending ? "正在同步……"
                    : state.Owns(entry.Fragment) ? "已经有这片碎片了"
                    : _manual.Owner.Gold < entry.Price ? "金币不够" : null;
                bool confirming = _confirmBuy == entry.Fragment;
                Button buy = ActionButton(confirming ? "确定" : "购买", "buy:" + entry.Fragment, reason == null, () =>
                {
                    if (_confirmBuy != entry.Fragment)
                    {
                        _confirmBuy = entry.Fragment;
                        _confirmLeft = ConfirmSeconds;
                        _signature = null;
                        Refresh(_manual);
                        return;
                    }
                    _confirmBuy = null;
                    Send(new HbrFateDiceCommand { Op = HbrFateDiceOp.Buy, Fragment = entry.Fragment, ShopKey = shopKey });
                });
                buy.TooltipText = reason ?? (confirming ? "再按一次确认" : string.Empty);
                buy.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                side.AddChild(buy);
            }
            Control row = FragmentRow(fragment, hbr, side);
            if (entry.Sold) row.Modulate = new Color(1f, 1f, 1f, 0.55f);
            _list.AddChild(row);
        }
    }

    // ---- building blocks -----------------------------------------------------------------------------------

    private static Control FragmentRow(HbrFateFragment fragment, bool hbr, Control side)
    {
        Color rim = NHbrFateSlotIcon.RimColor(fragment);
        string tag = $"{HbrFateDiceCatalog.CategoryName(fragment.Category)}·{HbrFateDiceCatalog.RarityName(fragment.Rarity)}";
        return Row(NHbrFateSlotIcon.ForFragment(fragment, RowIcon), fragment.Name, Colors.White, tag, rim,
            HbrFateDiceCatalog.Describe(fragment, hbr), side);
    }

    // A list row in the equipment shop's look: a faint cyan wash with a rule below, the icon, the name with a
    // coloured tag, the text, and the action on the right.
    private static Control Row(Control icon, string title, Color titleColor, string tag, Color tagColor, string body,
        Control side)
    {
        var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        StyleBoxFlat style = HbrManualPhoneStyle.Fill(RowWash, 12, null, 0, 10f, 8f);
        style.BorderColor = HbrManualPhoneStyle.Rule;
        style.BorderWidthBottom = 1;
        row.AddThemeStyleboxOverride("panel", style);
        var layout = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 14);
        row.AddChild(layout);
        layout.AddChild(icon);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 2);
        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 12);
        heading.AddChild(HbrManualPhoneStyle.Label(title, TextSize, titleColor));
        if (!string.IsNullOrEmpty(tag)) heading.AddChild(HbrManualPhoneStyle.Label(tag, SmallSize, tagColor));
        text.AddChild(heading);
        if (!string.IsNullOrEmpty(body)) text.AddChild(Text(body, SmallSize, HbrManualPhoneStyle.Paragraph, wrap: true));
        layout.AddChild(text);
        if (side != null)
        {
            side.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            layout.AddChild(side);
        }
        return row;
    }

    // The round button of the equipment shop; 卸下 in the teal secondary colours.
    private Button ActionButton(string label, string key, bool enabled, System.Action press, bool secondary = false)
    {
        Button button = HbrEquipmentArt.MakeRoundButton(label);
        if (secondary)
        {
            HbrManualPhoneStyle.Secondary(button, SmallSize, 31);
            foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            {
                if (button.GetThemeStylebox(state) is not StyleBoxFlat style) continue;
                style.ContentMarginLeft = 2f;
                style.ContentMarginRight = 2f;
            }
        }
        button.Disabled = !enabled;
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.SetMeta(KeyMeta, key);
        button.Pressed += press;
        _buttons[key] = button;
        _order.Add(key);
        if (enabled) _firstButton ??= button;
        return button;
    }

    private (string Key, int Index) FocusedEntry()
    {
        if (GetViewport()?.GuiGetFocusOwner() is not Button focused || !IsAncestorOf(focused) || !focused.HasMeta(KeyMeta))
            return (null, -1);
        string key = focused.GetMeta(KeyMeta).AsString();
        return (key, _order.IndexOf(key));
    }

    private void RestoreFocus(string key, int index)
    {
        if (key == null || _order.Count == 0) return;
        Button target = _buttons.TryGetValue(key, out Button same) ? same
            : _buttons[_order[Mathf.Clamp(index, 0, _order.Count - 1)]];
        target.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Send(HbrFateDiceCommand command)
    {
        if (_manual == null || !HbrFateDiceNetActions.Request(_manual, command)) return;
        _signature = null;
        Refresh(_manual);
    }

    private static Label Text(string text, int size, Color color, bool wrap = false)
    {
        Label label = HbrManualPhoneStyle.Label(text, size, color, wrap);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }
}
