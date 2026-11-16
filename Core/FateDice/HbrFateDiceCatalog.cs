using System;
using System.Collections.Generic;
using System.Linq;

namespace HBRGOSakiMod.Core.FateDice;

// The fate dice (user, 2026-11-09: 「骰子的效果也不要开局就全部给完，可以随进度给不同的
// 命运碎片，让玩家可以自定义独特的骰子」「魔剑和骰子先这样做」). Design v3, section 4.
// Pure data: no game types, so validation/hbr-fate-dice links this file directly.
internal enum HbrFateFragmentCategory
{
    Range,
    Stability,
    SpecialNumber,
    Gamble,
    Style
}

internal enum HbrFateFragmentRarity
{
    Common,
    Uncommon,
    Rare
}

internal sealed record HbrFateFragment(
    string Id,
    string Name,
    HbrFateFragmentCategory Category,
    HbrFateFragmentRarity Rarity,
    string Description);

internal static class HbrFateDiceCatalog
{
    internal const string WideEdge = "WIDE_EDGE";
    internal const string LastStand = "LAST_STAND";
    internal const string Floor = "FLOOR";
    internal const string FirstMove = "FIRST_MOVE";
    internal const string Reroll = "REROLL";
    internal const string SevenSeal = "SEVEN_SEAL";
    internal const string Mirror = "MIRROR";
    internal const string RoundNumber = "ROUND_NUMBER";
    internal const string WildDie = "WILD_DIE";
    internal const string AllIn = "ALL_IN";
    internal const string LuckyNumbers = "LUCKY_NUMBERS";
    internal const string HawkEye = "HAWK_EYE";
    internal const string SwordSong = "SWORD_SONG";
    internal const string Acceleration = "ACCELERATION";
    internal const string ProtagonistPrivilege = "PROTAGONIST_PRIVILEGE";

    // Values are first drafts (design v3: 「数值都是初版，要等实测再调」).
    internal const int WideEdgeRange = 2;
    internal const int LastStandRange = 5;
    internal const int FloorValue = 20;
    internal const int RerollThreshold = 10;
    internal const int WildDieRange = 5;
    internal const int WildDieThreshold = 10;
    internal const int WildDieSelfDamage = 7;
    internal const int AllInPercent = 15;
    internal const int HawkEyeFragile = 2;
    internal const int SwordSongSwordheart = 2;
    // The two cards turned into fragments keep their base card values.
    internal const int AccelerationPercent = 20;
    internal const int ProtagonistPrivilegePercent = -10;

