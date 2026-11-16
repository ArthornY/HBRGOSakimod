using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.Equipment;

internal enum HbrEquipmentKind { Booster, Chip, Ring, Earring, Bracelet, Necklace, Charm }

// The first six follow HbrBattleStats. Critical rate is stored in percentage
// points, exactly as the original chip text shows it (20 → +0.2%).
internal enum HbrEquipmentStat { Strength, Dexterity, Intelligence, Vitality, Spirit, Luck, Dp, CriticalRate }

// This mod's versions of the original passive skills (docs/hbr-equipment.md).
// Roll effects add flat points to one card type's rolls; damage, gauge and
// destruction effects are percentages; energy is gained every turn.
internal enum HbrEquipmentEffect
{
    AttackRoll, SkillRoll, PowerRoll,
    DpDamage, HpDamage, OverdriveGain, DestructionGain,
    SkillDamage, DestructionFlat, DamageTaken, BossDamageTaken,
    EnergyPerTurn, PerfectStrike
}

// Where a row can come from. Boss rows only drop from boss rewards.
[Flags]
internal enum HbrEquipmentPool { None = 0, Drop = 1, Alchemy = 2, Boss = 4 }

internal readonly record struct HbrEquipmentValue(HbrEquipmentStat Stat, decimal Original);

internal readonly record struct HbrEquipmentEffectValue(HbrEquipmentEffect Effect, decimal Value);

// One row of an original random value table: value, its "+" grade and weight.
internal readonly record struct HbrEquipmentRoll(decimal Original, int Plus, int Weight);

// One original random table, rolled on its own. It raises a stat or an effect.
internal sealed record HbrEquipmentRollGroup(HbrEquipmentStat? Stat, HbrEquipmentEffect? Effect,
    IReadOnlyList<HbrEquipmentRoll> Rows);

internal sealed record HbrEquipmentTemplate(
    string Id, HbrEquipmentKind Kind, int Rarity, string Name, string OriginalName, string Flavor,
    string Icon, int TierFrom, int TierTo, int PlusSlots, int ChipSlots, int Price,
    IReadOnlyList<HbrEquipmentValue> Values)
{
    internal IReadOnlyList<HbrEquipmentEffectValue> Effects { get; init; } = Array.Empty<HbrEquipmentEffectValue>();
    // An item stores one row index per group.
    internal IReadOnlyList<HbrEquipmentRollGroup> RollGroups { get; init; } = Array.Empty<HbrEquipmentRollGroup>();
    internal HbrEquipmentPool Pool { get; init; } = HbrEquipmentPool.Drop | HbrEquipmentPool.Alchemy;

    internal bool IsAccessory => Kind is HbrEquipmentKind.Ring or HbrEquipmentKind.Earring or
        HbrEquipmentKind.Bracelet or HbrEquipmentKind.Necklace;
    internal bool IsSold => Price > 0;
    internal bool InTier(int tier) => tier >= TierFrom && tier <= TierTo;
    internal bool InPool(HbrEquipmentPool pool) => (Pool & pool) != 0;
    // The original flavor line of a passive item describes the original
    // effect, so tooltips show the generated effect lines without it.
    internal bool HasEffects => Effects.Count > 0 || RollGroups.Any(group => group.Effect.HasValue);
}

// Rows copied from the hbr.quest master data snapshot recorded in
// docs/hbr-equipment.md. Names and flavor lines are the China-server text;
// stat lines are generated with this mod's stat names and scaled values.
// Element, SP and passive-skill rows carry this mod's adapted effects.
internal static class HbrEquipmentCatalog
{
    // Original ability and DP values are multiplied once at read time by the
    // item's grade scale. Boosters take the act whose shop sells them, since
    // their stars are only the original icon grade; every other item takes its
    // stars: ★1–2, ★3–4, ★5–6. The scales come from the expected-value
    // calibration in validation/hbr-equipment (docs/hbr-equipment.md).
    internal static readonly decimal[] GradeScales = [1.75m, 1.9m, 2.1m];
    internal const int TierCount = 3;
    internal const int MaxChipSlots = 4;
    internal const int CharmSlots = 6;
    internal const int MaxPlusLevel = 3;

    internal const string PlusReleaseName = "青绿琥珀之露";
    internal const string PlusChangeName = "阳石之露";
    internal const string PlusReleaseIcon = "ThumbnailPlusReleaseMaterial.png";
    internal const string PlusChangeIcon = "ThumbnailPlusChangeMaterial.png";
    internal const int PlusReleasePrice = 40;
    internal const int PlusChangePrice = 20;

    // 饰品 Lv. needed for each 吊饰 slot.
    internal static readonly int[] CharmSlotLevels = [4, 8, 12, 16, 20, 24];

    private static readonly int[] AccessoryCraftPrices = [50, 75, 100];
    private static readonly int[] CharmCraftPrices = [40, 55, 70];

    internal static int AccessoryCraftPrice(int tier) => AccessoryCraftPrices[ClampTier(tier)];
    internal static int CharmCraftPrice(int tier) => CharmCraftPrices[ClampTier(tier)];
    internal static int ClampTier(int tier) => Math.Clamp(tier, 0, TierCount - 1);

    internal static int Grade(HbrEquipmentTemplate template) => template == null ? 0 :
        template.Kind == HbrEquipmentKind.Booster ? ClampTier(template.TierFrom) :
        template.Rarity <= 2 ? 0 : template.Rarity <= 4 ? 1 : 2;

    internal static decimal ScaleOf(HbrEquipmentTemplate template) => GradeScales[Grade(template)];

    private const HbrEquipmentStat P = HbrEquipmentStat.Strength;
    private const HbrEquipmentStat D = HbrEquipmentStat.Dexterity;
    private const HbrEquipmentStat I = HbrEquipmentStat.Intelligence;
    private const HbrEquipmentStat V = HbrEquipmentStat.Vitality;
    private const HbrEquipmentStat S = HbrEquipmentStat.Spirit;
    private const HbrEquipmentStat L = HbrEquipmentStat.Luck;
    private const HbrEquipmentStat Dp = HbrEquipmentStat.Dp;
    private const HbrEquipmentStat Crt = HbrEquipmentStat.CriticalRate;

    private const HbrEquipmentEffect AttackRoll = HbrEquipmentEffect.AttackRoll;
    private const HbrEquipmentEffect SkillRoll = HbrEquipmentEffect.SkillRoll;
    private const HbrEquipmentEffect PowerRoll = HbrEquipmentEffect.PowerRoll;
    private const HbrEquipmentEffect DpDamage = HbrEquipmentEffect.DpDamage;
    private const HbrEquipmentEffect HpDamage = HbrEquipmentEffect.HpDamage;
    private const HbrEquipmentEffect OverdriveGain = HbrEquipmentEffect.OverdriveGain;
    private const HbrEquipmentEffect DestructionGain = HbrEquipmentEffect.DestructionGain;

