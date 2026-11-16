using HBRGOSakiMod.Core.Equipment;

// Expected-value calibration of the equipment scales. Many runs of a model
// player go through the production drop, shop, alchemy and "+" code; the mean
// damage multiplier against each act's fights is what the scales are tuned on.
//
// Model (docs/hbr-equipment.md):
// - Route: per act 6 normal + 2 elite fights and the boss on 17 floors.
// - Growth: battle levels 1/2/3, three automatic sense levels per fight
//   spread evenly, every allocatable point into 疯狂, and the guaranteed
//   joining ceremony taken as 疯狂 +8. No other events.
// - Enemy: 150 + 60 per act + 6 per floor, elite +30, boss +50.
// - Gold: native combat gold (normal 10..20, elite 35..45, boss 100) scaled so
//   that 99 + all income is about 2000 after act 3. A share goes into
//   equipment; sale proceeds come back in full.
// - Buying: save for the act's best booster first (take the best affordable
//   one if none is worn), then chips, ring alchemy when the expected crafted
//   ring beats the worn one, charms for open charm slots, accessories for empty
//   slots and chip upgrades. "+" materials open the ring first and reroll its
//   slots that are not 力量/灵巧. Everything not worn is sold.
internal static class HbrEquipmentCalibration
{
    internal sealed record ActResult(double First, double Normal, double LastNormal, double Elite, double Boss, double BossGear);

    private static readonly (int Floor, HbrDropRoom Room)[] Route =
    [
        (2, HbrDropRoom.Normal), (3, HbrDropRoom.Normal), (5, HbrDropRoom.Normal), (7, HbrDropRoom.Elite),
        (8, HbrDropRoom.Normal), (10, HbrDropRoom.Normal), (12, HbrDropRoom.Elite), (13, HbrDropRoom.Normal),
        (17, HbrDropRoom.Boss)
    ];
    internal const decimal GoldScale = (2000m - 99m) / 810m;
    private const int CeremonyFloor = 4;
    private const decimal CeremonyStrength = 16m;
    private const int Acts = 3;

    private static readonly HbrEquipmentSlot[] OtherAccessories =
        [HbrEquipmentSlot.Earring, HbrEquipmentSlot.Bracelet, HbrEquipmentSlot.Necklace];

    private static HbrEquipmentTemplate T(HbrEquipmentItem item) => HbrEquipmentCatalog.Find(item.Template);

    internal static decimal Hp(HbrEquipmentItem item)
    {
        HbrEquipmentBonus bonus = HbrEquipmentRules.ItemBonus(item);
        return (2m * bonus.Strength + bonus.Dexterity) / 3m;
    }

    private static decimal Hp(HbrEquipmentTemplate template) => Hp(new HbrEquipmentItem { Template = template.Id });

    private static decimal ChipValue(int tier) =>
        0.5m * HbrEquipmentCatalog.All.Where(t => t.Kind == HbrEquipmentKind.Chip && t.IsSold && t.InTier(tier)).Max(Hp);

    private static decimal BoosterValue(HbrEquipmentTemplate template, int tier) => Hp(template) + template.ChipSlots * ChipValue(tier);

    private static int Level(HbrEquipmentItem item) => 2 * T(item).Rarity + HbrEquipmentRules.DisplayPlus(item);

    private static HbrEquipmentState Put(HbrEquipmentState state, int id, HbrEquipmentSlot slot) =>
        HbrEquipmentRules.Equip(state, id, slot, out _) ?? state;

    private static HbrEquipmentState Clear(HbrEquipmentState state, HbrEquipmentSlot slot) =>
        state.At(slot) == 0 ? state : HbrEquipmentRules.Unequip(state, slot, out _) ?? state;

    private static HbrEquipmentKind KindOf(HbrEquipmentSlot slot) => slot.Kind;

