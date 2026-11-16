using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HBRGOSakiMod.Core.FateDice;

internal sealed record HbrFateShopOffer(string Fragment, int Price, bool Sold);

// The saved, immutable per-player fate dice record: kept swordheart, slots,
// cracks and fragments. Every change returns a new instance with a higher revision.
internal sealed record HbrFateDiceState
{
    internal const int CurrentVersion = 1;
    internal const int BaseSlots = 2;
    internal const int MaxSlots = 5;
    internal const int CrackRawPenalty = 3;
    internal const int KeptSwordheartMaximum = 7;
    private const int MaxReceipts = 64;

    // 0 means the run predates the fate dice; see Effective.
    public int Version { get; init; }
    public int Revision { get; init; }
    // Swordheart kept between combats; combat-only maximum raises are cut at the end of a combat.
    public int Swordheart { get; init; }
    public int BossesDefeated { get; init; }
    public int Cracks { get; init; }
    public string[] Equipped { get; init; } = Array.Empty<string>();
    public string[] Bag { get; init; } = Array.Empty<string>();
    // Reward sets already rolled (so a hook running twice does not roll again), reward
    // sets claimed, and merchant purchases ("shop:<room>:<fragment>").
    public string[] Receipts { get; init; } = Array.Empty<string>();
    public string[] Claimed { get; init; } = Array.Empty<string>();

    internal static readonly HbrFateDiceState Empty = new();

    internal int Slots => Math.Clamp(BaseSlots + Math.Max(0, BossesDefeated), BaseSlots, MaxSlots);

    // Cracks take empty slots first; the rest of a full set of cracks lowers raw rolls.
    internal int CrackedSlots => Math.Min(Math.Max(0, Cracks), Slots);

    internal int Capacity => Slots - CrackedSlots;

    internal int OverflowCracks => Math.Max(0, Cracks - Slots);

    internal int RawPenalty => CrackRawPenalty * OverflowCracks;

    internal bool Owns(string id) => Equipped.Contains(id) || Bag.Contains(id);

    internal bool IsEquipped(string id) => id != null && Equipped.Contains(id);

    internal bool HasReceipt(string receipt) => receipt != null && Receipts.Contains(receipt);

    internal bool HasClaimed(string receipt) => receipt != null && Claimed.Contains(receipt);

    // An old run has no record: it starts with two empty slots plus the bosses
    // already beaten, which every peer reads from the shared act index.
    internal static HbrFateDiceState Effective(HbrFateDiceState state, int bossesBeforeThisAct) =>
        state.Version > 0 ? state : state with { Version = CurrentVersion, BossesDefeated = Math.Max(0, bossesBeforeThisAct) };

    internal static HbrFateDiceState Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return Empty;
        HbrFateDiceState state = null;
        try { state = JsonSerializer.Deserialize<HbrFateDiceState>(data); }
        catch (JsonException) { }
        catch (NotSupportedException) { }
        return Normalize(state);
    }

    internal string Serialize() => JsonSerializer.Serialize(this);

    // Drops unknown or duplicate fragments and keeps the equipped set within capacity.
    internal static HbrFateDiceState Normalize(HbrFateDiceState state)
    {
        if (state == null) return Empty;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string[] equipped = (state.Equipped ?? Array.Empty<string>())
            .Where(id => HbrFateDiceCatalog.Exists(id) && seen.Add(id)).ToArray();
        string[] bag = (state.Bag ?? Array.Empty<string>())
            .Where(id => HbrFateDiceCatalog.Exists(id) && seen.Add(id)).ToArray();
        HbrFateDiceState normalized = state with
        {
            Swordheart = Math.Max(0, state.Swordheart),
            BossesDefeated = Math.Max(0, state.BossesDefeated),
            Cracks = Math.Max(0, state.Cracks),
            Equipped = equipped,
            Bag = bag,
            Receipts = state.Receipts ?? Array.Empty<string>(),
            Claimed = state.Claimed ?? Array.Empty<string>()
        };
        return normalized.FitCapacity();
    }

    private HbrFateDiceState Next() => this with { Revision = Revision + 1 };

    // Fragments beyond the free slots go back to the bag, last equipped first.
    private HbrFateDiceState FitCapacity()
    {
        if (Equipped.Length <= Capacity) return this;
        string[] kept = Equipped.Take(Capacity).ToArray();
        string[] returned = Equipped.Skip(Capacity).ToArray();
        return this with { Equipped = kept, Bag = Bag.Concat(returned).ToArray() };
    }

    // A new fragment fills a free slot, otherwise it waits in the bag.
    internal HbrFateDiceState WithFragment(string id)
    {
        if (!HbrFateDiceCatalog.Exists(id) || Owns(id)) return this;
        return (Equipped.Length < Capacity
            ? this with { Equipped = Equipped.Append(id).ToArray() }
            : this with { Bag = Bag.Append(id).ToArray() }).Next();
    }

    internal HbrFateDiceState Equip(string id) =>
        Bag.Contains(id) && Equipped.Length < Capacity
            ? (this with { Equipped = Equipped.Append(id).ToArray(), Bag = Bag.Where(other => other != id).ToArray() }).Next()
            : this;

    internal HbrFateDiceState Unequip(string id) =>
        Equipped.Contains(id)
            ? (this with { Equipped = Equipped.Where(other => other != id).ToArray(), Bag = Bag.Append(id).ToArray() }).Next()
            : this;

    // Every time the magic sword opens: one more crack.
    internal HbrFateDiceState WithCrack() => (this with { Cracks = Cracks + 1 }).FitCapacity().Next();

    internal HbrFateDiceState Repaired() => Cracks <= 0 ? this : (this with { Cracks = Cracks - 1 }).Next();

    internal HbrFateDiceState WithBossVictory() => (this with { BossesDefeated = BossesDefeated + 1 }).Next();

    internal HbrFateDiceState WithSwordheart(int amount, int keptMaximum) =>
        Math.Clamp(amount, 0, Math.Max(0, keptMaximum)) is var kept && kept == Swordheart
            ? this
            : (this with { Swordheart = kept }).Next();

    internal HbrFateDiceState WithReceipt(string receipt) =>
        string.IsNullOrEmpty(receipt) || HasReceipt(receipt) ? this
            : (this with { Receipts = Receipts.Append(receipt).TakeLast(MaxReceipts).ToArray() }).Next();

    internal HbrFateDiceState Claim(string receipt, string fragment)
    {
        if (!HasReceipt(receipt) || HasClaimed(receipt) || !HbrFateDiceCatalog.Exists(fragment)) return this;
        return (WithFragment(fragment) with { Claimed = Claimed.Append(receipt).TakeLast(MaxReceipts).ToArray() }).Next();
    }

    internal static string ShopReceipt(string shopKey, string fragment) => $"shop:{shopKey}:{fragment}";
}