    // Static fields initialise in source order: Build() reads these tables, so
    // they must stay above All.
    private static readonly HbrEquipmentRoll[] CharmRolls =
        [new(6m, 0, 34), new(9m, 1, 34), new(12m, 2, 18), new(14m, 3, 9), new(15m, 4, 5)];
    // ★6 earrings: the three passive grades are the largest bonus at one hit.
    private static readonly HbrEquipmentRoll[] EarringEffectRolls = [new(10m, 0, 100), new(12m, 1, 100), new(15m, 2, 100)];
    private static readonly HbrEquipmentRoll[] EarringDpRolls = Grades(400, 450, 500, 550, 600, 700, 800, 900, 1200);
    private static readonly HbrEquipmentRoll[] EarringWisdomRolls = Grades(32, 33, 34, 35, 37, 39, 41, 43, 48);
    private static readonly HbrEquipmentRoll[] StrikeBraceletRolls = Grades(29, 30, 31, 32, 34, 36, 38, 40, 45);

    internal static readonly IReadOnlyList<HbrEquipmentTemplate> All = Build();

    private static readonly Dictionary<string, HbrEquipmentTemplate> ById =
        All.ToDictionary(template => template.Id, StringComparer.Ordinal);

    internal static HbrEquipmentTemplate Find(string id) =>
        id != null && ById.TryGetValue(id, out HbrEquipmentTemplate template) ? template : null;

    internal static IEnumerable<HbrEquipmentTemplate> OfKind(HbrEquipmentKind kind) =>
        All.Where(template => template.Kind == kind);

    private static HbrEquipmentValue[] Values(params (HbrEquipmentStat Stat, decimal Value)[] values) =>
        values.Select(value => new HbrEquipmentValue(value.Stat, value.Value)).ToArray();

    // Boosters list their six stats in the original order: power, dexterity,
    // toughness, spirit, wisdom, luck.
    private static HbrEquipmentTemplate Booster(string id, int rarity, string name, string original,
        string flavor, int slots, int price, int tier, decimal p, decimal d, decimal v, decimal s, decimal i,
        decimal l) => new(id, HbrEquipmentKind.Booster, rarity, name, original, flavor,
        $"SeraphBooster{rarity}.png", tier, tier, 0, slots, price,
        Values((P, p), (D, d), (V, v), (S, s), (I, i), (L, l)));

    private static HbrEquipmentTemplate Chip(string id, int rarity, string name, string original,
        string flavor, int price, int tierFrom, int tierTo, params (HbrEquipmentStat, decimal)[] values) =>
        new(id, HbrEquipmentKind.Chip, rarity, name, original, flavor,
            price > 0 ? $"SeraphChip{rarity}.png" : "SeraphArtifactChip1.png",
            tierFrom, tierTo, 0, 0, price, Values(values));

    private static HbrEquipmentTemplate Accessory(string id, HbrEquipmentKind kind, int rarity, string name,
        string original, string flavor, string icon, params (HbrEquipmentStat, decimal)[] values) =>
        new(id, kind, rarity, name, original, flavor, icon, 0, TierCount - 1,
            rarity <= 1 ? 0 : Math.Min(MaxPlusLevel, rarity - 1), 0, 0, Values(values));

    private static HbrEquipmentTemplate Charm(string id, HbrEquipmentStat stat, string name, string original,
        string flavor, string icon) => new HbrEquipmentTemplate(id, HbrEquipmentKind.Charm, 3, name, original,
        flavor, icon, 0, TierCount - 1, 0, 0, 0, Array.Empty<HbrEquipmentValue>())
    {
        RollGroups = [new(stat, null, CharmRolls)],
        Pool = HbrEquipmentPool.Alchemy
    };

    private static HbrEquipmentTemplate HighCharm(string id, HbrEquipmentStat stat, string name,
        string original, string flavor, string icon) => new HbrEquipmentTemplate(id, HbrEquipmentKind.Charm, 4,
        name, original, flavor, icon, 1, TierCount - 1, 0, 0, 0, Values((stat, 18m)))
    {
        Pool = HbrEquipmentPool.Alchemy
    };

    // The original ★6 tables: four low rows, four rows of "+1" and one "+2".
    private static HbrEquipmentRoll[] Grades(params decimal[] values) =>
        values.Select((value, index) => new HbrEquipmentRoll(value, index < 4 ? 0 : index < 8 ? 1 : 2, 100)).ToArray();

    private static HbrEquipmentRollGroup StatGroup(HbrEquipmentStat stat, params HbrEquipmentRoll[] rows) =>
        new(stat, null, rows);

    // Element rings drop from the original corridors. Their element attack bonus
    // becomes a roll bonus for one card type: fire and thunder attacks, ice and
    // light skills, dark powers; 4/5/6/8/10% map to +10/12/13/17/20.
    private static HbrEquipmentTemplate ElementRing(string id, string name, string original, string flavor,
        string icon, HbrEquipmentEffect effect, decimal bonus) =>
        Accessory(id, HbrEquipmentKind.Ring, 6, name, original, flavor, icon, (P, 37), (D, 37)) with
        {
            Effects = [new(effect, bonus)],
            Pool = HbrEquipmentPool.Drop
        };

    // Gate bracelets cut one gate boss's damage by 25/50/75% in the original.
    // Here they cover every boss, so the grades become 5/10/15%.
    private static HbrEquipmentTemplate GateBracelet(string id, string name, string original, string flavor,
        string icon, int tier, decimal stat, decimal cut) =>
        Accessory(id, HbrEquipmentKind.Bracelet, 5, name, original, flavor, icon, (V, stat), (S, stat)) with
        {
            TierFrom = tier,
            TierTo = tier,
            Effects = [new(HbrEquipmentEffect.BossDamageTaken, cut)],
            Pool = HbrEquipmentPool.Boss
        };

    private static HbrEquipmentTemplate EffectEarring(string id, string name, string original, string flavor,
        string icon, HbrEquipmentEffect effect) =>
        Accessory(id, HbrEquipmentKind.Earring, 6, name, original, flavor, icon) with
        {
            RollGroups = [new(null, effect, EarringEffectRolls), StatGroup(Dp, EarringDpRolls), StatGroup(I, EarringWisdomRolls)],
            Pool = HbrEquipmentPool.Alchemy
        };

    // The normal-attack bonus and its grade table are replaced by Perfected Strike.
    private static HbrEquipmentTemplate StrikeBracelet(string id, string name, string original, string flavor,
        string icon) =>
        Accessory(id, HbrEquipmentKind.Bracelet, 6, name, original, flavor, icon) with
        {
            Effects = [new(HbrEquipmentEffect.PerfectStrike, 1m)],
            RollGroups = [StatGroup(V, StrikeBraceletRolls), StatGroup(S, StrikeBraceletRolls)],
            Pool = HbrEquipmentPool.Alchemy
        };

    // Five-star chains grant one energy each turn; six-star protection chains
    // grant two, including on items already kept in a run.
    private static HbrEquipmentTemplate EnergyNecklace(string id, int rarity, string name, string original,
        string flavor, string icon, HbrEquipmentPool pool) =>
        Accessory(id, HbrEquipmentKind.Necklace, rarity, name, original, flavor, icon) with
        {
            Effects = [new(HbrEquipmentEffect.EnergyPerTurn, rarity >= 6 ? 2m : 1m)],
            Pool = pool
        };

