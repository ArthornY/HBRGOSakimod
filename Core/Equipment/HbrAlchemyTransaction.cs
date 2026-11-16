using System;
using System.Text.Json.Serialization;

namespace HBRGOSakiMod.Core.Equipment;

// One original purchase owns the optional reforge and refund. Neither choosing
// a result nor rolling its second candidate creates a second purchase.
internal sealed record HbrAlchemyTransaction
{
    public string Id { get; init; } = string.Empty;
    public int Act { get; init; }
    public int Tier { get; init; }
    public string Offer { get; init; } = string.Empty;
    public int OriginalItemId { get; init; }
    public int PaidPrice { get; init; }
    public int Refund { get; init; }
    public bool StarPromotionEnabled { get; init; }
    public bool ExtraPlus { get; init; }
    public bool ForceHighest { get; init; }
    public bool ReforgeUsed { get; init; }
    public bool DecisionClosed { get; init; }
    public HbrEquipmentClaimItem Second { get; init; }
    [JsonIgnore] public bool ChoicePending => Second != null && !DecisionClosed;
}

internal readonly record struct HbrShopQuote(int OriginalPrice, int Price)
{
    internal bool HasDiscount => Price < OriginalPrice;
}
