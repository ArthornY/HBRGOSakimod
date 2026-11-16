using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Equipment;

namespace HBRGOSakiMod.Core.Nodes.Screens;

public partial class NManualShopPanel
{
    private readonly Dictionary<string, Button> _actionButtons = new();
    private ColorRect _comparisonShade;
    private PanelContainer _comparisonPanel;
    private Label _comparisonTitle;
    private Label _comparisonHint;
    private HBoxContainer _comparisonCards;
    private HBoxContainer _comparisonActions;
    private ScrollContainer _comparisonScroll;
    private readonly List<PanelContainer> _comparisonColumns = new();
    private readonly List<Label> _comparisonTexts = new();
    private Button _comparisonOriginal;
    private Button _comparisonSecond;

    private Control ComparisonFocusedControl => _comparisonPanel?.Visible == true
        ? _comparisonOriginal is { Disabled: false } ? _comparisonOriginal
            : _comparisonSecond is { Disabled: false } ? _comparisonSecond : _comparisonPanel : null;

    private static RichTextLabel MakeOriginalPrice(HbrManualShopPriceView view)
    {
        // Label prints BBCode literally. RichTextLabel owns the actual strike
        // formatting while the current price keeps the existing coin widget.
        RichTextLabel price = new()
        {
            BbcodeEnabled = true, Text = view.OriginalBbCode, FitContent = true,
            ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(64f, 20f), MouseFilter = MouseFilterEnum.Ignore
        };
        price.AddThemeFontSizeOverride("normal_font_size", 14);
        price.AddThemeColorOverride("default_color", HbrManualPhoneArt.Muted);
        return price;
    }

    private void RegisterAction(string id, Button button) => _actionButtons[id] = button;
    private string RememberActionFocus()
    {
        Control focused = GetViewport()?.GuiGetFocusOwner();
        return _actionButtons.FirstOrDefault(pair => ReferenceEquals(pair.Value, focused)).Key;
    }
    private void RestoreActionFocus(string id, bool comparing)
    {
        if (!IsVisibleInTree()) return;
        if (comparing)
        {
            if (id != null && id.StartsWith("reforge.choose.", StringComparison.Ordinal) &&
                _actionButtons.TryGetValue(id, out Button choice) && !choice.Disabled) choice.GrabFocus();
            else ComparisonFocusedControl?.GrabFocus();
        }
        else if (id != null && _actionButtons.TryGetValue(id, out Button button) && !button.Disabled) button.GrabFocus();
        else if (id?.StartsWith("reforge.", StringComparison.Ordinal) == true) DefaultFocusedControl.GrabFocus();
    }

