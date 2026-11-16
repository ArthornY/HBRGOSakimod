using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.FateDice;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HBRGOSakiMod.Core.Nodes.Merchant;

// 31O's fate fragments on the merchant's rug (design v3, section 4: 「商店：每家一到两块」; task: 「能做就把碎片
// 放上原版商人货架，复用现有购买命令」). The fragments do not become vanilla MerchantEntry items: those run the
// merchant's price hooks, purchase-triggered relics and restocking, which would change what a fragment costs and
// does. The stand instead sits in the rug's free column right of the card removal service and buys through
// HbrFateDiceNetActions with the same Buy command, shop key and price as the manual's 命运骰子 page, so the two
// stay in step and multiplayer goes through the existing synced action. Only the local 31O's own merchant
// screen gets a stand; it lays out its slots once the room's fragments can be read, and stays empty without.
public partial class NHbrFateFragmentShelf : Control
{
    internal const string NodeName = "HbrFateFragmentShelf";
    private const double PollSeconds = 0.2;
    private const ulong AwaitLimitMsec = 10000;

    private readonly List<NHbrFateFragmentShelfSlot> _slots = new();
    private NMerchantInventory _inventory;
    private ElectronicSoldierManual _manual;
    private string _signature;
    private double _poll;
    private string _awaiting;
    private ulong _awaitingSince;

    internal int Gold => _manual?.Owner?.Gold ?? 0;

    internal bool HbrNumerics => _manual?.IsHbrNumericsEnabled == true;

    // Called after the vanilla screen fills its slots.
    internal static void Attach(NMerchantInventory inventory)
    {
        if (inventory == null || inventory is NFakeMerchantInventory) return;
        Player player = inventory.Inventory?.Player;
        ElectronicSoldierManual manual = player?.GetRelic<ElectronicSoldierManual>();
        if (manual?.UsesFateDice != true || !LocalContext.IsMe(player)) return;
        Control rug = inventory.GetNodeOrNull<Control>("%SlotsContainer");
        if (rug == null || rug.GetNodeOrNull(NodeName) != null) return;
        var shelf = new NHbrFateFragmentShelf
        {
            Name = NodeName,
            MouseFilter = MouseFilterEnum.Ignore,
            _inventory = inventory,
            _manual = manual
        };
        rug.AddChild(shelf);
        shelf.Position = Vector2.Zero;
        shelf.Size = new Vector2(HbrFateFragmentShelfLayout.RugWidth, HbrFateFragmentShelfLayout.RugHeight);
    }

    public override void _Ready()
    {
        if (_manual != null) Refresh();
    }

    private void EnsureSlots(IReadOnlyList<HbrFateShopOffer> offers)
    {
        if (_slots.Count > 0 || offers.Count == 0) return;
        for (int i = 0; i < offers.Count; i++)
        {
            NHbrFateFragmentShelfSlot slot = NHbrFateFragmentShelfSlot.Create(this, offers[i], i, offers.Count);
            _slots.Add(slot);
            AddChild(slot);
        }
    }

    public override void _Process(double delta)
    {
        _poll -= delta;
        if (_poll > 0) return;
        _poll = PollSeconds;
        Refresh();
    }

    internal void TryBuy(NHbrFateFragmentShelfSlot slot)
    {
        if (_manual == null || slot?.Stocked != true || _awaiting != null || _manual.IsFateDicePending) return;
        HbrFateShopOffer offer = slot.Offer;
        if (Gold < offer.Price || _manual.FateDice.Owns(offer.Fragment))
        {
            Fail(slot, Gold < offer.Price ? PurchaseStatus.FailureGold : PurchaseStatus.FailureOutOfStock);
            return;
        }
        var command = new HbrFateDiceCommand { Op = HbrFateDiceOp.Buy, Fragment = offer.Fragment, ShopKey = _manual.CurrentFateShopKey };
        if (!HbrFateDiceNetActions.Request(_manual, command)) return;
        _awaiting = offer.Fragment;
        _awaitingSince = Time.GetTicksMsec();
        _signature = null;
        Refresh();
    }

    internal void PointHand(Control target) => _inventory?.MerchantHand?.PointAtTarget(target, Vector2.Zero);

    internal void StopHand() => _inventory?.MerchantHand?.StopPointing(2f);

