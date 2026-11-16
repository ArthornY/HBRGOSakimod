using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Equipment;

internal readonly record struct HbrRolledItem(HbrEquipmentTemplate Template, int[] Rolls, HbrPlusSlot[] Plus);

internal sealed record HbrEquipmentDrop(IReadOnlyList<HbrRolledItem> Items, int PlusRelease, int PlusChange)
{
    internal bool IsEmpty => Items.Count == 0 && PlusRelease <= 0 && PlusChange <= 0;
}

// Drop and alchemy tables. The original game gives fixed rows rather than a
// rarity curve, so these weights are this mod's per-act adaptation.
internal static class HbrEquipmentGenerator
{
    internal const int MaxRarity = 6;

    // Weights for ★1..★6 at each act tier.
    private static readonly int[][] RarityWeights =
    [
        [35, 40, 20, 5, 0, 0],
        [0, 15, 40, 35, 10, 0],
        [0, 0, 15, 35, 35, 15]
    ];
    // Alchemy follows the drops early, then leans to ★6, where the original
    // game's crafted earrings, bracelets and necklaces sit.
    private static readonly int[][] AlchemyRarityWeights =
    [
        [35, 40, 20, 5, 0, 0],
        [0, 15, 40, 35, 10, 0],
        [0, 0, 0, 25, 40, 35]
    ];

    private static readonly HbrEquipmentKind[] DropKinds =
        [HbrEquipmentKind.Ring, HbrEquipmentKind.Earring, HbrEquipmentKind.Bracelet, HbrEquipmentKind.Necklace];
    private static readonly int[] DropKindWeights = [30, 25, 25, 20];
    // Opened "+" slots on a new accessory: 0..3, capped by its slot count.
    private static readonly int[] InitialPlusWeights = [50, 30, 15, 5];
    private static readonly int[] PlusLevelWeights = [55, 30, 15];
    private static readonly int[] HighCharmPercent = [0, 10, 25];

    internal const int NormalDropPercent = 35;
    internal const int ElitePlusReleasePercent = 50;
    internal const int BossArtifactPercent = 50;
    // Boss-only rows on top of the ordinary boss drop.
    internal const int BossGatePercent = 40;
    internal const int BossChainPercent = 30;

    // The original "+" pool: DP, five abilities, luck and critical rate.
    private static readonly HbrEquipmentStat[] PlusStats =
    [
        HbrEquipmentStat.Dp, HbrEquipmentStat.Strength, HbrEquipmentStat.Dexterity, HbrEquipmentStat.Vitality,
        HbrEquipmentStat.Spirit, HbrEquipmentStat.Intelligence, HbrEquipmentStat.Luck, HbrEquipmentStat.CriticalRate
    ];

    internal static int HighCharmChance(int tier) => HighCharmPercent[HbrEquipmentCatalog.ClampTier(tier)];

    internal static IReadOnlyList<int> RarityWeightsFor(int tier, bool alchemy = false) =>
        (alchemy ? AlchemyRarityWeights : RarityWeights)[HbrEquipmentCatalog.ClampTier(tier)];

    internal static int RollRarity(int tier, bool upgraded, HbrEquipmentRandom random, bool alchemy = false)
    {
        int rarity = random.Weighted((alchemy ? AlchemyRarityWeights : RarityWeights)[HbrEquipmentCatalog.ClampTier(tier)]) + 1;
        return upgraded ? Math.Min(MaxRarity, rarity + 1) : rarity;
    }

    // Uses the nearest rarity the kind actually has in that pool. Ties go down
    // for ordinary rolls and up for upgraded elite/boss rolls, so an upgrade
    // never drops.
    internal static HbrEquipmentTemplate PickTemplate(HbrEquipmentKind kind, int rarity, bool preferHigher,
        HbrEquipmentPool pool, int tier, HbrEquipmentRandom random)
    {
        int clamped = HbrEquipmentCatalog.ClampTier(tier);
        List<HbrEquipmentTemplate> candidates = HbrEquipmentCatalog.OfKind(kind)
            .Where(template => template.InPool(pool) && template.InTier(clamped)).ToList();
        int nearest = candidates.Select(template => template.Rarity).Distinct()
            .OrderBy(value => Math.Abs(value - rarity)).ThenBy(value => preferHigher ? -value : value).First();
        List<HbrEquipmentTemplate> rows = candidates.Where(template => template.Rarity == nearest).ToList();
        return rows[random.NextInt(rows.Count)];
    }

