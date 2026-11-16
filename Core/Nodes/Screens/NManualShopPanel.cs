using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Equipment;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Nodes.Screens;

// The manual's shop, laid out like the original ショップ: the FriendStreetShop
// background, the clerk on the left with her bubble and the goods list on the
// right. Rows are rebuilt only when the saved state, gold or tab changes.
// Panels, tabs, rows and buttons follow the phone manual's page style.
public partial class NManualShopPanel : Control
{
    private enum ShopTab { Shop, Alchemy, Sell }

    // The clerk's lines from the original shop script, translated.
    private static readonly string[] ShopLines =
    [
        "欢迎光临！！每件商品的效果都『物有所值』哦♪",
        "全都是推荐商品哦♪",
        "金币留着也没什么用嘛♪",
        "给我把准备做到万全♪",
        "就凭那身装备没问题吗♪",
        "物有所值的价值才是信赖的证明♪"
    ];

    // The original alchemy tutorial lines (China-server text).
    private static readonly string[] AlchemyLines =
    [
        "通过炼金获得的饰品属性是随机的，反复挑战直到获得想要的饰品吧。",
        "在炼金中可以获得强力的饰品。"
    ];

    private const double ConfirmSeconds = 3d;
    private const float KeeperAspect = 478f / 857f;
    private const float BubbleMaxWidth = 320f;
    private const float BubblePadding = 16f;

    private ElectronicSoldierManual _manual;
    private ShopTab _tab;
    private TextureRect _background;
    private ColorRect _shade;
    private TextureRect _keeper;
    private PanelContainer _bubble;
    private Label _bubbleText;
    private float _bubbleWidth = BubbleMaxWidth;
    private HBoxContainer _tabBar;
    private readonly Dictionary<ShopTab, Button> _tabs = new();
    private PanelContainer _wallet;
    private Label _gold;
    private Label _materials;
    private PanelContainer _listPanel;
    private ScrollContainer _scroll;
    private VBoxContainer _rows;
    private Label _status;
    private string _lastState;
    private string _confirmId;
    private double _confirmLeft;
    private int _awaitingAfterId = -1;
    private int _awaitingRevision = -1;
    private HbrEquipmentOp _awaitingOp;
    private string _awaitingTransactionId;
    private HbrSatsukiShopPolicy _shopPolicy;
    private int _shopAct;
    private bool _compactOffers;
    private int _line;

