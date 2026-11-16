using System;

namespace HBRGOSakiMod.Core.Equipment;

// Immutable inputs to the shop transaction. Keep prices and random decisions
// pure so previews, the owner and every multiplayer peer use the same rules.
internal readonly partial record struct HbrSatsukiShopPolicy(
    bool Enabled, bool Discount10, bool ExtraPlus, bool FirstAlchemyHighest,
    bool Alchemy20, bool Refund, bool FirstBuyHalf, bool Reforge, int MemberCount)
{
    // Cheap UI invalidation value; no RNG, mutable transaction or card scans.
    internal int CacheKey => (Enabled ? 1 : 0) | (Discount10 ? 2 : 0) | (ExtraPlus ? 4 : 0) |
        (FirstAlchemyHighest ? 8 : 0) | (Alchemy20 ? 16 : 0) | (Refund ? 32 : 0) |
        (FirstBuyHalf ? 64 : 0) | (Reforge ? 128 : 0) | (Math.Clamp(MemberCount, 0, 6) << 8);

    internal int Price(int raw, bool craft, bool firstBuyAvailable)
    {
        if (!Enabled) return raw;
        decimal price = Math.Max(0, raw);
        if (Discount10) price *= 0.9m;
        if (craft && Alchemy20 && MemberCount >= 3) price *= 0.8m;
        if (FirstBuyHalf && firstBuyAvailable) price *= 0.5m;
        return Math.Max(1, (int)decimal.Floor(price));
    }

    // The caller owns the shop-only RNG stream. This helper never rolls dice
    // or reads battle modifiers, and the upper endpoint is exclusive.
    internal bool HasStarPromotion(int roll0To99) => Enabled && roll0To99 is >= 0 and < 30;
    internal int PromoteRarity(int rarity, bool promote) =>
        Math.Clamp(rarity + (Enabled && promote ? 1 : 0), 1, 6);
    internal int HighCharmChance(int raw) => Math.Clamp(raw + (Enabled ? 30 : 0), 0, 100);
    internal static int HighestAlchemyRarity(int tier) => tier <= 0 ? 4 : tier == 1 ? 5 : 6;
    internal int OpenedPlusCount(int current, int maximum) =>
        Math.Min(Math.Max(0, maximum), Math.Max(0, current) + (Enabled && ExtraPlus ? 1 : 0));
    internal int RefundGold(int paid, int rawD100) =>
        Enabled && Refund && rawD100 is > 50 and <= 100 ? Math.Max(0, paid) / 2 : 0;
    internal static int ReforgePrice(int paid) => Math.Max(1, Math.Max(0, paid) / 2);
}