    internal static HbrPlusSlot RollPlusSlot(HbrEquipmentRandom random) =>
        new(PlusStats[random.NextInt(PlusStats.Length)], random.Weighted(PlusLevelWeights) + 1);

    internal static HbrPlusSlot[] RollInitialPlus(HbrEquipmentTemplate template, HbrEquipmentRandom random)
    {
        int count = Math.Min(template.PlusSlots, random.Weighted(InitialPlusWeights));
        var slots = new HbrPlusSlot[count];
        for (int i = 0; i < count; i++) slots[i] = RollPlusSlot(random);
        return slots;
    }

    // One row index per random table, by the table's weights.
    internal static int[] RollTables(HbrEquipmentTemplate template, HbrEquipmentRandom random) =>
        template.RollGroups.Select(group => random.Weighted(group.Rows.Select(row => row.Weight).ToArray())).ToArray();

    private static HbrRolledItem RollItem(HbrEquipmentTemplate template, HbrEquipmentRandom random)
    {
        int[] rolls = RollTables(template, random);
        return new(template, rolls, RollInitialPlus(template, random));
    }

    internal static HbrRolledItem RollAccessory(HbrEquipmentKind? kind, int tier, bool upgraded,
        HbrEquipmentRandom random, HbrEquipmentPool pool = HbrEquipmentPool.Drop)
    {
        HbrEquipmentKind chosen = kind ?? DropKinds[random.Weighted(DropKindWeights)];
        int rarity = RollRarity(tier, upgraded, random, pool == HbrEquipmentPool.Alchemy);
        return RollItem(PickTemplate(chosen, rarity, upgraded, pool, tier, random), random);
    }

    // Gate bracelets of the current act, or ancient chains from the second act.
    internal static HbrRolledItem? RollBossAccessory(HbrEquipmentKind kind, int tier, HbrEquipmentRandom random)
    {
        int clamped = HbrEquipmentCatalog.ClampTier(tier);
        List<HbrEquipmentTemplate> rows = HbrEquipmentCatalog.OfKind(kind)
            .Where(template => template.InPool(HbrEquipmentPool.Boss) && template.InTier(clamped)).ToList();
        return rows.Count == 0 ? null : RollItem(rows[random.NextInt(rows.Count)], random);
    }