    private void BuildReforgeComparisonUi()
    {
        _comparisonShade = new ColorRect
        {
            Color = new Color(0.008f, 0.027f, 0.05f, 0.9f), Visible = false,
            MouseFilter = MouseFilterEnum.Stop, ZIndex = 30
        };
        _comparisonShade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_comparisonShade);
        _comparisonPanel = new PanelContainer
        { Visible = false, MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.All, ZIndex = 31 };
        _comparisonPanel.AddThemeStyleboxOverride("panel", HbrEquipmentArt.Panel(
            new Color(0.024f, 0.078f, 0.125f, 1f), HbrManualPhoneArt.Cyan2, 18));
        AddChild(_comparisonPanel);
        VBoxContainer body = new(); body.AddThemeConstantOverride("separation", 10);
        _comparisonPanel.AddChild(body);
        _comparisonTitle = HbrEquipmentArt.MakeLabel("重炼完成 · 选择保留一件", 23, Colors.White);
        _comparisonTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_comparisonTitle);
        _comparisonHint = HbrEquipmentArt.MakeLabel("", 15, HbrManualPhoneArt.Muted, 2);
        _comparisonHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_comparisonHint);
        _comparisonScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FocusMode = FocusModeEnum.None, FollowFocus = true
        };
        HbrManualPhoneStyle.Scroll(_comparisonScroll); body.AddChild(_comparisonScroll);
        _comparisonCards = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _comparisonCards.AddThemeConstantOverride("separation", (int)HbrManualShopPresentation.ComparisonGap);
        _comparisonScroll.AddChild(_comparisonCards);
        MarginContainer footer = new();
        footer.AddThemeConstantOverride("margin_right", (int)HbrManualShopPresentation.ComparisonScrollWidth);
        body.AddChild(footer);
        _comparisonActions = new HBoxContainer();
        _comparisonActions.AddThemeConstantOverride("separation", (int)HbrManualShopPresentation.ComparisonGap);
        footer.AddChild(_comparisonActions);
    }

    private void AddReforgeOffer(HbrEquipmentState state, int gold, bool editable)
    {
        HbrAlchemyTransaction transaction = state.LastAlchemy;
        if (transaction == null || !HbrEquipmentShop.CanReforge(_manual)) return;
        HbrEquipmentItem original = state.FindItem(transaction.OriginalItemId);
        if (original == null) return;
        int price = HbrSatsukiShopPolicy.ReforgePrice(transaction.PaidPrice);
        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", HbrEquipmentArt.Panel(
            new Color(0.06f, 0.16f, 0.23f, .94f), HbrManualPhoneArt.Cyan2, 14));
        VBoxContainer body = new(); body.AddThemeConstantOverride("separation", 8); panel.AddChild(body);
        Label title = HbrEquipmentArt.MakeLabel("商店店员的素养 · 本次可重炼一次", 18, HbrManualPhoneArt.Cyan);
        float width = Math.Max(100f, HbrManualShopPresentation.ListWidth(Size.X > 0f ? Size.X : HbrManualPhoneLayout.Body.Width) - 56f);
        title.CustomMinimumSize = new Vector2(width, 0f);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart; body.AddChild(title);
        Label detail = HbrEquipmentArt.MakeLabel($"刚炼得：{HbrEquipmentText.Name(original)}。再付 {price} 金币重炼，两件里保留一件。", 15, Colors.White);
        detail.CustomMinimumSize = new Vector2(width, 0f);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart; body.AddChild(detail);
        VBoxContainer actions = new(); actions.AddThemeConstantOverride("separation", 8); body.AddChild(actions);
        Button reforge = HbrEquipmentArt.MakeCapsule($"重炼一次 · {price}金币", 16, 46);
        reforge.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        reforge.Disabled = !editable || gold < price;
        reforge.TooltipText = gold < price ? "金币不足" : "支付后，比较原装备与重炼装备，再选择保留一件。";
        RegisterAction("reforge.start", reforge);
        reforge.Pressed += () => Send(new HbrEquipmentCommand
        { Revision = state.Revision, Op = HbrEquipmentOp.Reforge, TransactionId = transaction.Id }, false);
        actions.AddChild(reforge);
        Button keep = HbrEquipmentArt.MakeCapsule("保留当前", 16, 46);
        keep.SizeFlagsHorizontal = SizeFlags.ExpandFill; keep.Disabled = !editable;
        RegisterAction("reforge.keep", keep);
        keep.Pressed += () => ChooseReforge(transaction, 0);
        actions.AddChild(keep);
        _rows.AddChild(panel);
        LinkChoices(reforge, keep);
    }

    private void RefreshReforgeComparison(HbrEquipmentState state, bool editable)
    {
        HbrAlchemyTransaction transaction = state.LastAlchemy;
        bool visible = transaction?.ChoicePending == true;
        _comparisonShade.Visible = _comparisonPanel.Visible = visible;
        _comparisonColumns.Clear(); _comparisonTexts.Clear();
        HbrEquipmentArt.Clear(_comparisonCards);
        HbrEquipmentArt.Clear(_comparisonActions);
        _comparisonOriginal = _comparisonSecond = null;
        if (!visible) return;
        HbrEquipmentItem original = state.FindItem(transaction.OriginalItemId);
        HbrEquipmentClaimItem rolled = transaction.Second;
        HbrEquipmentItem second = new()
        { Id = transaction.OriginalItemId, Template = rolled.Template, Rolls = rolled.Rolls, Plus = rolled.Plus };
        _comparisonHint.Text = $"已支付 {HbrSatsukiShopPolicy.ReforgePrice(transaction.PaidPrice)} 金币重炼。选择保留一件，另一件将舍弃。关闭手册后仍可继续选择。";
        _comparisonOriginal = AddComparisonEquipment(original, "原装备", "保留原装备", 0, transaction,
            editable && HbrEquipmentShop.CanChooseReforge(_manual, 0));
        _comparisonSecond = AddComparisonEquipment(second, "重炼装备", "保留重炼装备", 1, transaction,
            editable && HbrEquipmentShop.CanChooseReforge(_manual, 1));
        LinkChoices(_comparisonOriginal, _comparisonSecond);
        ScrollBar bar = _comparisonScroll.GetVScrollBar();
        bar.FocusMode = FocusModeEnum.All;
        _comparisonOriginal.FocusNeighborTop = _comparisonSecond.FocusNeighborTop = bar.GetPath();
        bar.FocusNext = bar.FocusNeighborBottom = _comparisonOriginal.GetPath();
        bar.FocusPrevious = _comparisonSecond.GetPath();
        LayoutReforgeComparison();
    }

    private Button AddComparisonEquipment(HbrEquipmentItem item, string title, string choose, int index,
        HbrAlchemyTransaction transaction, bool enabled)
    {
        PanelContainer card = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StyleBoxFlat style = HbrEquipmentArt.Panel(new Color(.035f, .11f, .17f, .96f), HbrManualPhoneStyle.PanelEdge, 14);
        style.ContentMarginLeft = style.ContentMarginRight = HbrManualShopPresentation.ComparisonCardMargin;
        card.AddThemeStyleboxOverride("panel", style);
        VBoxContainer body = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10); card.AddChild(body);
        AddComparisonText(body, title, 18, HbrManualPhoneArt.Cyan);
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template != null)
        {
            Control icon = HbrEquipmentArt.MakeItemIcon(template.Icon, template.Rarity, 80f);
            icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter; body.AddChild(icon);
            AddComparisonText(body, HbrEquipmentText.Name(item), 19, Colors.White);
            AddComparisonText(body, HbrEquipmentText.KindName(template.Kind) + "　" + HbrEquipmentText.Stars(template.Rarity), 15, HbrManualPhoneArt.Muted);
            AddComparisonText(body, HbrEquipmentText.ItemText(item), 16, Colors.White);
            if (item.Plus.Length > 0) AddComparisonText(body, "+栏：" + HbrEquipmentText.PlusSlotsText(item), 15, HbrManualPhoneArt.Cyan);
        }
        else AddComparisonText(body, "装备已经变化，请刷新商店。", 16, HbrManualPhoneArt.Muted);
        Button button = HbrEquipmentArt.MakeCapsule(choose, 16, 50, selected: index == 1);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.Disabled = !enabled || template == null;
        button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            if (button.GetThemeStylebox(state) is StyleBoxFlat buttonStyle)
                buttonStyle.ContentMarginLeft = buttonStyle.ContentMarginRight = 6f;
        RegisterAction("reforge.choose." + index, button);
        button.Pressed += () => ChooseReforge(transaction, index);
        _comparisonActions.AddChild(button); _comparisonCards.AddChild(card); _comparisonColumns.Add(card);
        return button;
    }

    private void AddComparisonText(VBoxContainer parent, string text, int size, Color color)
    {
        Label label = HbrEquipmentArt.MakeLabel(text, size, color, 2);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.CustomMinimumSize = new Vector2(ComparisonLayout().TextWidth, 0f);
        _comparisonTexts.Add(label); parent.AddChild(label);
    }

    private HbrManualShopComparisonLayout ComparisonLayout() => HbrManualShopPresentation.Comparison(
        Size.X > 0f ? Size.X : HbrManualPhoneLayout.Body.Width,
        Size.Y > 0f ? Size.Y : HbrManualPhoneLayout.Body.Height);

    private void LayoutReforgeComparison()
    {
        if (_comparisonPanel == null) return;
        HbrManualShopComparisonLayout layout = ComparisonLayout();
        HbrPhoneRect rect = layout.Panel;
        HbrManualPhoneStyle.Pin(_comparisonPanel, Size, rect.X, rect.Y, rect.Width, rect.Height);
        float textWidth = Math.Max(1f, rect.Width - HbrManualShopPresentation.ComparisonInnerMargin * 2f);
        _comparisonTitle.CustomMinimumSize = _comparisonHint.CustomMinimumSize = new Vector2(textWidth, 0f);
        foreach (PanelContainer column in _comparisonColumns) column.CustomMinimumSize = new Vector2(layout.ColumnWidth, 0f);
        _comparisonCards.CustomMinimumSize = new Vector2(layout.ColumnWidth * 2f + HbrManualShopPresentation.ComparisonGap, 0f);
        foreach (Label label in _comparisonTexts) label.CustomMinimumSize = new Vector2(layout.TextWidth, 0f);
    }

    private void ChooseReforge(HbrAlchemyTransaction transaction, int index) => Send(new HbrEquipmentCommand
    { Revision = _manual.Equipment.Revision, Op = HbrEquipmentOp.ChooseReforge, TransactionId = transaction.Id, Index = index }, false);

    private static void LinkChoices(Button left, Button right)
    {
        left.FocusNeighborRight = left.FocusNext = left.GetPathTo(right);
        right.FocusNeighborLeft = right.FocusPrevious = right.GetPathTo(left);
        left.FocusNeighborLeft = left.FocusPrevious = left.GetPathTo(right);
        right.FocusNeighborRight = right.FocusNext = right.GetPathTo(left);
        left.FocusNeighborTop = left.FocusNeighborBottom = left.GetPath();
        right.FocusNeighborTop = right.FocusNeighborBottom = right.GetPath();
    }
}