internal enum HbrFateDiceOp
{
    Equip,
    Unequip,
    Claim,
    Buy
}

// A player's request from the manual or the reward screen, applied on every peer.
internal sealed record HbrFateDiceCommand
{
    public HbrFateDiceOp Op { get; init; }
    public string Fragment { get; init; }
    public string Receipt { get; init; }
    public string ShopKey { get; init; }
}

internal readonly record struct HbrFateDiceOutcome(bool Succeeded, HbrFateDiceState State, int GoldCost)
{
    internal static HbrFateDiceOutcome Fail(HbrFateDiceState state) => new(false, state, 0);
}

internal static class HbrFateDiceCommands
{
    // The parasol takes the merchant's stocked fragments without paying. Keep
    // the normal shop receipt so reopening either shop view cannot restock it.
    internal static HbrFateDiceState StealShop(HbrFateDiceState state, string shopKey,
        IReadOnlyList<HbrFateShopOffer> shopStock)
    {
        if (state == null || string.IsNullOrEmpty(shopKey) || shopStock == null) return state;
        foreach (HbrFateShopOffer offer in shopStock)
        {
            if (offer.Sold || state.Owns(offer.Fragment) ||
                state.HasReceipt(HbrFateDiceState.ShopReceipt(shopKey, offer.Fragment))) continue;
            HbrFateDiceState taken = state.WithFragment(offer.Fragment);
            if (!ReferenceEquals(taken, state))
                state = taken.WithReceipt(HbrFateDiceState.ShopReceipt(shopKey, offer.Fragment));
        }
        return state;
    }

    // Loadout changes only outside combat (「战斗外在手册的命运骰子页随时装卸」); claims
    // come from the reward screen and purchases from the merchant the player stands in.
    // shopStock is what the merchant the player stands in sells (null elsewhere).
    internal static HbrFateDiceOutcome Evaluate(HbrFateDiceState state, HbrFateDiceCommand command,
        bool loadoutUnlocked, int gold, string currentShopKey, IReadOnlyList<HbrFateShopOffer> shopStock)
    {
        if (state == null || command == null) return HbrFateDiceOutcome.Fail(state);
        switch (command.Op)
        {
            case HbrFateDiceOp.Equip:
                if (!loadoutUnlocked) return HbrFateDiceOutcome.Fail(state);
                HbrFateDiceState equipped = state.Equip(command.Fragment);
                return new HbrFateDiceOutcome(!ReferenceEquals(equipped, state), equipped, 0);
            case HbrFateDiceOp.Unequip:
                if (!loadoutUnlocked) return HbrFateDiceOutcome.Fail(state);
                HbrFateDiceState unequipped = state.Unequip(command.Fragment);
                return new HbrFateDiceOutcome(!ReferenceEquals(unequipped, state), unequipped, 0);
            case HbrFateDiceOp.Claim:
                HbrFateDiceState claimed = state.Claim(command.Receipt, command.Fragment);
                return new HbrFateDiceOutcome(!ReferenceEquals(claimed, state), claimed, 0);
            case HbrFateDiceOp.Buy:
                if (command.ShopKey == null || command.ShopKey != currentShopKey) return HbrFateDiceOutcome.Fail(state);
                HbrFateShopOffer offer = shopStock?.FirstOrDefault(entry => entry.Fragment == command.Fragment);
                if (offer == null || offer.Sold || state.Owns(offer.Fragment) || gold < offer.Price)
                    return HbrFateDiceOutcome.Fail(state);
                HbrFateDiceState bought = state.WithFragment(offer.Fragment)
                    .WithReceipt(HbrFateDiceState.ShopReceipt(command.ShopKey, offer.Fragment));
                return new HbrFateDiceOutcome(true, bought, offer.Price);
            default:
                return HbrFateDiceOutcome.Fail(state);
        }
    }
}