    private static HbrEquipmentState EquipBest(HbrEquipmentState state, int tier)
    {
        HbrEquipmentItem[] items = state.Items;
        if (items.Where(i => T(i).Kind == HbrEquipmentKind.Booster).OrderByDescending(i => BoosterValue(T(i), tier)).FirstOrDefault() is { } booster)
            state = Put(state, booster.Id, HbrEquipmentSlot.Booster);
        if (items.Where(i => T(i).Kind == HbrEquipmentKind.Ring).OrderByDescending(Hp).FirstOrDefault() is { } ring)
            state = Put(state, ring.Id, HbrEquipmentSlot.Ring);
        foreach (HbrEquipmentSlot slot in OtherAccessories)
            if (items.Where(i => T(i).Kind == KindOf(slot)).OrderByDescending(Level).ThenByDescending(Hp).FirstOrDefault() is { } best)
                state = Put(state, best.Id, slot);
        for (int i = 0; i < HbrEquipmentCatalog.MaxChipSlots; i++) state = Clear(state, HbrEquipmentSlot.Chip(i));
        int index = 0, chipSlots = HbrEquipmentRules.ChipSlotCount(state);
        foreach (HbrEquipmentItem chip in items.Where(i => T(i).Kind == HbrEquipmentKind.Chip).OrderByDescending(Hp))
            if (index < chipSlots) state = Put(state, chip.Id, HbrEquipmentSlot.Chip(index++));
        for (int i = 0; i < HbrEquipmentCatalog.CharmSlots; i++) state = Clear(state, HbrEquipmentSlot.Charm(i));
        index = 0;
        int charmSlots = HbrEquipmentRules.OpenCharmSlots(state);
        foreach (HbrEquipmentItem charm in items.Where(i => T(i).Kind == HbrEquipmentKind.Charm && Hp(i) > 0m).OrderByDescending(Hp))
            if (index < charmSlots) state = Put(state, charm.Id, HbrEquipmentSlot.Charm(index++));
        return state;
    }

    private static HbrEquipmentState Buy(HbrEquipmentState state, HbrShopOffer offer, int tier, ref decimal wallet)
    {
        if (offer == null || wallet < offer.Price) return state;
        HbrEquipmentState next = HbrEquipmentShop.Deliver(state, offer, tier, out _, out _);
        if (next == null) return state;
        wallet -= offer.Price;
        return next;
    }

