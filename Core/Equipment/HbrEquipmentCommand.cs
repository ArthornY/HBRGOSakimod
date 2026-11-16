using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Equipment;

internal enum HbrEquipmentOp { Equip, Unequip, Buy, OpenPlus, ChangePlus, Sell, Claim, Reforge, ChooseReforge }

// A reward's goods travel inside the claim so every peer adds the same rows.
internal sealed record HbrEquipmentClaimItem(string Template, int[] Rolls, HbrPlusSlot[] Plus);

internal sealed record HbrEquipmentClaim
{
    public string Receipt { get; init; } = string.Empty;
    public HbrEquipmentClaimItem[] Items { get; init; } = Array.Empty<HbrEquipmentClaimItem>();
    public int PlusRelease { get; init; }
    public int PlusChange { get; init; }

    internal static HbrEquipmentClaim ForItem(string receipt, HbrRolledItem item) => new()
    {
        Receipt = receipt,
        Items =
        [
            new HbrEquipmentClaimItem(item.Template.Id, item.Rolls ?? Array.Empty<int>(),
                item.Plus ?? Array.Empty<HbrPlusSlot>())
        ]
    };

    internal static HbrEquipmentClaim ForMaterials(string receipt, int plusRelease, int plusChange) => new()
    {
        Receipt = receipt,
        PlusRelease = Math.Max(0, plusRelease),
        PlusChange = Math.Max(0, plusChange)
    };

    // One reward button per item, plus one for the two materials.
    internal static IReadOnlyList<HbrEquipmentClaim> FromDrop(string roomKey, HbrEquipmentDrop drop)
    {
        var claims = drop.Items.Select((item, index) => ForItem($"claim:{roomKey}:{index}", item)).ToList();
        if (drop.PlusRelease > 0 || drop.PlusChange > 0)
            claims.Add(ForMaterials($"claim:{roomKey}:materials", drop.PlusRelease, drop.PlusChange));
        return claims;
    }

    internal HbrEquipmentState ApplyTo(HbrEquipmentState state)
    {
        if (state.HasReceipt(Receipt)) return null;
        foreach (HbrEquipmentClaimItem item in Items ?? Array.Empty<HbrEquipmentClaimItem>())
            state = HbrEquipmentRules.AddItem(state, item.Template, item.Rolls, item.Plus, out _);
        state = HbrEquipmentRules.AddMaterials(state, PlusRelease, PlusChange);
        return HbrEquipmentRules.Commit(state.WithReceipt(Receipt));
    }
}

internal sealed record HbrEquipmentCommand
{
    public int Revision { get; init; }
    public HbrEquipmentOp Op { get; init; }
    public int ItemId { get; init; }
    public int Slot { get; init; } = -1;
    public int Index { get; init; } = -1;
    public string Offer { get; init; } = string.Empty;
    public HbrEquipmentClaim Claim { get; init; }
    public string TransactionId { get; init; } = string.Empty;
}

internal readonly record struct HbrEquipmentOutcome(HbrEquipmentState State, int GoldCost, int GoldGain, string Error)
{
    internal bool Succeeded => State != null;
}

// The whole decision for one command, without game objects, so every peer
// and the offline checks reach the same result from the same inputs.
internal static class HbrEquipmentCommands
{
    internal const string LoadoutLocked = "战斗中不能更换装备";

    internal static HbrEquipmentOutcome Evaluate(HbrEquipmentState state, HbrEquipmentCommand command, int tier,
        bool loadoutUnlocked, int gold, HbrSatsukiShopPolicy policy = default, int actIndex = -1)
    {
        if (state == null || command == null) return Fail("无效的操作");
        if (command.Op != HbrEquipmentOp.Claim && command.Revision != state.Revision)
            return Fail("装备已经变化，请重新操作");
        if (state.LastAlchemy?.ChoicePending == true && command.Op is not (HbrEquipmentOp.ChooseReforge or HbrEquipmentOp.Claim))
            return Fail("请先选择保留哪一件炼金装备");
        int act = Math.Max(0, actIndex < 0 ? tier : actIndex);
        string error;
        switch (command.Op)
        {
            case HbrEquipmentOp.Equip:
                if (!loadoutUnlocked) return Fail(LoadoutLocked);
                return Done(HbrEquipmentRules.Equip(state, command.ItemId, HbrEquipmentSlot.FromPosition(command.Slot),
                    out error), error, command.ItemId);
            case HbrEquipmentOp.Unequip:
                if (!loadoutUnlocked) return Fail(LoadoutLocked);
                return Done(HbrEquipmentRules.Unequip(state, HbrEquipmentSlot.FromPosition(command.Slot), out error), error,
                    state.At(HbrEquipmentSlot.FromPosition(command.Slot)));
            case HbrEquipmentOp.OpenPlus:
                if (!loadoutUnlocked && state.IsEquipped(command.ItemId)) return Fail(LoadoutLocked);
                return Done(HbrEquipmentRules.OpenPlus(state, command.ItemId, out error), error, command.ItemId);
            case HbrEquipmentOp.ChangePlus:
                if (!loadoutUnlocked && state.IsEquipped(command.ItemId)) return Fail(LoadoutLocked);
                return Done(HbrEquipmentRules.ChangePlus(state, command.ItemId, command.Index, out error), error, command.ItemId);
            case HbrEquipmentOp.Sell:
            {
                int price = HbrEquipmentShop.SellPrice(state.FindItem(command.ItemId));
                HbrEquipmentState sold = HbrEquipmentRules.RemoveItem(state, command.ItemId, out error);
                return sold == null ? Fail(error) : new(CloseOriginalOffer(sold, command.ItemId), 0, price, null);
            }
            case HbrEquipmentOp.Buy:
                return HbrEquipmentPurchases.Buy(state, command, tier, act, gold, policy);
            case HbrEquipmentOp.Reforge:
                return HbrEquipmentPurchases.Reforge(state, command, loadoutUnlocked, gold, policy);
            case HbrEquipmentOp.ChooseReforge:
                return HbrEquipmentPurchases.Choose(state, command, loadoutUnlocked);
            case HbrEquipmentOp.Claim:
            {
                HbrEquipmentState claimed = command.Claim?.ApplyTo(state);
                return claimed == null ? Fail("已经领取过了") : new(claimed, 0, 0, null);
            }
            default:
                return Fail("无效的操作");
        }
    }

    private static HbrEquipmentState CloseOriginalOffer(HbrEquipmentState state, int changedItemId) =>
        state.LastAlchemy is { ReforgeUsed: false, DecisionClosed: false } transaction &&
        transaction.OriginalItemId == changedItemId
            ? state with { LastAlchemy = transaction with { DecisionClosed = true } } : state;

    private static HbrEquipmentOutcome Done(HbrEquipmentState state, string error, int changedItemId = 0) =>
        state == null ? Fail(error) : new(CloseOriginalOffer(state, changedItemId), 0, 0, null);

    private static HbrEquipmentOutcome Fail(string error) => new(null, 0, 0, error ?? "无法完成操作");
}