// A splitmix stream keyed by the room, like the equipment drops: rolling rewards
// never shifts another stream, and every peer derives the same picks.
internal sealed class HbrFateDiceRandom
{
    private readonly ulong _seed;
    private int _counter;

    private HbrFateDiceRandom(ulong seed) => _seed = seed;

    internal static HbrFateDiceRandom ForKey(ulong seed, string key)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char c in key ?? string.Empty)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return new HbrFateDiceRandom(seed ^ hash ^ 0x46415445UL);
    }

    internal int NextInt(int maxExclusive)
    {
        ulong z = _seed + 0x9E3779B97F4A7C15UL * (ulong)(++_counter);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return maxExclusive <= 1 ? 0 : (int)(z % (ulong)maxExclusive);
    }
}

internal enum HbrFateOfferKind
{
    Elite,
    Boss
}

internal static class HbrFateDiceOffers
{
    internal const int RewardChoices = 3;

    // Elites offer common and uncommon fragments, bosses rare ones (「首领：从稀有碎片里
    // 三选一」), each topped up from the other rarities when the player owns most of them.
    // Owned fragments never show up.
    internal static string[] Roll(HbrFateDiceState state, HbrFateOfferKind kind, HbrFateDiceRandom random)
    {
        HbrFateFragment[] unowned = HbrFateDiceCatalog.All.Where(fragment => !state.Owns(fragment.Id)).ToArray();
        IEnumerable<HbrFateFragment>[] tiers = kind switch
        {
            HbrFateOfferKind.Elite => new[]
            {
                unowned.Where(fragment => fragment.Rarity != HbrFateFragmentRarity.Rare),
                unowned.Where(fragment => fragment.Rarity == HbrFateFragmentRarity.Rare)
            },
            HbrFateOfferKind.Boss => new[]
            {
                unowned.Where(fragment => fragment.Rarity == HbrFateFragmentRarity.Rare),
                unowned.Where(fragment => fragment.Rarity == HbrFateFragmentRarity.Uncommon),
                unowned.Where(fragment => fragment.Rarity == HbrFateFragmentRarity.Common)
            },
            _ => Array.Empty<IEnumerable<HbrFateFragment>>()
        };
        int count = RewardChoices;
        var picks = new List<string>();
        foreach (IEnumerable<HbrFateFragment> tier in tiers)
        {
            List<HbrFateFragment> pool = tier.ToList();
            while (picks.Count < count && pool.Count > 0)
            {
                int index = random.NextInt(pool.Count);
                picks.Add(pool[index].Id);
                pool.RemoveAt(index);
            }
        }
        return picks.ToArray();
    }

    // A merchant's fragments follow from the room alone: fragments bought here stay
    // listed as sold, fragments owned before the visit are skipped, so the stock never
    // changes while the player shops and needs no state of its own.
    internal static HbrFateShopOffer[] Shop(HbrFateDiceState state, string shopKey, HbrFateDiceRandom random)
    {
        if (state == null || string.IsNullOrEmpty(shopKey)) return Array.Empty<HbrFateShopOffer>();
        bool BoughtHere(string id) => state.HasReceipt(HbrFateDiceState.ShopReceipt(shopKey, id));
        int count = 1 + random.NextInt(2);
        List<HbrFateFragment> pool = HbrFateDiceCatalog.All.ToList();
        var offers = new List<HbrFateShopOffer>();
        while (offers.Count < count && pool.Count > 0)
        {
            int index = random.NextInt(pool.Count);
            HbrFateFragment fragment = pool[index];
            pool.RemoveAt(index);
            bool sold = BoughtHere(fragment.Id);
            if (!sold && state.Owns(fragment.Id)) continue;
            offers.Add(new HbrFateShopOffer(fragment.Id, HbrFateDiceCatalog.Price(fragment.Rarity), sold));
        }
        return offers.ToArray();
    }
}