    private static HbrEquipmentState Shop(HbrEquipmentState state, int tier, decimal[] craftedRingHp, ref decimal wallet)
    {
        state = EquipBest(state, tier);
        IReadOnlyList<HbrShopOffer> offers = HbrEquipmentShop.ShopOffers(tier);
        IReadOnlyList<HbrShopOffer> alchemy = HbrEquipmentShop.AlchemyOffers(tier);
        List<HbrShopOffer> boosters = offers.Where(o => o.Kind == HbrShopOfferKind.Item && o.Template.Kind == HbrEquipmentKind.Booster).ToList();
        List<HbrShopOffer> chips = offers.Where(o => o.Kind == HbrShopOfferKind.Item && o.Template.Kind == HbrEquipmentKind.Chip).ToList();

        HbrEquipmentItem current = state.FindItem(state.At(HbrEquipmentSlot.Booster));
        decimal currentValue = current == null ? 0m : BoosterValue(T(current), tier);
        HbrShopOffer target = boosters.OrderByDescending(o => BoosterValue(o.Template, tier)).First();
        bool saving = BoosterValue(target.Template, tier) > currentValue + 0.5m;
        if (saving && wallet >= target.Price)
        {
            state = EquipBest(Buy(state, target, tier, ref wallet), tier);
            saving = false;
        }
        else if (current == null)
        {
            decimal have = wallet;
            if (boosters.Where(o => o.Price <= have).OrderByDescending(o => BoosterValue(o.Template, tier)).FirstOrDefault() is { } first)
                state = EquipBest(Buy(state, first, tier, ref wallet), tier);
        }
        decimal reserve = saving ? target.Price : 0m;
        bool CanSpend(decimal cost, decimal have) => have - cost >= reserve;

        while (state.Items.Count(i => T(i).Kind == HbrEquipmentKind.Chip) < HbrEquipmentRules.ChipSlotCount(state))
        {
            decimal have = wallet;
            HbrShopOffer chip = chips.Where(o => CanSpend(o.Price, have) && Hp(o.Template) > 0m)
                .OrderByDescending(o => Hp(o.Template)).ThenBy(o => o.Price).FirstOrDefault();
            if (chip == null) break;
            state = Buy(state, chip, tier, ref wallet);
        }
        state = EquipBest(state, tier);

        HbrShopOffer craftRing = alchemy.First(o => o.CraftKind == HbrEquipmentKind.Ring);
        HbrEquipmentItem ring = state.FindItem(state.At(HbrEquipmentSlot.Ring));
        if (CanSpend(craftRing.Price, wallet) && craftedRingHp[tier] > (ring == null ? 0m : Hp(ring)) + 2m)
            state = EquipBest(Buy(state, craftRing, tier, ref wallet), tier);

        HbrShopOffer craftCharm = alchemy.First(o => o.CraftKind == HbrEquipmentKind.Charm);
        for (int guard = 0; guard < 12; guard++)
        {
            int useful = state.Items.Count(i => T(i).Kind == HbrEquipmentKind.Charm && Hp(i) > 0m);
            if (useful >= HbrEquipmentRules.OpenCharmSlots(state) || !CanSpend(craftCharm.Price, wallet)) break;
            state = Buy(state, craftCharm, tier, ref wallet);
        }
        state = EquipBest(state, tier);

        foreach (HbrEquipmentSlot slot in OtherAccessories)
        {
            HbrShopOffer craft = alchemy.First(o => o.CraftKind == KindOf(slot));
            if (state.At(slot) == 0 && CanSpend(craft.Price + 20m, wallet)) state = EquipBest(Buy(state, craft, tier, ref wallet), tier);
        }

        for (int guard = 0; guard < 4; guard++)
        {
            List<HbrEquipmentItem> worn = Enumerable.Range(0, HbrEquipmentCatalog.MaxChipSlots)
                .Select(i => state.FindItem(state.At(HbrEquipmentSlot.Chip(i)))).Where(i => i != null).ToList();
            if (worn.Count == 0 || worn.Count < HbrEquipmentRules.ChipSlotCount(state)) break;
            decimal worst = worn.Min(Hp), have = wallet;
            HbrShopOffer better = chips.Where(o => CanSpend(o.Price, have) && Hp(o.Template) > worst + 0.5m)
                .OrderByDescending(o => Hp(o.Template)).FirstOrDefault();
            if (better == null) break;
            state = EquipBest(Buy(state, better, tier, ref wallet), tier);
        }

        foreach (HbrEquipmentSlot slot in HbrEquipmentSlot.Accessories)
            while (state.PlusRelease > 0 && state.FindItem(state.At(slot)) is { } item && HbrEquipmentRules.CanOpenPlus(state, item))
                state = HbrEquipmentRules.OpenPlus(state, item.Id, out _) ?? state;
        if (state.FindItem(state.At(HbrEquipmentSlot.Ring)) is { } rerolled)
            for (int index = 0; index < rerolled.Plus.Length && state.PlusChange > 0; index++)
                if (state.FindItem(rerolled.Id).Plus[index].Stat is not (HbrEquipmentStat.Strength or HbrEquipmentStat.Dexterity))
                    state = HbrEquipmentRules.ChangePlus(state, rerolled.Id, index, out _) ?? state;
        state = EquipBest(state, tier);

        foreach (HbrEquipmentItem item in state.Items.Where(i => !state.IsEquipped(i.Id)).ToList())
        {
            decimal price = HbrEquipmentShop.SellPrice(item);
            if (HbrEquipmentRules.RemoveItem(state, item.Id, out _) is { } sold) { state = sold; wallet += price; }
        }
        return state;
    }

    // HP-attack reference stat without gear before a fight.
    private static decimal Basis(int act, int floor, int levels, int fights, int points)
    {
        decimal stat = 100m + 4m * (levels - 1);
        decimal auto = 2.5m + 0.5m * fights;
        decimal strength = stat + 2m * (auto + points) + (act > 0 || floor > CeremonyFloor ? CeremonyStrength : 0m);
        decimal dexterity = stat + 2m * auto;
        return (2m * strength + dexterity) / 3m;
    }

