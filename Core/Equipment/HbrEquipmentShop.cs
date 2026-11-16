using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HBRGOSakiMod.Core.Equipment;

internal enum HbrShopOfferKind { Item, PlusRelease, PlusChange, Craft }

internal sealed record HbrShopOffer(string Id, HbrShopOfferKind Kind, int Price,
    HbrEquipmentTemplate Template = null, HbrEquipmentKind CraftKind = HbrEquipmentKind.Ring);

// The manual shop: the current act's booster and chip lineup like the
// original chapter shops, the two "+" materials, and alchemy.
internal static partial class HbrEquipmentShop
{
    internal const string PlusReleaseOffer = "material.plus_release";
    internal const string PlusChangeOffer = "material.plus_change";
    private const string ItemPrefix = "item.";
    private const string CraftPrefix = "craft.";

    private static readonly HbrEquipmentKind[] CraftKinds =
    [
        HbrEquipmentKind.Ring, HbrEquipmentKind.Earring, HbrEquipmentKind.Bracelet,
        HbrEquipmentKind.Necklace, HbrEquipmentKind.Charm
    ];

    internal static IReadOnlyList<HbrShopOffer> ShopOffers(int tier)
    {
        int clamped = HbrEquipmentCatalog.ClampTier(tier);
        List<HbrShopOffer> offers = HbrEquipmentCatalog.All
            .Where(template => template.IsSold && template.InTier(clamped))
            .Select(template => new HbrShopOffer(ItemPrefix + template.Id, HbrShopOfferKind.Item, template.Price,
                template))
            .ToList();
        offers.Add(new HbrShopOffer(PlusReleaseOffer, HbrShopOfferKind.PlusRelease, HbrEquipmentCatalog.PlusReleasePrice));
        offers.Add(new HbrShopOffer(PlusChangeOffer, HbrShopOfferKind.PlusChange, HbrEquipmentCatalog.PlusChangePrice));
        return offers;
    }

    internal static IReadOnlyList<HbrShopOffer> AlchemyOffers(int tier) => CraftKinds
        .Select(kind => new HbrShopOffer(CraftPrefix + kind.ToString().ToLowerInvariant(), HbrShopOfferKind.Craft,
            kind == HbrEquipmentKind.Charm
                ? HbrEquipmentCatalog.CharmCraftPrice(tier)
                : HbrEquipmentCatalog.AccessoryCraftPrice(tier), CraftKind: kind))
        .ToList();

    internal static HbrShopOffer Find(string id, int tier) =>
        ShopOffers(tier).Concat(AlchemyOffers(tier)).FirstOrDefault(offer => offer.Id == id);

    internal static HbrShopQuote Quote(HbrEquipmentState state, HbrShopOffer offer,
        HbrSatsukiShopPolicy policy, int actIndex) => offer == null ? default :
        new(offer.Price, policy.Price(offer.Price, offer.Kind == HbrShopOfferKind.Craft,
            !(state.PurchasedActs ?? Array.Empty<int>()).Contains(Math.Max(0, actIndex))));

    // Applies the goods of a paid offer. Gold is handled by the caller.
    internal static HbrEquipmentState Deliver(HbrEquipmentState state, HbrShopOffer offer, int tier,
        out int itemId, out string error)
    {
        itemId = 0;
        error = null;
        if (offer == null) { error = "这件商品现在没有货"; return null; }
        switch (offer.Kind)
        {
            case HbrShopOfferKind.Item:
                return HbrEquipmentRules.AddItem(state, offer.Template.Id, null, null, out itemId);
            case HbrShopOfferKind.PlusRelease:
                return HbrEquipmentRules.AddMaterials(state, 1, 0);
            case HbrShopOfferKind.PlusChange:
                return HbrEquipmentRules.AddMaterials(state, 0, 1);
            default:
                var random = new HbrEquipmentRandom(state);
                HbrRolledItem rolled = offer.CraftKind == HbrEquipmentKind.Charm
                    ? HbrEquipmentGenerator.RollCharm(tier, random)
                    : HbrEquipmentGenerator.RollAccessory(offer.CraftKind, tier, false, random, HbrEquipmentPool.Alchemy);
                return HbrEquipmentRules.AddItem(state with { RandomCounter = random.Counter }, rolled.Template.Id,
                    rolled.Rolls, rolled.Plus, out itemId);
        }
    }

    // Selling keeps its original rule; Satsuki discounts and refunds never alter it.
    internal static int SellPrice(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) return 0;
        if (template.IsSold) return template.Price / 2;
        return 5 * template.Rarity + 2 * HbrEquipmentRules.DisplayPlus(item);
    }
}

internal static class HbrEquipmentText
{
    internal static string StatName(HbrEquipmentStat stat) => stat switch
    {
        HbrEquipmentStat.Strength => "力量",
        HbrEquipmentStat.Dexterity => "灵巧",
        HbrEquipmentStat.Intelligence => "智慧",
        HbrEquipmentStat.Vitality => "体力",
        HbrEquipmentStat.Spirit => "精神",
        HbrEquipmentStat.Luck => "运气",
        HbrEquipmentStat.Dp => "DP",
        _ => "暴击率"
    };