    internal static readonly IReadOnlyList<HbrFateFragment> All = new[]
    {
        new HbrFateFragment(WideEdge, "宽刃", HbrFateFragmentCategory.Range, HbrFateFragmentRarity.Common,
            $"大成功的原始出目下限降低{WideEdgeRange}。"),
        new HbrFateFragment(LastStand, "绝境", HbrFateFragmentCategory.Range, HbrFateFragmentRarity.Uncommon,
            $"生命低于一半时，大成功的原始出目下限降低{LastStandRange}。"),
        new HbrFateFragment(Floor, "托底", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Common,
            $"原始出目低于{FloorValue}时按{FloorValue}算。"),
        new HbrFateFragment(FirstMove, "先手", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Rare,
            "每回合第一次掷骰取优势骰：额外投1枚骰子，优先取大成功，否则取最高出目。"),
        new HbrFateFragment(Reroll, "再来", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Common,
            $"每场战斗第一次原始出目不超过{RerollThreshold}时，自动重掷一次。"),
        new HbrFateFragment(SevenSeal, "七之刻印", HbrFateFragmentCategory.SpecialNumber, HbrFateFragmentRarity.Rare,
            "原始出目里带数字7时也获得1层剑心；原始出目是7的倍数时改为获得2层。"),
        new HbrFateFragment(Mirror, "镜面", HbrFateFragmentCategory.SpecialNumber, HbrFateFragmentRarity.Rare,
            "原始出目是11、22……99时，这张牌的伤害和格挡翻倍。"),
        new HbrFateFragment(RoundNumber, "整数", HbrFateFragmentCategory.SpecialNumber, HbrFateFragmentRarity.Common,
            "原始出目是10的倍数时，抽1张牌。"),
        new HbrFateFragment(WildDie, "狂骰", HbrFateFragmentCategory.Gamble, HbrFateFragmentRarity.Uncommon,
            $"大成功的原始出目下限降低{WildDieRange}；原始出目不超过{WildDieThreshold}时，自己受到{WildDieSelfDamage}点伤害。"),
        new HbrFateFragment(AllIn, "孤注", HbrFateFragmentCategory.Gamble, HbrFateFragmentRarity.Rare,
            $"出目修正+{AllInPercent}%，但不能取优势骰。"),
        new HbrFateFragment(LuckyNumbers, "幸运数字", HbrFateFragmentCategory.Style, HbrFateFragmentRarity.Uncommon,
            "双剑形态下，原始出目为7或77时也算大成功，不受出目修正影响。"),
        new HbrFateFragment(HawkEye, "鹰眼", HbrFateFragmentCategory.Style, HbrFateFragmentRarity.Uncommon,
            $"弓形态下大成功时，给予所有敌人{HawkEyeFragile}层脆弱。"),
        new HbrFateFragment(SwordSong, "剑鸣", HbrFateFragmentCategory.Style, HbrFateFragmentRarity.Uncommon,
            $"大成功时多获得{SwordSongSwordheart}层剑心。"),
        new HbrFateFragment(Acceleration, "加速", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Uncommon,
            $"出目修正+{AccelerationPercent}%。"),
        new HbrFateFragment(ProtagonistPrivilege, "主人公特典", HbrFateFragmentCategory.Stability, HbrFateFragmentRarity.Rare,
            $"出目修正{ProtagonistPrivilegePercent}%，并额外投1枚骰子；优先取大成功，否则取最高出目。"),
    };

    private static readonly Dictionary<string, HbrFateFragment> ById =
        All.ToDictionary(fragment => fragment.Id, StringComparer.Ordinal);

    internal static HbrFateFragment Find(string id) =>
        id != null && ById.TryGetValue(id, out HbrFateFragment fragment) ? fragment : null;

    internal static bool Exists(string id) => Find(id) != null;

    // Under HBR numerics fixed self-damage is shown and dealt ×100, like every other fixed HP cost.
    internal static string Describe(HbrFateFragment fragment, bool hbrNumerics) =>
        fragment == null ? string.Empty
            : hbrNumerics && fragment.Id == WildDie
                ? fragment.Description.Replace($"受到{WildDieSelfDamage}点伤害", $"受到{WildDieSelfDamage * 100}点伤害")
                : fragment.Description;

    internal static string CategoryName(HbrFateFragmentCategory category) => category switch
    {
        HbrFateFragmentCategory.Range => "区间",
        HbrFateFragmentCategory.Stability => "稳定",
        HbrFateFragmentCategory.SpecialNumber => "特殊点数",
        HbrFateFragmentCategory.Gamble => "赌一把",
        _ => "流派"
    };

    internal static string RarityName(HbrFateFragmentRarity rarity) => rarity switch
    {
        HbrFateFragmentRarity.Common => "普通",
        HbrFateFragmentRarity.Uncommon => "罕见",
        _ => "稀有"
    };

    // The design's category rims: 区间金、稳定蓝、特殊点数紫、赌一把红、流派银 (RGB hex).
    internal static string CategoryColor(HbrFateFragmentCategory category) => category switch
    {
        HbrFateFragmentCategory.Range => "E8C35A",
        HbrFateFragmentCategory.Stability => "7799CC",
        HbrFateFragmentCategory.SpecialNumber => "A77BD9",
        HbrFateFragmentCategory.Gamble => "D9534F",
        _ => "C8CDD6"
    };

    // Shop prices by rarity (a first draft).
    internal static int Price(HbrFateFragmentRarity rarity) => rarity switch
    {
        HbrFateFragmentRarity.Common => 80,
        HbrFateFragmentRarity.Uncommon => 120,
        _ => 160
    };
}