    private static decimal Enemy(int act, int floor, HbrDropRoom room) =>
        150m + 60m * act + 6m * (16 * act + floor - 1) + (room == HbrDropRoom.Boss ? 50m : room == HbrDropRoom.Elite ? 30m : 0m);

    internal static ActResult[] Run(int runs, decimal share)
    {
        var craftedRingHp = new decimal[HbrEquipmentCatalog.TierCount];
        for (int tier = 0; tier < craftedRingHp.Length; tier++)
        {
            decimal sum = 0m;
            for (int i = 0; i < 2000; i++)
            {
                HbrRolledItem rolled = HbrEquipmentGenerator.RollAccessory(HbrEquipmentKind.Ring, tier, false,
                    HbrEquipmentRandom.ForKey(777, $"ring-{tier}-{i}"), HbrEquipmentPool.Alchemy);
                sum += Hp(new HbrEquipmentItem { Template = rolled.Template.Id, Rolls = rolled.Rolls, Plus = rolled.Plus });
            }
            craftedRingHp[tier] = sum / 2000m;
        }

        var multiplier = new double[Acts, Route.Length];
        var bossGear = new double[Acts];
        for (int run = 0; run < runs; run++)
        {
            ulong seed = 0x9E3779B97F4A7C15UL * (ulong)(run + 1);
            HbrEquipmentState state = HbrEquipmentState.Empty with { Seed = seed };
            decimal wallet = 99m * share;
            int levels = 1, fights = 0, points = 0;
            for (int act = 0; act < Acts; act++)
            {
                int tier = HbrEquipmentRules.TierForAct(act);
                state = Shop(state, tier, craftedRingHp, ref wallet);
                for (int fight = 0; fight < Route.Length; fight++)
                {
                    (int floor, HbrDropRoom room) = Route[fight];
                    decimal gear = state.Equipped.Where(id => id > 0).Sum(id => Hp(state.FindItem(id)));
                    decimal value = 1m + (Basis(act, floor, levels, fights, points) + gear - Enemy(act, floor, room)) / 140m;
                    multiplier[act, fight] += (double)Math.Max(0m, value);
                    if (room == HbrDropRoom.Boss) bossGear[act] += (double)gear;

                    HbrEquipmentRandom goldRandom = HbrEquipmentRandom.ForKey(seed, $"gold:{act}:{floor}");
                    decimal gold = room switch
                    {
                        HbrDropRoom.Normal => 10 + goldRandom.NextInt(11),
                        HbrDropRoom.Elite => 35 + goldRandom.NextInt(11),
                        _ => 100
                    };
                    wallet += gold * GoldScale * share;
                    HbrEquipmentDrop drop = HbrEquipmentGenerator.RollCombatDrop(room, tier, false,
                        HbrEquipmentRandom.ForKey(seed, $"{act}:{floor}:{room}"));
                    foreach (HbrRolledItem rolled in drop.Items)
                        state = HbrEquipmentRules.AddItem(state, rolled.Template.Id, rolled.Rolls, rolled.Plus, out _);
                    state = HbrEquipmentRules.AddMaterials(state, drop.PlusRelease, drop.PlusChange);
                    (levels, points) = room switch
                    {
                        HbrDropRoom.Normal => (levels + 1, points + 1),
                        HbrDropRoom.Elite => (levels + 2, points + 2),
                        _ => (levels + 3, points + 4)
                    };
                    fights++;
                    if (room != HbrDropRoom.Boss) state = Shop(state, tier, craftedRingHp, ref wallet);
                }
            }
        }

        var results = new ActResult[Acts];
        for (int act = 0; act < Acts; act++)
        {
            double Mean(int fight) => multiplier[act, fight] / runs;
            int[] normals = Enumerable.Range(0, Route.Length).Where(f => Route[f].Room == HbrDropRoom.Normal).ToArray();
            int[] elites = Enumerable.Range(0, Route.Length).Where(f => Route[f].Room == HbrDropRoom.Elite).ToArray();
            results[act] = new ActResult(Mean(normals[0]), normals.Average(Mean), Mean(normals[^1]), elites.Average(Mean),
                Mean(Route.Length - 1), bossGear[act] / runs);
        }
        return results;
    }
}