    // Ancient chains come from the boss-token exchange, so they drop from
    // bosses from the second act on.
    private static HbrEquipmentTemplate AncientChain(string id, string name, string original, string flavor,
        string icon, bool offensive)
    {
        HbrEquipmentTemplate chain = offensive
            ? Accessory(id, HbrEquipmentKind.Necklace, 6, name, original, flavor, icon)
            : Accessory(id, HbrEquipmentKind.Necklace, 6, name, original, flavor, icon, (Dp, 300));
        HbrEquipmentEffectValue[] effects = offensive
            ?
            [
                new(HbrEquipmentEffect.SkillDamage, 10m), new(HbrEquipmentEffect.DestructionFlat, 10m),
                new(HbrEquipmentEffect.EnergyPerTurn, 1m)
            ]
            : [new(HbrEquipmentEffect.DamageTaken, 10m), new(HbrEquipmentEffect.EnergyPerTurn, 1m)];
        return chain with { TierFrom = 1, Effects = effects, Pool = HbrEquipmentPool.Boss };
    }

    private static List<HbrEquipmentTemplate> Build() =>
    [
        Booster("SBooster_0101", 1, "第一阶段", "ファーストステップ", "在最初阶段值得推荐的增幅器", 1, 25, 0, 8, 8, 8, 8, 8, 8),
        Booster("SBooster_0102", 2, "高墙", "ハイウォール", "给予为同伴挺身而出的你", 2, 40, 0, 12, 12, 15, 15, 12, 12),
        Booster("SBooster_0103", 2, "天使", "アンジェリカ", "为治疗友方的天使提供帮助", 2, 55, 0, 12, 12, 12, 12, 18, 12),
        Booster("SBooster_0104", 2, "战吼", "バトルクライ", "想用HP攻击击败敌方的话非它莫属", 2, 55, 0, 18, 12, 12, 12, 12, 12),
        Booster("SBooster_0105", 2, "群蜂", "フレックスビー", "想用DP攻击扰乱敌方的话非它莫属", 2, 55, 0, 12, 18, 12, 12, 12, 12),
        Booster("SBooster_0201", 1, "控制器", "トリガーボックス", "泛用性高很有安心感的增幅器", 3, 65, 0, 16, 16, 16, 16, 16, 16),
        Booster("SBooster_0202", 2, "战牙", "ウォーファング", "用HP攻击击败敌方！", 3, 80, 0, 21, 18, 18, 18, 18, 18),
        Booster("SBooster_0203", 2, "蝴蝶", "バタフライ", "用DP攻击歼灭敌方", 3, 80, 0, 18, 21, 18, 18, 18, 18),
        Booster("SBooster_0204", 3, "信仰之杖", "ビリーフワンド", "为保护后方挺身而出吧", 3, 90, 0, 21, 21, 24, 24, 21, 21),
        Booster("SBooster_0205", 3, "蓝色之环", "ブルーハイロゥ", "提升回复力与友方突破险境吧", 3, 90, 0, 22, 22, 22, 22, 25, 22),
        Booster("SBooster_0301", 1, "倾注", "ヴェルサーレ", "近乎全能很有安全感的增幅器", 4, 100, 1, 23, 23, 23, 23, 23, 23),
        Booster("SBooster_0302", 2, "粉碎", "ジャガナート", "用HP攻击大肆发泄吧", 4, 100, 1, 29, 24, 24, 24, 24, 24),
        Booster("SBooster_0303", 2, "蜘蛛奇迹", "スパイダーミュー", "用DP攻击让敌方动弹不得吧", 4, 100, 1, 24, 29, 24, 24, 24, 24),
        Booster("SBooster_0304", 3, "永续", "アンブロークン", "给与你保护友方的不屈意志", 4, 105, 1, 29, 29, 31, 31, 29, 29),
        Booster("SBooster_0305", 3, "全盛", "フルブルーマー", "通过灵活的思维来维护同伴的安全", 4, 105, 1, 29, 29, 29, 29, 33, 29),
        Booster("SBooster_0306", 1, "村正", "ムラマサ", "牺牲扩张性从而换得力量的大幅提升", 0, 110, 1, 57, 19, 19, 19, 19, 19),
        Booster("SBooster_0401", 1, "木星", "ジュピター", "高标准制作的新世代增幅器", 4, 115, 1, 34, 34, 34, 34, 34, 34),
        Booster("SBooster_0402", 3, "无畏舰", "ドレッドノート", "大幅提升力量，不断打出强力的HP攻击吧", 4, 115, 1, 40, 36, 36, 36, 36, 36),
        Booster("SBooster_0403", 3, "空中感知", "エリアルフィール", "急剧强化灵巧，打出猛烈的DP攻击吧", 4, 115, 1, 36, 40, 36, 36, 36, 36),
        Booster("SBooster_0404", 2, "十二水坝", "ダズンダム", "大幅强化防御，完成守护友方的使命吧", 4, 115, 1, 35, 35, 40, 40, 35, 35),
        Booster("SBooster_0405", 2, "诺伊曼之帆", "ノイマンセイル", "大脑高速运转，用回复和强化来改变战况吧", 4, 115, 1, 35, 35, 35, 35, 42, 35),
        Booster("SBooster_0501", 2, "阳炎", "カゲロウ", "牺牲扩张性从而换得灵巧的大幅提升", 0, 125, 2, 29, 79, 36, 36, 29, 29),
        Booster("SBooster_0502", 2, "空蝉", "ウツセミ", "提高战斗稳定性的新世代增幅器", 1, 125, 2, 35, 35, 60, 60, 35, 35),
        Booster("SBooster_0601", 1, "大师", "マエストロ", "强化力量，将敌人的HP削减至最低吧", 4, 130, 2, 51, 40, 40, 40, 40, 40),
        Booster("SBooster_0602", 1, "监察员", "インスペクター", "提升灵巧，贯穿敌人的DP吧", 4, 130, 2, 40, 51, 40, 40, 40, 40),
        Booster("SBooster_0603", 1, "指挥家", "コンダクター", "提升智慧，用辅助技能支援同伴吧", 4, 130, 2, 39, 39, 39, 39, 53, 39),
        Booster("SBooster_0701", 1, "全防御", "フルガード", "能够获得强韧耐久性的增幅器", 4, 130, 2, 40, 40, 46, 46, 40, 40),
        Booster("SBooster_0702", 1, "全能者", "ラウンドマン", "全方位提升能力的万能增幅器", 4, 135, 2, 43, 43, 43, 43, 43, 43),

        Chip("SChip_1001", 1, "攻击芯片Ⅰ", "攻撃チップⅠ", "提升攻击能力", 10, 0, 0, (P, 1), (D, 1)),
        Chip("SChip_1002", 1, "耐久芯片Ⅰ", "耐久チップⅠ", "提升防御能力", 10, 0, 0, (V, 1), (S, 1)),
        Chip("SChip_1003", 1, "治愈芯片Ⅰ", "治癒チップⅠ", "为提升治愈能力设计而成", 10, 0, 0, (Dp, 10), (I, 1)),
        Chip("SChip_1004", 1, "HP攻击芯片Ⅰ", "HP攻撃チップⅠ", "提升HP攻击能力", 25, 0, 0, (P, 2)),
        Chip("SChip_1005", 1, "DP攻击芯片Ⅰ", "DP攻撃チップⅠ", "提升DP攻击能力", 25, 0, 0, (D, 2)),
        Chip("SChip_1006", 1, "偏导护盾芯片Ⅰ", "デフレクタチップⅠ", "提升偏导护盾能力", 20, 0, 0, (Dp, 20)),
        Chip("SChip_2001", 2, "暴击芯片Ⅱ", "クリティカルチップⅡ", "更容易出现暴击", 30, 0, 0, (Crt, 0.2m)),
        Chip("SChip_2002", 2, "攻击芯片Ⅱ", "攻撃チップⅡ", "提升攻击能力", 50, 0, 0, (P, 2), (D, 2)),
        Chip("SChip_2003", 2, "耐久芯片Ⅱ", "耐久チップⅡ", "提升防御能力", 50, 0, 0, (V, 2), (S, 2)),
        Chip("SChip_2004", 2, "治愈芯片Ⅱ", "治癒チップⅡ", "为提升治愈能力设计而成", 50, 0, 0, (Dp, 20), (I, 2)),
        Chip("SChip_2005", 2, "偏导护盾芯片Ⅱ", "デフレクタチップⅡ", "提升偏导护盾能力", 55, 0, 0, (Dp, 40)),
        Chip("SChip_2006", 2, "回复芯片Ⅱ", "ヒールチップⅡ", "提升回复能力", 55, 0, 0, (I, 4)),
        Chip("SChip_2007", 2, "HP攻击芯片Ⅱ", "HP攻撃チップⅡ", "提升HP攻击能力", 55, 0, 0, (P, 4)),
        Chip("SChip_2008", 2, "DP攻击芯片Ⅱ", "DP攻撃チップⅡ", "提升DP攻击能力", 55, 0, 0, (D, 4)),
        Chip("SChip_3001", 3, "暴击芯片Ⅲ", "クリティカルチップⅢ", "更容易出现暴击", 60, 1, 1, (Crt, 0.4m)),
        Chip("SChip_3002", 3, "攻击芯片Ⅲ", "攻撃チップⅢ", "提升攻击能力", 60, 1, 1, (P, 3), (D, 3)),
        Chip("SChip_3003", 3, "耐久芯片Ⅲ", "耐久チップⅢ", "提升防御能力", 60, 1, 1, (V, 3), (S, 3)),
        Chip("SChip_3004", 3, "治愈芯片Ⅲ", "治癒チップⅢ", "为提升治愈能力设计而成", 60, 1, 1, (Dp, 30), (I, 3)),
        Chip("SChip_3005", 3, "偏导护盾芯片Ⅲ", "デフレクタチップⅢ", "提升偏导护盾能力", 60, 1, 1, (Dp, 50)),
        Chip("SChip_3006", 3, "回复芯片Ⅲ", "ヒールチップⅢ", "提升回复能力", 60, 1, 1, (I, 5)),
        Chip("SChip_3007", 3, "HP攻击芯片Ⅲ", "HP攻撃チップⅢ", "提升HP攻击能力", 60, 1, 1, (P, 5)),
        Chip("SChip_3008", 3, "DP攻击芯片Ⅲ", "DP攻撃チップⅢ", "提升DP攻击能力", 60, 1, 1, (D, 5)),
        Chip("SChip_4001", 4, "暴击芯片Ⅳ", "クリティカルチップⅣ", "更容易出现暴击", 50, 1, 2, (Crt, 0.6m)),
        Chip("SChip_4002", 4, "耐久芯片Ⅳ", "耐久チップⅣ", "提升防御能力", 70, 1, 2, (V, 4), (S, 3)),
        Chip("SChip_4003", 4, "治愈芯片Ⅳ", "治癒チップⅣ", "为提升治愈能力设计而成", 70, 1, 2, (Dp, 50), (I, 4)),
        Chip("SChip_4004", 4, "偏导护盾芯片Ⅳ", "デフレクタチップⅣ", "提升偏导护盾能力", 70, 1, 2, (Dp, 100)),
        Chip("SChip_4005", 4, "回复芯片Ⅳ", "ヒールチップⅣ", "提升回复能力", 70, 1, 2, (I, 6)),
        Chip("SChip_4006", 4, "HP攻击芯片Ⅳ", "HP攻撃チップⅣ", "提升HP攻击能力", 70, 1, 2, (P, 6)),
        Chip("SChip_4007", 4, "DP攻击芯片Ⅳ", "DP攻撃チップⅣ", "提升DP攻击能力", 70, 1, 2, (D, 6)),
        Chip("SChip_5001", 5, "攻击芯片Ⅴ", "攻撃チップⅤ", "提升攻击能力", 80, 2, 2, (P, 5), (D, 5)),
        Chip("SChip_5002", 5, "耐久芯片Ⅴ", "耐久チップⅤ", "提升防御能力", 80, 2, 2, (V, 5), (S, 5)),
        Chip("SChip_5003", 5, "回复芯片Ⅴ", "ヒールチップⅤ", "提升回复能力", 80, 2, 2, (I, 7)),
        // Original artifact chips are exploration rewards, never shop stock.
        Chip("SChip_5004", 5, "人工芯片（力量）", "アーティファクトチップ（力）", "提升HP攻击能力", 0, 2, 2, (P, 9)),
        Chip("SChip_5005", 5, "人工芯片（灵巧）", "アーティファクトチップ（器用さ）", "提升DP攻击能力", 0, 2, 2, (D, 9)),
        Chip("SChip_6002", 5, "人工芯片（智慧）", "アーティファクトチップ（知性）", "提升回复能力", 0, 2, 2, (I, 9)),
        Chip("SChip_6003", 5, "人工芯片（体力）", "アーティファクトチップ（体力）", "提升体力能力", 0, 2, 2, (V, 9)),

        Accessory("Acc.Ring001a", HbrEquipmentKind.Ring, 1, "光面戒指", "プレインリング", "提升攻击能力", "Ringlow1.png", (P, 8), (D, 8)),
        Accessory("Acc.Ring002a", HbrEquipmentKind.Ring, 1, "精致戒指", "ファインリング", "提升攻击能力", "Ringloww2.png", (P, 12), (D, 12)),
        Accessory("Acc.Ring003a", HbrEquipmentKind.Ring, 2, "激情戒指", "パッションリング", "提升攻击能力", "RingMedium1.png", (P, 15), (D, 15)),
        Accessory("Acc.Ring003b", HbrEquipmentKind.Ring, 2, "能量戒指", "パワーリング", "为HP攻击而设计的戒指", "RingMedium2.png", (P, 18), (D, 13)),
        Accessory("Acc.Ring003c", HbrEquipmentKind.Ring, 2, "技巧戒指", "テクニカルリング", "为DP攻击而设计的戒指", "RingMedium3.png", (P, 13), (D, 18)),
        Accessory("Acc.Ring004a", HbrEquipmentKind.Ring, 3, "白金戒指", "プラチナリング", "提升攻击能力", "RingMediumw4.png", (P, 19), (D, 19)),
        Accessory("Acc.Ring004b", HbrEquipmentKind.Ring, 3, "斗士戒指", "ファイターリング", "为HP攻击而设计的戒指", "RingMediumw3.png", (P, 23), (D, 16)),
        Accessory("Acc.Ring004c", HbrEquipmentKind.Ring, 3, "狙击手戒指", "スナイパーリング", "为DP攻击而设计的戒指", "RingMediumw1.png", (P, 16), (D, 23)),
        Accessory("Acc.Ring005a", HbrEquipmentKind.Ring, 4, "秘银戒指", "ミスリルリング", "提升攻击能力", "RingHigh1.png", (P, 24), (D, 24)),
        Accessory("Acc.Ring005b", HbrEquipmentKind.Ring, 4, "战士戒指", "ウォーリアーリング", "为HP攻击而设计的戒指", "RingHighw2.png", (P, 29), (D, 20)),
        Accessory("Acc.Ring005c", HbrEquipmentKind.Ring, 4, "游侠戒指", "レンジャーリング", "为DP攻击而设计的戒指", "RingHighw3.png", (P, 20), (D, 29)),
        Accessory("Acc.Ring005d", HbrEquipmentKind.Ring, 5, "奥义戒指", "奥義のリング", "专门为HP攻击而设计的戒指", "RingHighw4.png", (P, 49), (D, 11)),
        Accessory("Acc.Ring005e", HbrEquipmentKind.Ring, 5, "神秘戒指", "神秘のリング", "专门为DP攻击而设计的戒指", "RingHighw5.png", (P, 11), (D, 49)),
        ElementRing("Acc.Ring006a_00", "炎之戒指", "炎のリング", "自身火属性技能攻击力+4％", "Ring_High1_fire_w1.png", AttackRoll, 10),
        ElementRing("Acc.Ring006a_01", "炎之戒指", "炎のリング", "自身火属性技能攻击力+5％", "Ring_High1_fire_w1.png", AttackRoll, 12),
        ElementRing("Acc.Ring006a_02", "炎之戒指", "炎のリング", "自身火属性技能攻击力+6％", "Ring_High1_fire_w1.png", AttackRoll, 13),
        ElementRing("Acc.Ring006a_03", "炎之戒指", "炎のリング", "自身火属性技能攻击力+8％", "Ring_High1_fire_w1.png", AttackRoll, 17),
        ElementRing("Acc.Ring006a_04", "炎之戒指", "炎のリング", "自身火属性技能攻击力+10％", "Ring_High1_fire_w1.png", AttackRoll, 20),
        ElementRing("Acc.Ring006b_00", "冰之戒指", "氷のリング", "自身冰属性技能攻击力+4％", "Ring_High1_ice_w1.png", SkillRoll, 10),
        ElementRing("Acc.Ring006b_01", "冰之戒指", "氷のリング", "自身冰属性技能攻击力+5％", "Ring_High1_ice_w1.png", SkillRoll, 12),
        ElementRing("Acc.Ring006b_02", "冰之戒指", "氷のリング", "自身冰属性技能攻击力+6％", "Ring_High1_ice_w1.png", SkillRoll, 13),
        ElementRing("Acc.Ring006b_03", "冰之戒指", "氷のリング", "自身冰属性技能攻击力+8％", "Ring_High1_ice_w1.png", SkillRoll, 17),
        ElementRing("Acc.Ring006b_04", "冰之戒指", "氷のリング", "自身冰属性技能攻击力+10％", "Ring_High1_ice_w1.png", SkillRoll, 20),
        ElementRing("Acc.Ring006c_00", "雷之戒指", "雷のリング", "自身雷属性技能攻击力+4％", "Ring_High1_thunder_w1.png", AttackRoll, 10),
        ElementRing("Acc.Ring006c_01", "雷之戒指", "雷のリング", "自身雷属性技能攻击力+5％", "Ring_High1_thunder_w1.png", AttackRoll, 12),
        ElementRing("Acc.Ring006c_02", "雷之戒指", "雷のリング", "自身雷属性技能攻击力+6％", "Ring_High1_thunder_w1.png", AttackRoll, 13),
        ElementRing("Acc.Ring006c_03", "雷之戒指", "雷のリング", "自身雷属性技能攻击力+8％", "Ring_High1_thunder_w1.png", AttackRoll, 17),
        ElementRing("Acc.Ring006c_04", "雷之戒指", "雷のリング", "自身雷属性技能攻击力+10％", "Ring_High1_thunder_w1.png", AttackRoll, 20),
        ElementRing("Acc.Ring006d_00", "光之戒指", "光のリング", "自身光属性技能攻击力+4％", "Ring_High1_light_w1.png", SkillRoll, 10),
        ElementRing("Acc.Ring006d_01", "光之戒指", "光のリング", "自身光属性技能攻击力+5％", "Ring_High1_light_w1.png", SkillRoll, 12),
        ElementRing("Acc.Ring006d_02", "光之戒指", "光のリング", "自身光属性技能攻击力+6％", "Ring_High1_light_w1.png", SkillRoll, 13),
        ElementRing("Acc.Ring006d_03", "光之戒指", "光のリング", "自身光属性技能攻击力+8％", "Ring_High1_light_w1.png", SkillRoll, 17),
        ElementRing("Acc.Ring006d_04", "光之戒指", "光のリング", "自身光属性技能攻击力+10％", "Ring_High1_light_w1.png", SkillRoll, 20),
        ElementRing("Acc.Ring006e_00", "暗之戒指", "闇のリング", "自身暗属性技能攻击力+4％", "Ring_High1_dark_w1.png", PowerRoll, 10),
        ElementRing("Acc.Ring006e_01", "暗之戒指", "闇のリング", "自身暗属性技能攻击力+5％", "Ring_High1_dark_w1.png", PowerRoll, 12),
        ElementRing("Acc.Ring006e_02", "暗之戒指", "闇のリング", "自身暗属性技能攻击力+6％", "Ring_High1_dark_w1.png", PowerRoll, 13),
        ElementRing("Acc.Ring006e_03", "暗之戒指", "闇のリング", "自身暗属性技能攻击力+8％", "Ring_High1_dark_w1.png", PowerRoll, 17),
        ElementRing("Acc.Ring006e_04", "暗之戒指", "闇のリング", "自身暗属性技能攻击力+10％", "Ring_High1_dark_w1.png", PowerRoll, 20),

        Accessory("Acc.Earring002a", HbrEquipmentKind.Earring, 1, "精致耳坠", "ファインピアス", "提升偏导护盾能力", "EarringsLoww1.png", (Dp, 240)),
        Accessory("Acc.Earring002b", HbrEquipmentKind.Earring, 1, "聪慧耳坠", "スマートピアス", "为提升治愈能力设计而成", "EarringsLoww3.png", (Dp, 110), (I, 15)),
        Accessory("Acc.Earring003a", HbrEquipmentKind.Earring, 2, "黄金耳坠", "ゴールドピアス", "提升偏导护盾能力", "EarringsMedium3.png", (Dp, 300)),
        Accessory("Acc.Earring003b", HbrEquipmentKind.Earring, 2, "才华耳坠", "ウィズダムピアス", "为提升治愈能力设计而成", "EarringsMedium5.png", (Dp, 140), (I, 19)),
        Accessory("Acc.Earring004a", HbrEquipmentKind.Earring, 3, "白金耳坠", "プラチナピアス", "提升偏导护盾能力", "EarringsMediumw1.png", (Dp, 390)),
        Accessory("Acc.Earring004b", HbrEquipmentKind.Earring, 3, "符文耳坠", "ルーンピアス", "为提升治愈能力设计而成", "EarringsMediumw3.png", (Dp, 180), (I, 24)),
        Accessory("Acc.Earring005a", HbrEquipmentKind.Earring, 4, "秘银耳坠", "ミスリルピアス", "提升偏导护盾能力", "EarringsHigh3.png", (Dp, 480)),
        Accessory("Acc.Earring005b", HbrEquipmentKind.Earring, 4, "星光耳坠", "スターライトピアス", "为提升治愈能力设计而成", "EarringsHighw3.png", (Dp, 230), (I, 29)),
        Accessory("Acc.Earring005c", HbrEquipmentKind.Earring, 5, "博爱耳坠", "博愛のピアス", "专门为治愈能力而设计的戒指", "EarringsHighw4.png", (Dp, 200), (I, 40)),
        EffectEarring("Acc.Earring006.F001", "击碎耳坠", "ブレイクピアス", "对DP技能攻击力提升（连击数越少效果越大）", "Earrings_High2_ice_w5.png", DpDamage),
        EffectEarring("Acc.Earring006.F002", "攻击耳坠", "アタックピアス", "对HP技能攻击力提升（连击数越少效果越大）", "Earrings_High2_thunder_w5.png", HpDamage),
        EffectEarring("Acc.Earring006.F003", "驱动耳坠", "ドライブピアス", "增加超频条提升量（连击数越多效果越大）", "Earrings_High3_fire_w1.png", OverdriveGain),
        EffectEarring("Acc.Earring006.F004", "破坏耳坠", "ブラストピアス", "增加破坏率提升量（连击数越多效果越大）", "Earrings_High3_light_w1.png", DestructionGain),

        Accessory("Acc.Bracelet003a", HbrEquipmentKind.Bracelet, 2, "精致手环", "ファインブレス", "提升防御能力", "BraceletMedium3.png", (V, 16), (S, 16)),
        Accessory("Acc.Bracelet004a", HbrEquipmentKind.Bracelet, 3, "防护手环", "ガードブレス", "为物理防御而设计的戒指", "BraceletMediumw3.png", (V, 23), (S, 17)),
        Accessory("Acc.Bracelet004b", HbrEquipmentKind.Bracelet, 3, "心灵手环", "マインドブレス", "为特殊防御而设计的戒指", "BraceletMediumw2.png", (V, 17), (S, 23)),
        Accessory("Acc.Bracelet005a", HbrEquipmentKind.Bracelet, 4, "守护手环", "プロテクトブレス", "为物理防御而设计的戒指", "BraceletHigh5.png", (V, 29), (S, 22)),
        Accessory("Acc.Bracelet005b", HbrEquipmentKind.Bracelet, 4, "灵魂手环", "スピリットブレス", "为特殊防御而设计的戒指", "BraceletHigh4.png", (V, 22), (S, 29)),
        Accessory("Acc.Bracelet005c", HbrEquipmentKind.Bracelet, 5, "庇护手环", "庇護のブレス", "专门为物理防御而设计的戒指", "BraceletHighw3.png", (V, 38), (S, 27)),
        Accessory("Acc.Bracelet005d", HbrEquipmentKind.Bracelet, 5, "退魔手环", "退魔のブレス", "专门为特殊防御而设计的戒指", "BraceletHighw2.png", (V, 27), (S, 38)),
        GateBracelet("Acc.Bracelet006a", "火门手环", "火門のブレス", "小幅减少来自异度监测之眼R的伤害", "Bracelet_High1_fire_1.png", 0, 32, 5),
        GateBracelet("Acc.Bracelet006b", "火门手环", "火門のブレス", "减少来自异度监测之眼R的伤害", "Bracelet_High1_fire_1.png", 0, 32, 10),
        GateBracelet("Acc.Bracelet006c", "火门手环", "火門のブレス", "大幅减少来自异度监测之眼R的伤害", "Bracelet_High1_fire_1.png", 0, 32, 15),
        GateBracelet("Acc.Bracelet007a", "冰门手环", "氷門のブレス", "小幅减少来自异度监测之眼B的伤害", "Bracelet_High1_ice_1.png", 0, 32, 5),
        GateBracelet("Acc.Bracelet007b", "冰门手环", "氷門のブレス", "减少来自异度监测之眼B的伤害", "Bracelet_High1_ice_1.png", 0, 32, 10),
        GateBracelet("Acc.Bracelet007c", "冰门手环", "氷門のブレス", "大幅减少来自异度监测之眼B的伤害", "Bracelet_High1_ice_1.png", 0, 32, 15),
        GateBracelet("Acc.Bracelet008a", "雷门手环", "雷門のブレス", "小幅减少来自异度监测之眼Y的伤害", "Bracelet_High1_thunder_1.png", 0, 32, 5),
        GateBracelet("Acc.Bracelet008b", "雷门手环", "雷門のブレス", "减少来自异度监测之眼Y的伤害", "Bracelet_High1_thunder_1.png", 0, 32, 10),
        GateBracelet("Acc.Bracelet008c", "雷门手环", "雷門のブレス", "大幅减少来自异度监测之眼Y的伤害", "Bracelet_High1_thunder_1.png", 0, 32, 15),
        GateBracelet("Acc.Bracelet009a", "光门手环", "光門のブレス", "小幅减少来自异度监测之眼W的伤害", "Bracelet_High1_light_1.png", 0, 32, 5),
        GateBracelet("Acc.Bracelet009b", "光门手环", "光門のブレス", "减少来自异度监测之眼W的伤害", "Bracelet_High1_light_1.png", 0, 32, 10),
        GateBracelet("Acc.Bracelet009c", "光门手环", "光門のブレス", "大幅减少来自异度监测之眼W的伤害", "Bracelet_High1_light_1.png", 0, 32, 15),
        GateBracelet("Acc.Bracelet010a", "暗门手环", "闇門のブレス", "小幅减少来自异度监测之眼P的伤害", "Bracelet_High1_dark_1.png", 0, 32, 5),
        GateBracelet("Acc.Bracelet010b", "暗门手环", "闇門のブレス", "减少来自异度监测之眼P的伤害", "Bracelet_High1_dark_1.png", 0, 32, 10),
        GateBracelet("Acc.Bracelet010c", "暗门手环", "闇門のブレス", "大幅减少来自异度监测之眼P的伤害", "Bracelet_High1_dark_1.png", 0, 32, 15),
        GateBracelet("Acc.Bracelet011a", "火门手环Ⅱ", "火門のブレスⅡ", "小幅减少来自正之右肌·空无R与恶之左肌·空无R", "Bracelet_High2_fire_1.png", 1, 35, 5),
        GateBracelet("Acc.Bracelet011b", "火门手环Ⅱ", "火門のブレスⅡ", "减少来自正之右肌·空无R与恶之左肌·空无R", "Bracelet_High2_fire_1.png", 1, 35, 10),
        GateBracelet("Acc.Bracelet011c", "火门手环Ⅱ", "火門のブレスⅡ", "大幅减少来自正之右肌·空无R与恶之左肌·空无R", "Bracelet_High2_fire_1.png", 1, 35, 15),
        GateBracelet("Acc.Bracelet012a", "冰门手环Ⅱ", "氷門のブレスⅡ", "小幅减少来自正之右肌·空无B与恶之左肌·空无B", "Bracelet_High2_ice_1.png", 1, 35, 5),
        GateBracelet("Acc.Bracelet012b", "冰门手环Ⅱ", "氷門のブレスⅡ", "减少来自正之右肌·空无B与恶之左肌·空无B", "Bracelet_High2_ice_1.png", 1, 35, 10),
        GateBracelet("Acc.Bracelet012c", "冰门手环Ⅱ", "氷門のブレスⅡ", "大幅减少来自正之右肌·空无B与恶之左肌·空无B", "Bracelet_High2_ice_1.png", 1, 35, 15),
        GateBracelet("Acc.Bracelet013a", "雷门手环Ⅱ", "雷門のブレスⅡ", "小幅减少来自正之右肌·空无Y与恶之左肌·空无Y", "Bracelet_High2_thunder_1.png", 1, 35, 5),
        GateBracelet("Acc.Bracelet013b", "雷门手环Ⅱ", "雷門のブレスⅡ", "减少来自正之右肌·空无Y与恶之左肌·空无Y", "Bracelet_High2_thunder_1.png", 1, 35, 10),
        GateBracelet("Acc.Bracelet013c", "雷门手环Ⅱ", "雷門のブレスⅡ", "大幅减少来自正之右肌·空无Y与恶之左肌·空无Y", "Bracelet_High2_thunder_1.png", 1, 35, 15),
        GateBracelet("Acc.Bracelet014a", "光门手环Ⅱ", "光門のブレスⅡ", "小幅减少来自正之右肌·空无W与恶之左肌·空无W", "Bracelet_High2_light_1.png", 1, 35, 5),
        GateBracelet("Acc.Bracelet014b", "光门手环Ⅱ", "光門のブレスⅡ", "减少来自正之右肌·空无W与恶之左肌·空无W", "Bracelet_High2_light_1.png", 1, 35, 10),
        GateBracelet("Acc.Bracelet014c", "光门手环Ⅱ", "光門のブレスⅡ", "大幅减少来自正之右肌·空无W与恶之左肌·空无W", "Bracelet_High2_light_1.png", 1, 35, 15),
        GateBracelet("Acc.Bracelet015a", "暗门手环Ⅱ", "闇門のブレスⅡ", "小幅减少来自正之右肌·空无P与恶之左肌·空无P", "Bracelet_High2_dark_1.png", 1, 35, 5),
        GateBracelet("Acc.Bracelet015b", "暗门手环Ⅱ", "闇門のブレスⅡ", "减少来自正之右肌·空无P与恶之左肌·空无P", "Bracelet_High2_dark_1.png", 1, 35, 10),
        GateBracelet("Acc.Bracelet015c", "暗门手环Ⅱ", "闇門のブレスⅡ", "大幅减少来自正之右肌·空无P与恶之左肌·空无P", "Bracelet_High2_dark_1.png", 1, 35, 15),
        GateBracelet("Acc.Bracelet016a", "火门手环Ⅲ", "火門のブレスⅢ", "小幅减少来自阿蒙之门R的伤害", "Bracelet_High2_fire_3.png", 2, 38, 5),
        GateBracelet("Acc.Bracelet016b", "火门手环Ⅲ", "火門のブレスⅢ", "减少来自阿蒙之门R的伤害", "Bracelet_High2_fire_3.png", 2, 38, 10),
        GateBracelet("Acc.Bracelet016c", "火门手环Ⅲ", "火門のブレスⅢ", "大幅减少来自阿蒙之门R的伤害", "Bracelet_High2_fire_3.png", 2, 38, 15),
        GateBracelet("Acc.Bracelet017a", "冰门手环Ⅲ", "氷門のブレスⅢ", "小幅减少来自阿蒙之门B的伤害", "Bracelet_High2_ice_3.png", 2, 38, 5),
        GateBracelet("Acc.Bracelet017b", "冰门手环Ⅲ", "氷門のブレスⅢ", "减少来自阿蒙之门B的伤害", "Bracelet_High2_ice_3.png", 2, 38, 10),
        GateBracelet("Acc.Bracelet017c", "冰门手环Ⅲ", "氷門のブレスⅢ", "大幅减少来自阿蒙之门B的伤害", "Bracelet_High2_ice_3.png", 2, 38, 15),
        GateBracelet("Acc.Bracelet018a", "雷门手环Ⅲ", "雷門のブレスⅢ", "小幅减少来自阿蒙之门Y的伤害", "Bracelet_High2_thunder_3.png", 2, 38, 5),
        GateBracelet("Acc.Bracelet018b", "雷门手环Ⅲ", "雷門のブレスⅢ", "减少来自阿蒙之门Y的伤害", "Bracelet_High2_thunder_3.png", 2, 38, 10),
        GateBracelet("Acc.Bracelet018c", "雷门手环Ⅲ", "雷門のブレスⅢ", "大幅减少来自阿蒙之门Y的伤害", "Bracelet_High2_thunder_3.png", 2, 38, 15),
        GateBracelet("Acc.Bracelet019a", "光门手环Ⅲ", "光門のブレスⅢ", "小幅减少来自阿蒙之门W的伤害", "Bracelet_High2_light_3.png", 2, 38, 5),
        GateBracelet("Acc.Bracelet019b", "光门手环Ⅲ", "光門のブレスⅢ", "减少来自阿蒙之门W的伤害", "Bracelet_High2_light_3.png", 2, 38, 10),
        GateBracelet("Acc.Bracelet019c", "光门手环Ⅲ", "光門のブレスⅢ", "大幅减少来自阿蒙之门W的伤害", "Bracelet_High2_light_3.png", 2, 38, 15),
        GateBracelet("Acc.Bracelet020a", "暗门手环Ⅲ", "闇門のブレスⅢ", "小幅减少来自阿蒙之门P的伤害", "Bracelet_High2_dark_3.png", 2, 38, 5),
        GateBracelet("Acc.Bracelet020b", "暗门手环Ⅲ", "闇門のブレスⅢ", "减少来自阿蒙之门P的伤害", "Bracelet_High2_dark_3.png", 2, 38, 10),
        GateBracelet("Acc.Bracelet020c", "暗门手环Ⅲ", "闇門のブレスⅢ", "大幅减少来自阿蒙之门P的伤害", "Bracelet_High2_dark_3.png", 2, 38, 15),
        StrikeBracelet("Acc.Bracelet006.F001", "炎击手环", "炎撃のブレス", "通常攻击攻击力+100～200%在此之上赋予火属性", "Bracelet_High2_fire_w1.png"),
        StrikeBracelet("Acc.Bracelet006.F002", "冰击手环", "氷撃のブレス", "通常攻击攻击力+100～200%在此之上赋予冰属性", "Bracelet_High2_ice_w1.png"),
        StrikeBracelet("Acc.Bracelet006.F003", "雷击手环", "雷撃のブレス", "通常攻击攻击力+100～200%在此之上赋予雷属性", "Bracelet_High2_thunder_w1.png"),
        StrikeBracelet("Acc.Bracelet006.F004", "光击手环", "光撃のブレス", "通常攻击攻击力+100～200%在此之上赋予光属性", "Bracelet_High2_light_w1.png"),
        StrikeBracelet("Acc.Bracelet006.F005", "暗击手环", "闇撃のブレス", "通常攻击攻击力+100～200%在此之上赋予暗属性", "Bracelet_High2_dark_w1.png"),

        Accessory("Acc.Necklace004a", HbrEquipmentKind.Necklace, 1, "幸运项链", "ラッキーチェーン", "似乎会有什么好事发生", "NecklaceMedium4.png", (L, 15)),
        Accessory("Acc.Necklace005a", HbrEquipmentKind.Necklace, 3, "运气项链", "フォーチュンチェーン", "似乎会有什么好事发生", "Necklacemediumw1.png", (L, 38)),
        Accessory("Acc.Necklace005c", HbrEquipmentKind.Necklace, 4, "祝福项链", "祝福のチェーン", "似乎会有什么好事发生", "NecklaceHigh4.png", (L, 48)),
        EnergyNecklace("Acc.Necklace005e", 5, "天命项链", "天命のチェーン", "能感受到神奇的力量", "NecklaceHighw4.png", HbrEquipmentPool.Drop),
        EnergyNecklace("Acc.Necklace006.F001", 6, "火之加护项链", "火の加護のチェーン", "火属性技能的次数+1", "Necklace_High2_fire_w5.png", HbrEquipmentPool.Alchemy),
        EnergyNecklace("Acc.Necklace006.F002", 6, "冰之加护项链", "氷の加護のチェーン", "冰属性技能的次数+1", "Necklace_High2_ice_w5.png", HbrEquipmentPool.Alchemy),
        EnergyNecklace("Acc.Necklace006.F003", 6, "雷之加护项链", "雷の加護のチェーン", "雷属性技能的次数+1", "Necklace_High2_thunder_w5.png", HbrEquipmentPool.Alchemy),
        EnergyNecklace("Acc.Necklace006.F004", 6, "光之加护项链", "光の加護のチェーン", "光属性技能的次数+1", "Necklace_High2_light_w5.png", HbrEquipmentPool.Alchemy),
        EnergyNecklace("Acc.Necklace006.F005", 6, "暗之加护项链", "闇の加護のチェーン", "暗属性技能的次数+1", "Necklace_High2_dark_w5.png", HbrEquipmentPool.Alchemy),
        EnergyNecklace("Acc.Necklace006.F006", 6, "幸甚项链", "ユーフォリアチェーン", "运气+48", "Necklace_High1_ice_w5.png", HbrEquipmentPool.Alchemy) with
        {
            RollGroups = [StatGroup(L, new HbrEquipmentRoll(48m, 0, 100))]
        },
        AncientChain("Acc.Necklace006.F007", "银冰远古项链", "銀氷のエンシェントチェーン", "冰属性技能次数+1", "Necklace_High4_ice.png", offensive: true),
        AncientChain("Acc.Necklace006.F008", "冰套远古项链", "氷套のエンシェントチェーン", "冰属性技能次数+1", "Necklace_High3_ice.png", offensive: false),
        AncientChain("Acc.Necklace006.F009", "火耀远古项链", "火耀のエンシェントチェーン", "火属性技能次数+1", "Necklace_High4_fire.png", offensive: true),
        AncientChain("Acc.Necklace006.F010", "火套远古项链", "火套のエンシェントチェーン", "火属性技能次数+1", "Necklace_High3_fire.png", offensive: false),
        AncientChain("Acc.Necklace006.F011", "雷霆远古项链", "雷霆のエンシェントチェーン", "雷属性技能次数+1", "Necklace_High4_thunder.png", offensive: true),
        AncientChain("Acc.Necklace006.F012", "雷套远古项链", "雷套のエンシェントチェーン", "雷属性技能次数+1", "Necklace_High3_thunder.png", offensive: false),
        AncientChain("Acc.Necklace006.F013", "光辉远古项链", "光輝のエンシェントチェーン", "光属性技能次数+1", "Necklace_High4_light.png", offensive: true),
        AncientChain("Acc.Necklace006.F014", "光套远古项链", "光套のエンシェントチェーン", "光属性技能次数+1", "Necklace_High3_light.png", offensive: false),
        AncientChain("Acc.Necklace006.F015", "常暗远古项链", "常闇のエンシェントチェーン", "暗属性技能次数+1", "Necklace_High4_dark.png", offensive: true),
        AncientChain("Acc.Necklace006.F016", "暗套远古项链", "闇套のエンシェントチェーン", "暗属性技能次数+1", "Necklace_High3_dark.png", offensive: false),

        Charm("Acc.Charm01.F001", P, "力量吊饰", "力のチャーム", "肌肉好像会变得有力量", "Charm_Power1.png"),
        Charm("Acc.Charm01.F002", D, "灵巧吊饰", "器用さのチャーム", "指尖好像会变得灵巧", "Charm_Dexterity1.png"),
        Charm("Acc.Charm01.F003", V, "体力吊饰", "体力のチャーム", "身体好像会变得健康", "Charm_Toughness1.png"),
        Charm("Acc.Charm01.F004", S, "精神吊饰", "精神のチャーム", "内心好像会平静下来", "Charm_Spirit1.png"),
        Charm("Acc.Charm01.F005", I, "智慧吊饰", "知性のチャーム", "考试成绩好像会变好", "Charm_Wisdom1.png"),
        Charm("Acc.Charm01.F006", L, "运气吊饰", "運のチャーム", "好像会有什么好事发生", "Charm_Luck1.png"),
        // The China server has not released high charms; these names are mod translations.
        // Their original Charm_*2 icons are not in the local client cache, so the
        // ordinary charm icons stand in.
        HighCharm("Acc.Charm_Dimension.F001", P, "高级力量吊饰", "力のハイチャーム", "肌肉好像会变得有力量", "Charm_Power1.png"),
        HighCharm("Acc.Charm_Dimension.F002", D, "高级灵巧吊饰", "器用さのハイチャーム", "指尖好像会变得灵巧", "Charm_Dexterity1.png"),
        HighCharm("Acc.Charm_Dimension.F003", V, "高级体力吊饰", "体力のハイチャーム", "身体好像会变得健康", "Charm_Toughness1.png"),
        HighCharm("Acc.Charm_Dimension.F004", S, "高级精神吊饰", "精神のハイチャーム", "内心好像会平静下来", "Charm_Spirit1.png"),
        HighCharm("Acc.Charm_Dimension.F005", I, "高级智慧吊饰", "知性のハイチャーム", "考试成绩好像会变好", "Charm_Wisdom1.png"),
        HighCharm("Acc.Charm_Dimension.F006", L, "高级运气吊饰", "運のハイチャーム", "好像会有什么好事发生", "Charm_Luck1.png")
    ];
}
