using System;
using System.Linq;

namespace HBRGOSakiMod.Core.Equipment;

// Authoritative, immutable transaction decisions. Clients supply an offer or
// transaction identity, never prices, discounts, generated rows or refunds.
internal static class HbrEquipmentPurchases
{
    private static HbrEquipmentOutcome Fail(string error) => new(null, 0, 0, error);
    internal static HbrEquipmentOutcome Buy(HbrEquipmentState state, HbrEquipmentCommand command,
        int tier, int act, int gold, HbrSatsukiShopPolicy policy)
    {
        HbrShopOffer offer = HbrEquipmentShop.Find(command.Offer, tier);
        if (offer == null) return Fail("这件商品现在没有货");
        int price = HbrEquipmentShop.Quote(state, offer, policy, act).Price;
        if (gold < price) return Fail("金币不足");
        HbrEquipmentState bought;
        int refund = 0;
        if (offer.Kind == HbrShopOfferKind.Craft)
        {
            bool forceHighest = policy.Enabled && policy.FirstAlchemyHighest && !state.AlchemyActs.Contains(act);
            var random = new HbrEquipmentRandom(state);
            HbrRolledItem rolled = HbrEquipmentGenerator.RollAlchemy(offer.CraftKind, tier, policy, forceHighest, random);
            if (policy.Enabled && policy.Refund) refund = policy.RefundGold(price, random.NextInt(100) + 1);
            bought = HbrEquipmentRules.AddItem(state with { RandomCounter = random.Counter },
                rolled.Template.Id, rolled.Rolls, rolled.Plus, out int itemId);
            if (itemId <= 0) return Fail("无法完成炼金");
            bought = bought with
            {
                AlchemyActs = state.AlchemyActs.Append(act).Distinct().ToArray(),
                LastAlchemy = new HbrAlchemyTransaction
                {
                    Id = $"alchemy:{act}:{bought.Revision}:{itemId}", Act = act, Tier = tier, Offer = offer.Id,
                    OriginalItemId = itemId, PaidPrice = price, Refund = refund,
                    StarPromotionEnabled = policy.Enabled, ExtraPlus = policy.ExtraPlus,
                    ForceHighest = forceHighest
                }
            };
        }
        else
        {
            bought = HbrEquipmentShop.Deliver(state, offer, tier, out _, out string error);
            if (bought == null) return Fail(error ?? "无法完成购买");
            if (bought.LastAlchemy is { DecisionClosed: false } previous)
                bought = bought with { LastAlchemy = previous with { DecisionClosed = true } };
        }
        return new(bought with { PurchasedActs = state.PurchasedActs.Append(act).Distinct().ToArray() }, price, refund, null);
    }

    internal static bool CanReforge(HbrEquipmentState state, HbrSatsukiShopPolicy policy, bool loadoutUnlocked) =>
        policy.Enabled && policy.Reforge && state.LastAlchemy is
            { ReforgeUsed: false, DecisionClosed: false, Second: null } transaction &&
        state.FindItem(transaction.OriginalItemId) != null &&
        (loadoutUnlocked || !state.IsEquipped(transaction.OriginalItemId));

    internal static bool CanChoose(HbrEquipmentState state, int index, bool loadoutUnlocked) =>
        state.LastAlchemy is { DecisionClosed: false } transaction && index is 0 or 1 &&
        state.FindItem(transaction.OriginalItemId) != null &&
        (index == 0 || transaction.Second != null && (loadoutUnlocked || !state.IsEquipped(transaction.OriginalItemId)));

    internal static HbrEquipmentOutcome Reforge(HbrEquipmentState state, HbrEquipmentCommand command,
        bool loadoutUnlocked, int gold, HbrSatsukiShopPolicy policy)
    {
        if (!CanReforge(state, policy, loadoutUnlocked) || command.TransactionId != state.LastAlchemy.Id)
            return Fail("这次炼金无法再重炼");
        HbrAlchemyTransaction transaction = state.LastAlchemy;
        int price = HbrSatsukiShopPolicy.ReforgePrice(transaction.PaidPrice);
        if (gold < price) return Fail("金币不足");
        HbrShopOffer offer = HbrEquipmentShop.Find(transaction.Offer, transaction.Tier);
        if (offer?.Kind != HbrShopOfferKind.Craft) return Fail("这次炼金的商品无效");
        var random = new HbrEquipmentRandom(state);
        var generation = new HbrSatsukiShopPolicy(transaction.StarPromotionEnabled, false,
            transaction.ExtraPlus, false, false, false, false, false, 0);
        HbrRolledItem rolled = HbrEquipmentGenerator.RollAlchemy(offer.CraftKind, transaction.Tier,
            generation, transaction.ForceHighest, random);
        HbrEquipmentState pending = state with
        {
            RandomCounter = random.Counter,
            LastAlchemy = transaction with
            {
                ReforgeUsed = true, Second = new(rolled.Template.Id, rolled.Rolls, rolled.Plus)
            }
        };
        return new(HbrEquipmentRules.Commit(pending), price, 0, null);
    }

    internal static HbrEquipmentOutcome Choose(HbrEquipmentState state, HbrEquipmentCommand command,
        bool loadoutUnlocked)
    {
        if (!CanChoose(state, command.Index, loadoutUnlocked) || command.TransactionId != state.LastAlchemy.Id)
            return Fail("这次炼金已经选择过了");
        HbrAlchemyTransaction transaction = state.LastAlchemy;
        HbrEquipmentState result = state;
        if (command.Index == 1)
        {
            HbrEquipmentClaimItem candidate = transaction.Second;
            var replacement = new HbrEquipmentItem { Id = transaction.OriginalItemId,
                Template = candidate.Template, Rolls = candidate.Rolls, Plus = candidate.Plus };
            result = HbrEquipmentRules.Trim(state with
            { Items = state.Items.Select(item => item.Id == replacement.Id ? replacement : item).ToArray() });
        }
        return new(HbrEquipmentRules.Commit(result with
        { LastAlchemy = transaction with { Second = null, DecisionClosed = true } }), 0, 0, null);
    }
}