    internal static HbrRolledItem RollCharm(int tier, HbrEquipmentRandom random)
    {
        bool high = random.NextInt(100) < HighCharmPercent[HbrEquipmentCatalog.ClampTier(tier)];
        List<HbrEquipmentTemplate> pool = HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Charm)
            .Where(template => (template.RollGroups.Count == 0) == high && template.InTier(tier)).ToList();
        if (pool.Count == 0)
            pool = HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Charm).Where(template => template.RollGroups.Count > 0).ToList();
        HbrEquipmentTemplate charm = pool[random.NextInt(pool.Count)];
        return new(charm, RollTables(charm, random), Array.Empty<HbrPlusSlot>());
    }

    // Alchemy alone receives Satsuki's policy; combat drops never enter this
    // path. Its RNG remains the existing saved, per-owner equipment stream.
    internal static HbrRolledItem RollAlchemy(HbrEquipmentKind kind, int tier,
        HbrSatsukiShopPolicy policy, bool forceHighest, HbrEquipmentRandom random)
    {
        if (kind == HbrEquipmentKind.Charm)
        {
            bool high = forceHighest || random.NextInt(100) < policy.HighCharmChance(HighCharmChance(tier));
            var pool = HbrEquipmentCatalog.OfKind(kind).Where(template =>
                (template.RollGroups.Count == 0) == high &&
                (template.InTier(tier) || high && policy.Enabled)).ToList();
            if (pool.Count == 0) pool = HbrEquipmentCatalog.OfKind(kind).Where(template => template.RollGroups.Count > 0).ToList();
            HbrEquipmentTemplate charm = pool[random.NextInt(pool.Count)];
            return new(charm, RollTables(charm, random), Array.Empty<HbrPlusSlot>());
        }
        int rarity = forceHighest ? HbrSatsukiShopPolicy.HighestAlchemyRarity(tier) : RollRarity(tier, false, random, true);
        bool promote = policy.Enabled && policy.HasStarPromotion(random.NextInt(100));
        rarity = policy.PromoteRarity(rarity, promote);
        HbrEquipmentTemplate chosen;
        var exactDrop = policy.Enabled &&
            !HbrEquipmentCatalog.OfKind(kind).Any(template => template.Rarity == rarity &&
                template.InPool(HbrEquipmentPool.Alchemy) && template.InTier(tier))
            ? HbrEquipmentCatalog.OfKind(kind).Where(template => template.Rarity == rarity &&
                template.InPool(HbrEquipmentPool.Drop) && template.InTier(tier)).ToArray()
            : Array.Empty<HbrEquipmentTemplate>();
        // Fill an alchemy rank gap only with an existing exact drop row
        // (for example five-star necklaces or six-star rings), never an invented rank.
        chosen = exactDrop.Length > 0 ? exactDrop[random.NextInt(exactDrop.Length)]
            : PickTemplate(kind, rarity, promote, HbrEquipmentPool.Alchemy, tier, random);
        HbrRolledItem item = RollItem(chosen, random);
        int count = policy.OpenedPlusCount(item.Plus.Length, chosen.PlusSlots);
        return count == item.Plus.Length ? item : item with
        { Plus = item.Plus.Concat(Enumerable.Range(0, count - item.Plus.Length).Select(_ => RollPlusSlot(random))).ToArray() };
    }

    internal static HbrRolledItem RollArtifactChip(HbrEquipmentRandom random)
    {
        List<HbrEquipmentTemplate> pool = HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Chip)
            .Where(template => !template.IsSold).ToList();
        return new(pool[random.NextInt(pool.Count)], Array.Empty<int>(), Array.Empty<HbrPlusSlot>());
    }

    // Gold Hoppers keep their original 青绿琥珀之露 drop.
    internal static HbrEquipmentDrop RollCombatDrop(HbrDropRoom room, int tier, bool goldHopper,
        HbrEquipmentRandom random)
    {
        var items = new List<HbrRolledItem>();
        int release = goldHopper ? 1 : 0;
        int change = 0;
        switch (room)
        {
            case HbrDropRoom.Normal:
                if (random.NextInt(100) < NormalDropPercent) items.Add(RollAccessory(null, tier, false, random));
                break;
            case HbrDropRoom.Elite:
                items.Add(RollAccessory(null, tier, true, random));
                if (random.NextInt(100) < ElitePlusReleasePercent) release++;
                break;
            case HbrDropRoom.Boss:
                items.Add(RollAccessory(null, tier, true, random));
                release++;
                change++;
                if (HbrEquipmentCatalog.ClampTier(tier) == HbrEquipmentCatalog.TierCount - 1 &&
                    random.NextInt(100) < BossArtifactPercent)
                    items.Add(RollArtifactChip(random));
                if (random.NextInt(100) < BossGatePercent &&
                    RollBossAccessory(HbrEquipmentKind.Bracelet, tier, random) is { } gate)
                    items.Add(gate);
                if (random.NextInt(100) < BossChainPercent &&
                    RollBossAccessory(HbrEquipmentKind.Necklace, tier, random) is { } chain)
                    items.Add(chain);
                break;
        }
        return new(items, release, change);
    }
}