    internal static string KindName(HbrEquipmentKind kind) => kind switch
    {
        HbrEquipmentKind.Booster => "增幅器",
        HbrEquipmentKind.Chip => "芯片",
        HbrEquipmentKind.Ring => "戒指",
        HbrEquipmentKind.Earring => "耳坠",
        HbrEquipmentKind.Bracelet => "手环",
        HbrEquipmentKind.Necklace => "项链",
        _ => "吊饰"
    };

    internal static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    internal static string Value(HbrEquipmentStat stat, decimal scaled) =>
        "+" + Number(scaled) + (stat == HbrEquipmentStat.CriticalRate ? "%" : string.Empty);

    internal static string Stars(int rarity) => new('★', Math.Clamp(rarity, 0, 6));

    internal static string Name(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) return string.Empty;
        int plus = HbrEquipmentRules.DisplayPlus(item);
        return plus > 0 ? $"{template.Name}+{plus}" : template.Name;
    }

    private static readonly HbrEquipmentStat[] SixStats =
    [
        HbrEquipmentStat.Strength, HbrEquipmentStat.Dexterity, HbrEquipmentStat.Vitality,
        HbrEquipmentStat.Spirit, HbrEquipmentStat.Intelligence, HbrEquipmentStat.Luck
    ];

    // Groups equal values the way the original booster text does:
    // "全属性 +14", or "体力、精神 +26.25 / 其他属性 +21".
    internal static string Summary(HbrEquipmentBonus bonus)
    {
        var parts = new List<string>();
        List<IGrouping<decimal, HbrEquipmentStat>> groups = SixStats.Where(stat => bonus[stat] != 0m)
            .GroupBy(stat => bonus[stat]).OrderByDescending(group => group.Key).ToList();
        if (groups.Count == 1 && groups[0].Count() == SixStats.Length)
            parts.Add("全属性 " + Value(HbrEquipmentStat.Strength, groups[0].Key));
        else
        {
            for (int i = 0; i < groups.Count; i++)
            {
                bool rest = i == groups.Count - 1 && groups.Count > 1 && groups[i].Count() >= 3;
                string names = rest ? "其他属性" : string.Join("、", groups[i].Select(StatName));
                parts.Add($"{names} {Value(groups[i].First(), groups[i].Key)}");
            }
        }
        if (bonus.Dp != 0m) parts.Add("DP " + Value(HbrEquipmentStat.Dp, bonus.Dp));
        if (bonus.CriticalRate != 0m) parts.Add("暴击率 " + Value(HbrEquipmentStat.CriticalRate, bonus.CriticalRate));
        return string.Join(" / ", parts);
    }

    // Hit-scaled effects show their value at the favoured end; the other end is
    // HbrEquipmentRules.HitScaledFloor.
    internal static string EffectText(HbrEquipmentEffect effect, string value) => effect switch
    {
        HbrEquipmentEffect.AttackRoll => $"攻击牌出目 +{value}",
        HbrEquipmentEffect.SkillRoll => $"技能牌出目 +{value}",
        HbrEquipmentEffect.PowerRoll => $"能力牌出目 +{value}",
        HbrEquipmentEffect.DpDamage => $"对DP技能攻击力最高 +{value}%（连击数越少效果越大）",
        HbrEquipmentEffect.HpDamage => $"对HP技能攻击力最高 +{value}%（连击数越少效果越大）",
        HbrEquipmentEffect.OverdriveGain => $"OD计量获取最高 +{value}%（连击数越多效果越大）",
        HbrEquipmentEffect.DestructionGain => $"破坏率上升量最高 +{value}%（连击数越多效果越大）",
        HbrEquipmentEffect.SkillDamage => $"技能攻击力 +{value}%",
        HbrEquipmentEffect.DestructionFlat => $"破坏率上升量 +{value}%",
        HbrEquipmentEffect.DamageTaken => $"受到的伤害减少 {value}%",
        HbrEquipmentEffect.BossDamageTaken => $"受到Boss的伤害减少 {value}%",
        HbrEquipmentEffect.EnergyPerTurn => $"每回合开始时获得 {value} 点能量",
        _ => "战斗开始时，基础打击变为完美打击"
    };

    internal static IEnumerable<string> EffectLines(HbrEquipmentItem item) =>
        HbrEquipmentRules.ItemEffects(item).Select(effect => EffectText(effect.Effect, Number(effect.Value)));

    // Summed effects of the whole loadout, in enum order.
    internal static IEnumerable<string> EffectLines(HbrEquipmentEffectTotals totals) =>
        Enum.GetValues<HbrEquipmentEffect>().Where(totals.Has)
            .Select(effect => EffectText(effect, Number(totals[effect])));

    // Stats first, then effects: the line every list shows for an owned item.
    internal static string ItemText(HbrEquipmentItem item)
    {
        string stats = Summary(HbrEquipmentRules.ItemBonus(item));
        return string.Join(" / ", (stats.Length > 0 ? new[] { stats } : Array.Empty<string>()).Concat(EffectLines(item)));
    }

    // Name, the official flavor line unless it describes an adapted effect,
    // then the item line.
    internal static string Tooltip(HbrEquipmentItem item)
    {
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item?.Template);
        if (template == null) return string.Empty;
        string flavor = template.HasEffects || string.IsNullOrEmpty(template.Flavor) ? string.Empty : template.Flavor + "\n";
        return $"{Name(item)}\n{flavor}{ItemText(item)}";
    }

    internal static string Describe(HbrEquipmentTemplate template)
    {
        if (template == null) return string.Empty;
        var parts = new List<string>();
        // An item without rolls reads only the fixed rows.
        string fixedStats = Summary(HbrEquipmentRules.ItemBonus(new HbrEquipmentItem { Template = template.Id }));
        if (fixedStats.Length > 0) parts.Add(fixedStats);
        // Stat tables before effect tables, as in ItemText.
        foreach (HbrEquipmentRollGroup group in template.RollGroups.OrderBy(group => group.Effect.HasValue))
        {
            decimal min = group.Rows.Min(row => row.Original), max = group.Rows.Max(row => row.Original);
            if (group.Stat is HbrEquipmentStat stat)
            {
                string low = Value(stat, HbrEquipmentRules.Scaled(template, stat, min));
                parts.Add(min == max ? $"{StatName(stat)} {low}"
                    : $"{StatName(stat)} {low}～{Number(HbrEquipmentRules.Scaled(template, stat, max))}");
            }
            else if (group.Effect is HbrEquipmentEffect effect)
                parts.Add(EffectText(effect, min == max ? Number(min) : $"{Number(min)}～{Number(max)}"));
        }
        parts.AddRange(template.Effects.Select(effect => EffectText(effect.Effect, Number(effect.Value))));
        if (template.Kind == HbrEquipmentKind.Booster) parts.Add($"芯片装备栏 {template.ChipSlots}");
        return string.Join(" / ", parts);
    }

    // A "+" slot reads in the scale of the accessory it belongs to.
    internal static string PlusSlotText(HbrEquipmentItem item, int index)
    {
        HbrPlusSlot slot = item.Plus[index];
        HbrEquipmentTemplate template = HbrEquipmentCatalog.Find(item.Template);
        return $"{StatName(slot.Stat)} {Value(slot.Stat, HbrEquipmentRules.Scaled(template, slot.Stat, HbrEquipmentRules.PlusOriginal(slot)))}";
    }

    // Every opened "+" slot of an item, as one line: "力量 +5.7、DP +38".
    internal static string PlusSlotsText(HbrEquipmentItem item) =>
        string.Join("、", Enumerable.Range(0, item?.Plus.Length ?? 0).Select(index => PlusSlotText(item, index)));

    // The charm alchemy offer, read from the catalog: the normal charms' roll
    // range and the high charms' fixed value, in battle units.
    internal static string CharmCraftText(int tier, int? highChance = null)
    {
        HbrEquipmentTemplate charm = HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Charm).First(t => t.RollGroups.Count > 0);
        HbrEquipmentTemplate high = HbrEquipmentCatalog.OfKind(HbrEquipmentKind.Charm).First(t => t.RollGroups.Count == 0);
        HbrEquipmentRollGroup rolls = charm.RollGroups[0];
        HbrEquipmentStat stat = rolls.Stat ?? HbrEquipmentStat.Strength;
        string low = Number(HbrEquipmentRules.Scaled(charm, stat, rolls.Rows.Min(row => row.Original)));
        string top = Number(HbrEquipmentRules.Scaled(charm, stat, rolls.Rows.Max(row => row.Original)));
        HbrEquipmentValue value = high.Values[0];
        string highText = Number(HbrEquipmentRules.Scaled(high, value.Stat, value.Original));
        int chance = highChance ?? HbrEquipmentGenerator.HighCharmChance(tier);
        int firstTier = Enumerable.Range(0, HbrEquipmentCatalog.TierCount).FirstOrDefault(t => HbrEquipmentGenerator.HighCharmChance(t) > 0);
        string highLine = chance > 0 ? $"{chance}% 几率获得高级吊饰（+{highText}）"
            : $"第{"一二三"[firstTier]}幕起有几率获得高级吊饰（+{highText}）";
        return $"随机获得一个吊饰，属性 +{low}～{top}\n{highLine}";
    }

    // The scale rule in one line, for the equipment page.
    internal static string ScaleRule()
    {
        IReadOnlyList<decimal> scales = HbrEquipmentCatalog.GradeScales;
        return $"原作数值按装备档次换算后计入战斗属性：★1–2 ×{Number(scales[0])}，★3–4 ×{Number(scales[1])}，★5–6 ×{Number(scales[2])}；" +
            $"增幅器按出售的幕，第一到第三幕依次 ×{Number(scales[0])}、×{Number(scales[1])}、×{Number(scales[2])}。暴击率保持原作数值。";
    }
}