    internal Control DefaultFocusedControl => ComparisonFocusedControl ??
        (_tabs.TryGetValue(_tab, out Button button) ? button : this);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUi();
        LayoutContent();
        Refresh(_manual);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) LayoutContent();
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
        if (_rows == null || _status == null || manual == null) return;
        HbrEquipmentState state = manual.Equipment;
        int gold = manual.Owner?.Gold ?? 0;
        bool editable = HbrEquipmentNetActions.CanRequest(manual) && !manual.IsEquipmentPending;
        _shopPolicy = HbrSatsukiShopPolicy.Get(manual.Owner);
        _shopAct = manual.Owner?.RunState.CurrentActIndex ?? -1;
        bool comparing = state.LastAlchemy?.ChoicePending == true;
        string key = $"{_tab}|{state.Revision}|{state.Items.Length}|{gold}|{manual.EquipmentTier}|{editable}|{_confirmId}|{_shopPolicy.CacheKey}|{_shopAct}|{_compactOffers}";
        if (key == _lastState) return;
        _lastState = key;
        string focus = RememberActionFocus();
        ReportResult(state);
        _gold.Text = $"金币  {gold}";
        _materials.Text = $"持有　{HbrEquipmentCatalog.PlusReleaseName} ×{state.PlusRelease}　{HbrEquipmentCatalog.PlusChangeName} ×{state.PlusChange}";
        foreach ((ShopTab tab, Button button) in _tabs)
        { HbrEquipmentArt.StyleCapsule(button, tab == _tab); button.Disabled = comparing; }
        _actionButtons.Clear();
        HbrEquipmentArt.Clear(_rows);
        if (_tab != ShopTab.Sell && !comparing) AddReforgeOffer(state, gold, editable);
        switch (_tab)
        {
            case ShopTab.Shop:
                foreach (HbrShopOffer offer in HbrEquipmentShop.ShopOffers(manual.EquipmentTier))
                    AddOfferRow(offer, state, gold, editable && !comparing);
                break;
            case ShopTab.Alchemy:
                foreach (HbrShopOffer offer in HbrEquipmentShop.AlchemyOffers(manual.EquipmentTier))
                    AddOfferRow(offer, state, gold, editable && !comparing);
                break;
            default:
                AddSellRows(state, editable && !comparing);
                break;
        }
        RefreshReforgeComparison(state, editable);
        RestoreActionFocus(focus, comparing);
    }

    private void BuildUi()
    {
        _background = new TextureRect
        {
            Texture = HbrEquipmentArt.Load(HbrEquipmentArt.ShopBackground),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            // Tinted toward the phone's navy so the frosted panels read over the street.
            Modulate = new Color(0.52f, 0.6f, 0.68f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_background);
        _shade = new ColorRect { Color = new Color(0.016f, 0.063f, 0.1f, 0.42f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_shade);
        _keeper = new TextureRect
        {
            Texture = HbrEquipmentArt.Load(HbrEquipmentArt.Shopkeeper),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_keeper);

        _bubble = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        StyleBoxFlat bubbleStyle = HbrEquipmentArt.Panel(new Color(1f, 1f, 1f, 0.94f), HbrManualPhoneArt.Cyan2, 18, 2);
        bubbleStyle.ContentMarginLeft = BubblePadding;
        bubbleStyle.ContentMarginRight = BubblePadding;
        bubbleStyle.ContentMarginTop = 10f;
        bubbleStyle.ContentMarginBottom = 10f;
        _bubble.AddThemeStyleboxOverride("panel", bubbleStyle);
        // An autowrapping label enters the tree 1px wide and wraps one glyph per line, and a container
        // only ever grows to fit its children. Without a set text width and the refit below, the bubble
        // stayed almost as tall as the page and its white fill covered the clerk.
        _bubbleText = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(BubbleMaxWidth - 2f * BubblePadding, 0f)
        };
        _bubbleText.AddThemeFontSizeOverride("font_size", 18);
        _bubbleText.AddThemeColorOverride("font_color", new Color("#16303a"));
        _bubble.AddChild(_bubbleText);
        AddChild(_bubble);
        _bubble.MinimumSizeChanged += FitBubble;

        _tabBar = new HBoxContainer();
        _tabBar.AddThemeConstantOverride("separation", 6);
        AddChild(_tabBar);
        AddTab(ShopTab.Shop, "商店", HbrEquipmentArt.Load(HbrEquipmentArt.ShopIcon));
        AddTab(ShopTab.Alchemy, "炼金", HbrEquipmentArt.Load(HbrEquipmentArt.AlchemyIcon));
        AddTab(ShopTab.Sell, "出售", HbrEquipmentArt.CoinTexture);

        _wallet = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        // The home screen's capsule: teal glass, no edge.
        _wallet.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Fill(new Color(0.369f, 0.659f, 0.729f, 0.6f), 22,
            null, 0, 14f, 6f));
        HBoxContainer walletRow = new() { MouseFilter = MouseFilterEnum.Ignore };
        walletRow.AddThemeConstantOverride("separation", 8);
        walletRow.AddChild(HbrEquipmentArt.Picture(HbrEquipmentArt.CoinTexture, new Vector2(26, 26)));
        _gold = HbrEquipmentArt.MakeLabel("", 20, Colors.White);
        walletRow.AddChild(_gold);
        _wallet.AddChild(walletRow);
        AddChild(_wallet);

        _listPanel = new PanelContainer();
        _listPanel.AddThemeStyleboxOverride("panel", HbrManualPhoneStyle.Fill(new Color(0.024f, 0.078f, 0.125f, 0.82f), 18,
            HbrManualPhoneStyle.PanelEdge, 1, 14f, 10f));
        AddChild(_listPanel);
        VBoxContainer listLayout = new();
        listLayout.AddThemeConstantOverride("separation", 6);
        _listPanel.AddChild(listLayout);
        _materials = HbrEquipmentArt.MakeLabel("", 15, HbrManualPhoneArt.Muted, 3);
        listLayout.AddChild(_materials);
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None
        };
        HbrManualPhoneStyle.Scroll(_scroll);
        listLayout.AddChild(_scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 2);
        _scroll.AddChild(_rows);
        _status = HbrEquipmentArt.MakeLabel("原版商人不出售这些装备。战斗中也可以购买和炼金，但要在战斗外更换装备。", 15,
            HbrManualPhoneArt.Muted, 3);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        listLayout.AddChild(_status);
        BuildReforgeComparisonUi();
        ShowLine();
    }

    private void AddTab(ShopTab tab, string text, Texture2D icon)
    {
        Button button = HbrEquipmentArt.MakeCapsule(text, 19, 48f, tab == _tab);
        button.CustomMinimumSize = new Vector2(132, 48);
        button.Icon = icon;
        button.ExpandIcon = true;
        button.AddThemeConstantOverride("icon_max_width", 30);
        button.Pressed += () => SelectTab(tab);
        _tabBar.AddChild(button);
        _tabs[tab] = button;
    }

    private void SelectTab(ShopTab tab)
    {
        if (_tab == tab) return;
        _tab = tab;
        _confirmId = null;
        _line = 0;
        ShowLine();
        _lastState = null;
        Refresh(_manual);
    }

    private void ShowLine()
    {
        string[] lines = _tab == ShopTab.Alchemy ? AlchemyLines : ShopLines;
        _bubbleText.Text = lines[_line % lines.Length];
    }

    private void LayoutContent()
    {
        if (_listPanel == null || Size.X <= 0 || Size.Y <= 0) return;
        bool compact = HbrManualShopPresentation.CompactOffers(Size.X);
        bool changed = compact != _compactOffers;
        _compactOffers = compact;
        _background.Position = Vector2.Zero;
        _background.Size = Size;
        _shade.Position = Vector2.Zero;
        _shade.Size = Size;
        bool wide = Size.X >= 760f;
        _keeper.Visible = wide;
        _bubble.Visible = wide;
        _tabBar.Position = new Vector2(12f, 8f);
        // Three 132px tabs take about 410px; the gold pill sits to their right,
        // or below them when the page is too narrow.
        bool walletBesideTabs = Size.X - 182f >= 430f;
        _wallet.Size = new Vector2(170f, 44f);
        _wallet.Position = walletBesideTabs ? new Vector2(Size.X - 182f, 10f) : new Vector2(12f, 62f);
        float top = walletBesideTabs ? 64f : 114f;
        float listLeft = wide ? Size.X * 0.4f : 12f;
        HbrManualPhoneStyle.Pin(_listPanel, Size, listLeft, top, Size.X - listLeft - 12f, Math.Max(0f, Size.Y - top - 12f));
        LayoutReforgeComparison();
        if (changed) { _lastState = null; Refresh(_manual); }
        if (!wide) return;
        // The clerk stands at the bottom and leaves room above her head for the
        // bubble, as in the original shop screen.
        float keeperHeight = Size.Y * 0.8f;
        float keeperWidth = keeperHeight * KeeperAspect;
        _keeper.Size = new Vector2(keeperWidth, keeperHeight);
        _keeper.Position = new Vector2(Math.Max(0f, listLeft - keeperWidth) * 0.45f, Size.Y - keeperHeight);
        _bubbleWidth = Math.Min(BubbleMaxWidth, listLeft - 24f);
        _bubbleText.CustomMinimumSize = new Vector2(_bubbleWidth - 2f * BubblePadding, 0f);
        _bubble.Position = new Vector2(16f, top + 4f);
        FitBubble();
    }

    // The bubble is as tall as its text: setting a zero height clamps it back to the minimum, which
    // follows the label's wrapped lines. This runs again whenever the text's height settles.
    private void FitBubble()
    {
        if (_bubble == null) return;
        _bubble.Size = new Vector2(_bubbleWidth, 0f);
    }

    private void AddOfferRow(HbrShopOffer offer, HbrEquipmentState state, int gold, bool editable)
    {
        HbrEquipmentTemplate template = offer.Template;
        string icon, name, description;
        int rarity;
        switch (offer.Kind)
        {
            case HbrShopOfferKind.Item:
                icon = template.Icon;
                rarity = template.Rarity;
                name = template.Name;
                int owned = state.Items.Count(item => item.Template == template.Id);
                description = $"{template.Flavor}\n{HbrEquipmentText.Describe(template)}" + (owned > 0 ? $"　持有 {owned}" : "");
                break;
            case HbrShopOfferKind.PlusRelease:
                icon = HbrEquipmentCatalog.PlusReleaseIcon;
                rarity = 0;
                name = HbrEquipmentCatalog.PlusReleaseName;
                description = $"用于解锁饰品+栏位的道具\n持有 {state.PlusRelease}";
                break;
            case HbrShopOfferKind.PlusChange:
                icon = HbrEquipmentCatalog.PlusChangeIcon;
                rarity = 0;
                name = HbrEquipmentCatalog.PlusChangeName;
                description = $"用于变更饰品+栏位效果的道具\n持有 {state.PlusChange}";
                break;
            default:
                HbrEquipmentTemplate sample = HbrEquipmentCatalog.OfKind(offer.CraftKind)
                    .Where(row => row.InPool(HbrEquipmentPool.Alchemy)).OrderBy(row => row.Rarity).Last();
                icon = sample.Icon;
                rarity = 0;
                name = $"炼金：{HbrEquipmentText.KindName(offer.CraftKind)}";
                description = offer.CraftKind == HbrEquipmentKind.Charm
                    ? HbrEquipmentText.CharmCraftText(_manual.EquipmentTier, HbrEquipmentShop.CharmCraftChance(_manual))
                    : CraftDescription(offer.CraftKind, _manual.EquipmentTier, _shopPolicy,
                        !state.AlchemyActs.Contains(Math.Max(0, _shopAct)));
                break;
        }
        HbrShopQuote quote = HbrEquipmentShop.Quote(state, offer, _shopPolicy, _shopAct);
        bool affordable = gold >= quote.Price;
        AddRow(icon, rarity, name, description, quote.Price, offer.Id,
            editable && affordable, affordable ? null : "金币不足",
            () => Purchase(offer), originalPrice: quote.OriginalPrice);
    }

    private static string CraftDescription(HbrEquipmentKind kind, int tier,
        HbrSatsukiShopPolicy policy = default, bool firstAlchemy = false)
    {
        if (kind == HbrEquipmentKind.Charm) return HbrEquipmentText.CharmCraftText(tier);
        IReadOnlyList<int> weights = HbrEquipmentGenerator.RarityWeightsFor(tier, alchemy: true);
        string odds = string.Join("　", weights.Select((weight, index) => (weight, index))
            .Where(pair => pair.weight > 0).Select(pair => $"★{pair.index + 1} {pair.weight}%"));
        // ★6 rings come from battles only, as from the original corridors.
        string ringNote = !policy.Enabled && kind == HbrEquipmentKind.Ring && weights[^1] > 0
            ? "\n★6 戒指只能在战斗中缴获，这里改为 ★5" : string.Empty;
        string changes = policy.Enabled ? "\n佐月：30%概率多一星，最高★6。" : string.Empty;
        if (policy.Enabled && policy.FirstAlchemyHighest && firstAlchemy)
            changes += $"\n本幕首次炼金：先生成★{HbrSatsukiShopPolicy.HighestAlchemyRarity(tier)}，再判定升星。";
        if (policy.Enabled && policy.ExtraPlus) changes += "\n已解锁+栏额外增加1个，不超过栏位上限。";
        if (policy.Enabled && kind == HbrEquipmentKind.Necklace)
            changes += "\n项链没有★2，缺档沿用相邻星级商品。";
        string label = policy.Enabled ? "基础星级权重（强化前）" : "星级权重";
        return $"随机获得一件{HbrEquipmentText.KindName(kind)}，可能自带已解锁的+栏\n{label}：{odds}{ringNote}{changes}";
    }

    private void AddSellRows(HbrEquipmentState state, bool editable)
    {
        List<HbrEquipmentItem> items = state.Items.Where(item => !state.IsEquipped(item.Id))
            .OrderBy(item => HbrEquipmentCatalog.Find(item.Template).Kind).ThenByDescending(item => item.Id).ToList();
        if (items.Count == 0)
        {
            Label empty = HbrEquipmentArt.MakeLabel("没有可以出售的装备。已装备的物品要先在「装备」页卸下。", 17, HbrManualPhoneArt.Muted, 3);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _rows.AddChild(empty);
            return;
        }
        foreach (HbrEquipmentItem item in items)
        {
            HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item.Template);
            int price = HbrEquipmentShop.SellPrice(item);
            AddRow(template.Icon, template.Rarity, HbrEquipmentText.Name(item),
                $"{HbrEquipmentText.KindName(template.Kind)}　{HbrEquipmentText.ItemText(item)}",
                price, $"sell:{item.Id}", editable, null, () => Sell(item.Id), "出售");
        }
    }

    private void AddRow(string icon, int rarity, string name, string description, int price, string confirmId,
        bool enabled, string disabledReason, Action action, string verb = "购买", int? originalPrice = null)
    {
        PanelContainer row = new();
        StyleBoxFlat line = new() { BgColor = new Color(0.588f, 0.941f, 0.98f, 0.03f) };
        line.BorderColor = HbrManualPhoneStyle.Rule;
        line.BorderWidthBottom = 1;
        line.ContentMarginLeft = 8f;
        line.ContentMarginRight = 8f;
        line.ContentMarginTop = 8f;
        line.ContentMarginBottom = 8f;
        row.AddThemeStyleboxOverride("panel", line);
        VBoxContainer rowBody = new(); rowBody.AddThemeConstantOverride("separation", 6);
        row.AddChild(rowBody);
        HBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 12);
        rowBody.AddChild(layout);
        layout.AddChild(HbrEquipmentArt.MakeItemIcon(icon, rarity, 64f));
        VBoxContainer text = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        Label heading = HbrEquipmentArt.MakeLabel(name, 20, Colors.White);
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        heading.CustomMinimumSize = new Vector2(100f, 0f);
        text.AddChild(heading);
        Label detail = HbrEquipmentArt.MakeLabel(description, 15, HbrManualPhoneStyle.Text, 3);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.CustomMinimumSize = new Vector2(100f, 0f);
        text.AddChild(detail);
        layout.AddChild(text);
        VBoxContainer side = new() { Alignment = BoxContainer.AlignmentMode.Center };
        HbrManualShopPriceView priceView = HbrManualShopPriceView.From(originalPrice ?? price, price);
        if (priceView.Discounted)
        {
            side.AddChild(MakeOriginalPrice(priceView));
            side.AddChild(HbrEquipmentArt.MakeLabel(priceView.Badge, 13, HbrManualPhoneArt.Cyan, 2));
        }
        side.AddChild(HbrEquipmentArt.MakePrice(price, 19));
        bool confirming = _confirmId == confirmId;
        Button buy = HbrEquipmentArt.MakeRoundButton(confirming ? "确定" : verb);
        buy.Disabled = !enabled;
        buy.TooltipText = disabledReason ?? (confirming ? "再按一次确认" : string.Empty);
        buy.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        RegisterAction(confirmId, buy);
        buy.Pressed += () =>
        {
            if (_confirmId != confirmId)
            {
                _confirmId = confirmId;
                _confirmLeft = ConfirmSeconds;
                _lastState = null;
                Refresh(_manual);
                return;
            }
            _confirmId = null;
            action();
        };
        if (_compactOffers)
        {
            HBoxContainer footer = new(); rowBody.AddChild(footer);
            footer.AddChild(side);
            footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            footer.AddChild(buy);
        }
        else { layout.AddChild(side); layout.AddChild(buy); }
        _rows.AddChild(row);
    }

    private void Purchase(HbrShopOffer offer)
    {
        Send(new HbrEquipmentCommand { Revision = _manual.Equipment.Revision, Op = HbrEquipmentOp.Buy, Offer = offer.Id },
            offer.Kind is HbrShopOfferKind.Item or HbrShopOfferKind.Craft);
    }

    private void Sell(int itemId)
    {
        Send(new HbrEquipmentCommand { Revision = _manual.Equipment.Revision, Op = HbrEquipmentOp.Sell, ItemId = itemId },
            false);
    }

    private void Send(HbrEquipmentCommand command, bool reportNewItem)
    {
        HbrEquipmentState state = _manual.Equipment;
        HbrEquipmentOutcome check = HbrEquipmentCommands.Evaluate(state, command, _manual.EquipmentTier,
            ElectronicSoldierManual.IsEquipmentLoadoutUnlocked, _manual.Owner?.Gold ?? 0,
            HbrSatsukiShopPolicy.Get(_manual.Owner), _manual.Owner?.RunState.CurrentActIndex ?? -1);
        if (!check.Succeeded)
        {
            _status.Text = check.Error;
            _lastState = null;
            Refresh(_manual);
            return;
        }
        if (!HbrEquipmentNetActions.Request(_manual, command))
        {
            _status.Text = "现在无法操作，请稍后再试。";
            return;
        }
        _awaitingAfterId = reportNewItem ? state.NextItemId - 1 : -1;
        _awaitingRevision = state.Revision;
        _awaitingOp = command.Op;
        _awaitingTransactionId = command.TransactionId;
        _status.Text = "交易处理中…";
        _line++;
        ShowLine();
        _lastState = null;
        Refresh(_manual);
    }

    // After a purchase or alchemy, name what arrived once the state changes.
    private void ReportResult(HbrEquipmentState state)
    {
        if (_awaitingRevision < 0) return;
        if (state.Revision == _awaitingRevision)
        {
            if (_manual.IsEquipmentPending) return;
            _status.Text = "交易未完成，请重新操作。";
            _awaitingRevision = -1;
            _awaitingAfterId = -1;
            return;
        }
        _awaitingRevision = -1;
        if (_awaitingOp == HbrEquipmentOp.Reforge)
        {
            _status.Text = state.LastAlchemy is { ChoicePending: true } pending && pending.Id == _awaitingTransactionId
                ? "重炼完成，请选择保留一件。" : "装备已更新，请确认当前结果。";
            return;
        }
        if (_awaitingOp == HbrEquipmentOp.ChooseReforge)
        {
            if (state.LastAlchemy?.Id != _awaitingTransactionId || !state.LastAlchemy.DecisionClosed)
            { _status.Text = "装备已更新，请确认当前结果。"; return; }
            HbrEquipmentItem chosen = state.FindItem(state.LastAlchemy?.OriginalItemId ?? 0);
            _status.Text = chosen == null ? "选择已完成。" : "已保留：" + HbrEquipmentText.Name(chosen) + "。";
            return;
        }
        if (_awaitingAfterId < 0)
        { _status.Text = _awaitingOp == HbrEquipmentOp.Sell ? "出售完成。" : "交易完成。"; return; }
        HbrEquipmentItem newest = state.Items.Where(item => item.Id > _awaitingAfterId).OrderBy(item => item.Id).LastOrDefault();
        if (newest == null) return;
        _awaitingAfterId = -1;
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(newest.Template);
        _status.Text = $"获得：{HbrEquipmentText.Name(newest)}（{HbrEquipmentText.Stars(template.Rarity)}）　" +
            HbrEquipmentText.ItemText(newest) +
            (newest.Plus.Length > 0 ? "　+栏：" + HbrEquipmentText.PlusSlotsText(newest) : "") +
            (state.LastAlchemy is { Refund: > 0 } transaction && transaction.OriginalItemId == newest.Id
                ? $"　返还 {transaction.Refund} 金币。" : string.Empty);
    }
}