    private void Refresh()
    {
        if (_manual == null || !_manual.UsesFateDice) return;
        IReadOnlyList<HbrFateShopOffer> offers = _manual.CurrentFateShop;
        EnsureSlots(offers);
        string signature = $"{string.Join(",", offers.Select(o => $"{o.Fragment}:{o.Sold}:{o.Price}"))}|{Gold}|{_manual.IsFateDicePending}";
        if (signature != _signature)
        {
            _signature = signature;
            foreach (NHbrFateFragmentShelfSlot slot in _slots)
            {
                HbrFateShopOffer offer = offers.FirstOrDefault(o => o.Fragment == slot.Offer?.Fragment);
                slot.SetOffer(offer ?? slot.Offer with { Sold = true }, Gold);
            }
            SettlePurchase(offers);
        }
        else if (_awaiting != null && Time.GetTicksMsec() - _awaitingSince > AwaitLimitMsec)
        {
            _awaiting = null;
        }
        Relink();
    }

    // The bought fragment shows as sold once the synced command has run; a command that ends without selling
    // it (another screen bought it first, or the gold went) clears the wait quietly.
    private void SettlePurchase(IReadOnlyList<HbrFateShopOffer> offers)
    {
        if (_awaiting == null) return;
        HbrFateShopOffer offer = offers.FirstOrDefault(o => o.Fragment == _awaiting);
        NHbrFateFragmentShelfSlot slot = _slots.FirstOrDefault(s => s.Offer?.Fragment == _awaiting);
        if (offer?.Sold == true)
        {
            _awaiting = null;
            Feedback(PurchaseStatus.Success, slot);
        }
        else if (!_manual.IsFateDicePending)
        {
            _awaiting = null;
        }
    }

    private void Fail(NHbrFateFragmentShelfSlot slot, PurchaseStatus status)
    {
        slot.Wiggle();
        Feedback(status, slot);
    }

    // The vanilla merchant's reactions: the thank-you or disappointed sound, his line, and the hand.
    private void Feedback(PurchaseStatus status, NHbrFateFragmentShelfSlot slot)
    {
        try
        {
            SfxCmd.Play(status == PurchaseStatus.Success
                ? "event:/sfx/npcs/merchant/merchant_thank_yous"
                : "event:/sfx/npcs/merchant/merchant_dissapointment");
            _inventory?.GetNodeOrNull<NMerchantDialogue>("%Dialogue")?.ShowForPurchaseAttempt(status);
            if (status == PurchaseStatus.Success && slot != null)
            {
                _inventory?.MerchantHand?.PointAtTarget(slot, Vector2.Zero);
                _inventory?.MerchantHand?.StopPointing(2f);
            }
        }
        catch (System.Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Fate fragment stand feedback failed: {error.Message}");
        }
    }

    // Controller focus: the card removal service (or, without it, the right-most relic or potion) leads right
    // into the stand; the stand runs top to bottom, its top slot reaching up to the right-most card. Vanilla
    // rebuilds its links after each of its own purchases, so this runs on every poll and only writes changes.
    private void Relink()
    {
        if (_inventory == null) return;
        List<NHbrFateFragmentShelfSlot> visible = _slots.Where(s => s.Visible && s.Stocked).ToList();
        Control left = LeftNeighbour();
        Control above = RightMost(_inventory.GetNodeOrNull<Control>("%CharacterCards"));
        if (left != null)
            SetNeighbour(left, Side.Right, visible.Count > 0 ? visible[0] : left);
        for (int i = 0; i < visible.Count; i++)
        {
            NHbrFateFragmentShelfSlot slot = visible[i];
            SetNeighbour(slot, Side.Left, left ?? slot);
            SetNeighbour(slot, Side.Right, slot);
            SetNeighbour(slot, Side.Top, i > 0 ? visible[i - 1] : above ?? slot);
            SetNeighbour(slot, Side.Bottom, i < visible.Count - 1 ? visible[i + 1] : slot);
        }
    }

    private Control LeftNeighbour()
    {
        if (_inventory.GetNodeOrNull<Control>("%MerchantCardRemoval") is { } removal && removal.IsVisibleInTree())
            return removal;
        return new[] { RightMost(_inventory.GetNodeOrNull<Control>("%Potions")), RightMost(_inventory.GetNodeOrNull<Control>("%Relics")) }
            .Where(slot => slot != null)
            .OrderByDescending(slot => slot.GlobalPosition.X)
            .FirstOrDefault();
    }

    private static Control RightMost(Control container) =>
        container?.GetChildren().OfType<NMerchantSlot>()
            .Where(slot => slot.IsVisibleInTree())
            .OrderByDescending(slot => slot.GlobalPosition.X)
            .FirstOrDefault();

    private static void SetNeighbour(Control from, Side side, Control to)
    {
        NodePath path = from.GetPathTo(to);
        if (from.GetFocusNeighbor(side).ToString() != path.ToString()) from.SetFocusNeighbor(side, path);
    }
}
