using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Helpers;

internal enum PhantomFusionEffectType
{
    None = 0,
    Flyconid = 1,
    BruteRubyRaider = 2,
    AssassinRubyRaider = 3,
    AxeRubyRaider = 4,
    CrossbowRubyRaider = 5,
    TrackerRubyRaider = 6,
    Fogmog = 7,
    Mawler = 8,
    FuzzyWurmCrawler = 9,
    Inklet = 10,
    SnappingJaxfruit = 11,
    SlitheringStrangler = 12,
    LeafSlimeS = 13,
    LeafSlimeM = 14,
    TwigSlimeS = 15,
    TwigSlimeM = 16,
    ShrinkerBeetle = 17,
    VineShambler = 18,
    Nibbit = 19,
    CubexConstruct = 20,
    Byrdonis = 21,
    BygoneEffigy = 22,
    PhrogParasite = 23,
    Vantom = 24,
    KinPriest = 25,
    CeremonialBeast = 26,
    CrestHopper = 27,
    GoldHopper = 28,
    HealHopper = 29,
    SmallHopper = 30,
    AbyssKnocker = 31,
    ClayKnocker = 32,
    Knocker = 33,
    TezukaSaki = 34,
    Toadpole = 35,
    DampCultist = 36,
    CalcifiedCultist = 37,
    GremlinMerc = 38,
    FatGremlin = 39,
    Seapunk = 40,
    FossilStalker = 41,
    LivingFog = 42,
    CorpseSlug = 43,
    TwoTailedRat = 44,
    SewerClam = 45,
    HauntedShip = 46,
    SludgeSpinner = 47,
    PunchConstruct = 48,
    SkulkingColony = 49,
    TerrorEel = 50,
    PhantasmalGardener = 51,
    LagavulinMatriarch = 52,
    SoulFysh = 53,
    WaterfallGiant = 54,
    Tunneler = 55,
    SpinyToad = 56,
    Chomper = 57,
    HunterKiller = 58,
    TheObscura = 59,
    BowlbugEgg = 60,
    BowlbugNectar = 61,
    BowlbugRock = 62,
    BowlbugSilk = 63,
    LouseProgenitor = 64,
    SlumberingBeetle = 65,
    ThievingHopper = 66,
    Exoskeleton = 67,
    Myte = 68,
    Ovicopter = 69,
    DecimillipedeSegment = 70,
    Entomancer = 71,
    InfestedPrism = 72,
    Crusher = 73,
    Rocket = 74,
    TheInsatiable = 75,
    KnowledgeDemon = 76,
    GlobeHead = 77,
    TurretOperator = 78,
    LivingShield = 79,
    Axebot = 80,
    OwlMagistrate = 81,
    DevotedSculptor = 82,
    FrogKnight = 83,
    SlimedBerserker = 84,
    TheLost = 85,
    TheForgotten = 86,
    ScrollOfBiting = 87,
    Fabricator = 88,
    MechaKnight = 89,
    SoulNexus = 90,
    FlailKnight = 91,
    SpectralKnight = 92,
    MagiKnight = 93,
    Queen = 94,
    TestSubject = 95,
    Aeonglass = 96,
    MysteriousKnight = 97,
    FakeMerchant = 98,
    FlatHand = 99
}

internal static class PhantomFusionEffectHelper
{
    internal static int GetFusionSlotCount(int experienceValue) =>
        experienceValue >= 4 ? 7 : experienceValue >= 2 ? 5 : 4;

    internal static decimal GetDamageBonusMultiplier(int experienceValue) =>
        experienceValue >= 4 ? 0.7m : experienceValue >= 2 ? 1.2m : 1.5m;

    // The first saved field now includes both direct and inherited fusion counts.
    // The last two fields retain old saves' bonuses, whose original counts were not recorded.
    private readonly record struct EffectState(
        int FusionCount,
        decimal LegacyInheritedPrimary,
        decimal LegacyInheritedSecondary);

    private readonly record struct EffectDefinition(
        decimal PrimaryBase,
        decimal PrimaryAdditional,
        decimal SecondaryBase = 0m,
        decimal SecondaryAdditional = 0m);

    internal static string AddDirectEffect(
        string effectData,
        ModelId materialMonsterId)
    {
        PhantomFusionEffectType type = GetEffectType(materialMonsterId);
        if (type == PhantomFusionEffectType.None)
        {
            return effectData ?? string.Empty;
        }

        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        EffectState state = GetState(effects, type);
        effects[type] = NormalizeState(type, state with
        {
            FusionCount = Math.Min(
                1_000_000,
                Math.Max(0, state.FusionCount) + 1)
        });
        return Serialize(effects);
    }

    internal static string InheritEffects(
        string targetEffectData,
        string sourceEffectData,
        decimal factor)
    {
        Dictionary<PhantomFusionEffectType, EffectState> target =
            Parse(targetEffectData);
        decimal normalizedFactor = Math.Clamp(factor, 0m, 1m);
        foreach ((PhantomFusionEffectType type, EffectState sourceState) in
                 Parse(sourceEffectData))
        {
            int inheritedCount = (int)decimal.Floor(sourceState.FusionCount * normalizedFactor);
            // Only legacy numeric bonuses use the old compatibility path. All new
            // inheritance carries counts, including effects whose value has fallen to zero.
            decimal inheritedPrimary = sourceState.LegacyInheritedPrimary * normalizedFactor;
            decimal inheritedSecondary = sourceState.LegacyInheritedSecondary * normalizedFactor;
            if (inheritedCount > 0 || inheritedPrimary > 0m || inheritedSecondary > 0m)
            {
                EffectState targetState = GetState(target, type);
                target[type] = NormalizeState(type, targetState with
                {
                    FusionCount = Math.Min(1_000_000, targetState.FusionCount + inheritedCount),
                    LegacyInheritedPrimary = Math.Min(
                        1_000_000m,
                        targetState.LegacyInheritedPrimary + inheritedPrimary),
                    LegacyInheritedSecondary = Math.Min(
                        1_000_000m,
                        targetState.LegacyInheritedSecondary + inheritedSecondary)
                });
            }
        }

        return Serialize(target);
    }

    internal static bool HasEffects(string effectData) =>
        Parse(effectData).Values.Any(state =>
            state.FusionCount > 0 ||
            state.LegacyInheritedPrimary > 0m ||
            state.LegacyInheritedSecondary > 0m);

    internal static bool HasEffect(
        string effectData,
        PhantomFusionEffectType type)
    {
        EffectState state = GetState(Parse(effectData), type);
        return state.FusionCount > 0 ||
            state.LegacyInheritedPrimary > 0m ||
            state.LegacyInheritedSecondary > 0m;
    }

    internal static IReadOnlyList<PhantomFusionEffectType> GetEffectTypes(
        string effectData) =>
        Parse(effectData)
            .Where(pair =>
                pair.Key != PhantomFusionEffectType.None &&
                (pair.Value.FusionCount > 0 ||
                 pair.Value.LegacyInheritedPrimary > 0m ||
                 pair.Value.LegacyInheritedSecondary > 0m))
            .Select(pair => pair.Key)
            .OrderBy(type => (int)type)
            .ToArray();

    internal static string KeepOnlyEffects(
        string effectData,
        IEnumerable<PhantomFusionEffectType> retainedTypes)
    {
        HashSet<PhantomFusionEffectType> retained = retainedTypes?
            .Where(type => type != PhantomFusionEffectType.None)
            .ToHashSet() ?? new HashSet<PhantomFusionEffectType>();
        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        foreach (PhantomFusionEffectType type in effects.Keys
                     .Where(type => !retained.Contains(type))
                     .ToArray())
        {
            effects.Remove(type);
        }

        return Serialize(effects);
    }

    internal static string GetEffectDescription(
        string effectData,
        PhantomFusionEffectType type,
        Creature resourceOwner = null)
    {
        string isolated = KeepOnlyEffects(effectData, new[] { type });
        return GetDescriptionLines(isolated, resourceOwner).FirstOrDefault() ??
            type.ToString();
    }

    internal static PhantomFusionEffectType GetMonsterEffectType(
        ModelId monsterId) => GetEffectType(monsterId);

    internal static string Normalize(string effectData) =>
        Serialize(Parse(effectData));

    internal static decimal GetBaseDamageBonusPercent(
        int experienceValue)
    {
        return experienceValue >= 4
            ? 0m
            : experienceValue >= 2
                ? 10m
                : 20m;
    }

    internal static decimal GetStaticDamageBonusPercent(
        string effectData)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        return GetEffect(effects, PhantomFusionEffectType.AssassinRubyRaider, 5m, 2m) +
            GetEffect(effects, PhantomFusionEffectType.FuzzyWurmCrawler, 5m, 2m) +
            GetEffect(effects, PhantomFusionEffectType.TwigSlimeS, 2m, 1m) +
            GetEffect(effects, PhantomFusionEffectType.TwigSlimeM, 2m, 1m) +
            GetEffect(effects, PhantomFusionEffectType.CrestHopper, 5m, 2m) +
            GetEffect(effects, PhantomFusionEffectType.ClayKnocker, 5m, 2m) +
            GetEffect(effects, PhantomFusionEffectType.Knocker, 3m, 1m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.TheInsatiable, 0m, 80m) -
            GetSecondaryEffect(effects, PhantomFusionEffectType.TurretOperator, 30m, 10m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.SlimedBerserker, 15m, 8m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.ScrollOfBiting, 0m, 1m) * 3m +
            GetSecondaryEffect(effects, PhantomFusionEffectType.MechaKnight, 0m, 10m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.SoulNexus, 0m, 10m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.FlailKnight, 0m, 6m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.MagiKnight, -12m, 6m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.TestSubject, 0m, 5m);
    }

    internal static decimal GetStaticDamageReductionPercent(
        string effectData)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        return GetEffect(
                effects,
                PhantomFusionEffectType.AxeRubyRaider,
                3m,
                1m) +
            GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.LivingShield,
                0m,
                5m) +
            GetSecondaryEffect(
                effects,
                PhantomFusionEffectType.OwlMagistrate,
                0m,
                3m);
    }

    internal static decimal GetBonusMaxHp(string effectData)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        return GetEffect(effects, PhantomFusionEffectType.LeafSlimeS, 2m, 1m) +
            GetEffect(effects, PhantomFusionEffectType.LeafSlimeM, 3m, 2m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.TwigSlimeS, 1m, 0m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.TwigSlimeM, 1m, 0m) +
            GetEffect(effects, PhantomFusionEffectType.DecimillipedeSegment, 6m, 3m) +
            GetEffect(effects, PhantomFusionEffectType.SlimedBerserker, 8m, 0m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.ScrollOfBiting, 0m, 1m);
    }

    internal static decimal GetExtraMaxDp(string effectData)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        return GetEffect(effects, PhantomFusionEffectType.SmallHopper, 1m, 1m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.ClayKnocker, 2m, 1m) +
            GetSecondaryEffect(effects, PhantomFusionEffectType.Knocker, 1m, 1m) +
            GetEffect(effects, PhantomFusionEffectType.TezukaSaki, 0m, 10m);
    }

    internal static int GetSkulkingColonyShellAmount(string effectData, decimal maximumHp)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects = Parse(effectData);
        if (!effects.ContainsKey(PhantomFusionEffectType.SkulkingColony)) return 0;

        decimal percent = GetEffect(effects, PhantomFusionEffectType.SkulkingColony, 60m, -10m);
        // Lower caps strengthen Hardened Shell. Keep a real one-point cap when
        // repeated fusion or small maximum HP would otherwise produce zero.
        return (int)Math.Clamp(decimal.Floor(Math.Max(0, maximumHp) * percent / 100m),
            1m, int.MaxValue);
    }

    internal static decimal GetEffect(
        string effectData,
        PhantomFusionEffectType type,
        decimal baseValue,
        decimal additionalValue)
    {
        return GetEffect(Parse(effectData), type, baseValue, additionalValue);
    }

    internal static decimal GetFixedEffect(
        string effectData,
        PhantomFusionEffectType type,
        decimal value)
    {
        return GetSecondaryEffect(
            Parse(effectData),
            type,
            value,
            0m);
    }

    internal static decimal GetSecondaryEffect(
        string effectData,
        PhantomFusionEffectType type,
        decimal baseValue,
        decimal additionalValue)
    {
        return GetSecondaryEffect(
            Parse(effectData),
            type,
            baseValue,
            additionalValue);
    }

    internal static IReadOnlyList<string> GetDescriptionLines(
        string effectData,
        Creature resourceOwner = null)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects =
            Parse(effectData);
        List<string> descriptions = new();
        string Resource(decimal value) => Format(HbrResourceValueHelper.Fixed(resourceOwner, value));

        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Flyconid, 2m, 1m,
            value => $"每第3次攻击意图额外给予{Format(value)}层易伤。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.BruteRubyRaider, 1m, 1m,
            value => $"每第2次强化意图额外获得{Format(value)}点力量。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.AssassinRubyRaider, 5m, 2m,
            value => $"伤害增幅+{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.AxeRubyRaider, 3m, 1m,
            value => $"伤害减免+{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.CrossbowRubyRaider, 5m, 2m,
            value => $"非攻击意图额外获得{Format(value)}点格挡。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.TrackerRubyRaider, 1m, 1m,
            value => $"多段攻击段数+{Format(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Fogmog, 1m, 1m,
            value => $"第1次行动后，召唤一只利齿之眼Lv.{Format(value)}（幻影），其拥有爪牙。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Mawler, 1m, 1m,
            value => $"第1次攻击意图段数+{Format(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.FuzzyWurmCrawler, 5m, 2m,
            value => $"伤害增幅+{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Inklet, 1m, 1m,
            value => $"被召唤时，获得{Format(value)}层滑溜。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SnappingJaxfruit, 2m, 1m,
            value => $"每次攻击意图，伤害增幅+{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SlitheringStrangler, 5m, 2m,
            value => $"第1次行动后，给予敌方全体{Format(value)}层紧缠。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.LeafSlimeS, 2m, 1m,
            value => $"最大生命值+{Resource(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.LeafSlimeM, 3m, 2m,
            value => $"最大生命值+{Resource(value)}。");
        AddTwigDescription(descriptions, effects, PhantomFusionEffectType.TwigSlimeS, resourceOwner);
        AddTwigDescription(descriptions, effects, PhantomFusionEffectType.TwigSlimeM, resourceOwner);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.ShrinkerBeetle, 2m, 1m,
            value => $"第2次行动后，额外给予敌方全体{Format(value)}层虚弱。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.VineShambler, 2m, 2m,
            value => $"每第4次行动，使下一次行动时敌方全体本回合力量-{Format(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Nibbit, 3m, 1m,
            value => $"攻击意图额外获得{Format(value)}点格挡。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.CubexConstruct, 1m, 1m,
            value => $"被召唤时，获得{Format(value)}层人工制品。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Byrdonis, 1m, 1m,
            value => $"被召唤时，获得{Format(value)}层领地意识。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.BygoneEffigy, 1m, 1m,
            value => $"被召唤时，给予生命值最高的{Format(value)}名敌方单位各1层缓慢。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.PhrogParasite, 6m, 3m,
            value => $"每次强化意图，给予敌方全体{Format(value)}层中毒。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Vantom, 3m, 2m,
            value => $"被召唤时，额外获得{Format(value)}层滑溜。");
        AddKinPriestDescription(descriptions, effects);
        AddCeremonialBeastDescription(descriptions, effects);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.CrestHopper, 5m, 2m,
            value => $"伤害增幅+{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.GoldHopper, 5m, 3m,
            value => $"战斗结束后，额外获得{Format(value)}金币。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.HealHopper, 3m, 1m,
            value => $"强化意图额外回复{Resource(value)}点偏导护盾。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SmallHopper, 1m, 1m,
            value => $"最大偏导护盾+{Resource(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.AbyssKnocker, 5m, 3m,
            value => $"每第3次攻击意图，额外造成{Format(value)}点伤害。");
        AddClayKnockerDescription(descriptions, effects, resourceOwner);
        AddKnockerDescription(descriptions, effects, resourceOwner);
        AddTezukaDescription(descriptions, effects, resourceOwner);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Toadpole, 4m, 2m,
            value => $"被召唤时，获得{Format(value)}层荆棘。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.DampCultist, 3m, 1m,
            value => $"被召唤时，获得{Format(value)}点仪式。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.CalcifiedCultist,
            1m, 0m, 0m, 2m,
            (ritual, damage) => damage > 0m
                ? $"被召唤时，获得{Format(ritual)}点仪式；每回合开始时，伤害增幅+{Format(damage)}%。"
                : $"被召唤时，获得{Format(ritual)}点仪式。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.GremlinMerc,
            3m, 1m, 3m, 0m,
            (gold, damage) => $"战斗结束后，额外获得{Format(gold)}金币和{Format(damage)}%伤害增幅。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.FatGremlin, 5m, 3m,
            value => $"战斗结束后，额外获得{Format(value)}金币。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Seapunk, 3m, 1m,
            value => $"强化意图额外获得{Format(value)}点格挡。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.FossilStalker,
            1m, 0m, 0m, 3m,
            (suck, damage) => damage > 0m
                ? $"被召唤时，获得{Format(suck)}层吮吸；造成未被格挡的伤害时，伤害增幅+{Format(damage)}%。"
                : $"被召唤时，获得{Format(suck)}层吮吸。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.LivingFog, 1m, 1m,
            value => $"每第3次攻击意图，召唤{Format(value)}只气态炸弹Lv.1（幻影），其拥有爪牙。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.CorpseSlug,
            1m, 0m, 0m, 5m,
            (strength, damage) => damage > 0m
                ? $"自己的爪牙死亡时，获得{Format(strength)}点力量和{Format(damage)}%伤害增幅。"
                : $"自己的爪牙死亡时，获得{Format(strength)}点力量。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.TwoTailedRat, 1m, 1m,
            value => $"第1回合结束时，召唤一只双尾鼠Lv.{Format(value)}（幻影），其拥有爪牙。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SewerClam, 4m, 2m,
            value => $"被召唤时，获得{Format(value)}层覆甲。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.HauntedShip,
            2m, 1m, 3m, 1m,
            (weak, strength) => $"每第3次攻击意图，给予敌方全体{Format(weak)}层虚弱，并使本回合即将造成伤害最高的敌方单位力量-{Format(strength)}。");
        AddSludgeSpinnerDescription(descriptions, effects);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.PunchConstruct, 1m, 1m,
            value => $"被召唤时，获得{Format(value)}层人工制品。");
        AddSkulkingColonyDescription(descriptions, effects);
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.TerrorEel,
            99m, 0m, 0m, 5m,
            (vulnerable, fragile) => fragile > 0m
                ? $"生命值首次低于一半时，给予敌方全体{Format(vulnerable)}层易伤和{Format(fragile)}层脆弱。"
                : $"生命值首次低于一半时，给予敌方全体{Format(vulnerable)}层易伤。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.PhantasmalGardener, 6m, 3m,
            value => $"被召唤时，获得{Format(value)}层胆小。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.LagavulinMatriarch, 8m, 4m,
            value => $"战斗开始时，获得{Format(value)}层覆甲。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SoulFysh, 2m, 1m,
            value => $"偏导护盾归零时，立刻获得{Format(value)}层无实体。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.WaterfallGiant, 2m, 1m,
            value => $"每次行动后，获得{Format(value)}层蒸汽喷发。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Tunneler, 16m, 8m,
            value => $"第2回合开始时，获得{Format(value)}点格挡。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SpinyToad, 8m, 4m,
            value => $"被召唤时，获得{Format(value)}层荆棘。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Chomper, 2m, 2m,
            value => $"被召唤时，获得{Format(value)}层人工制品。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.HunterKiller, 1m, 1m,
            value => $"玩家回合内，幻影每行动3次，使敌方全体失去{Format(value)}点力量和敏捷。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.TheObscura, 1m, 2m,
            value => $"被召唤时，召唤一只寄生惧魔Lv.{Format(value)}（幻影），其拥有爪牙。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.BowlbugEgg,
            0m, 2m, 1m, 0m,
            (bonus, _) => bonus > 0m
                ? $"被召唤时，将1张免费打出且带有消耗的铁波斩加入手牌，其伤害和格挡+{Format(bonus)}。"
                : "被召唤时，将1张免费打出且带有消耗的铁波斩加入手牌。" );
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.BowlbugNectar,
            1m, 1m, 8m, 0m,
            (turnReduction, strength) => $"第{Format(Math.Max(1m, 5m - turnReduction))}回合开始时，获得{Format(strength)}点力量。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.BowlbugRock, 1m, 1m,
            value => $"被召唤时，给予生命值最高的{Format(value)}名敌方单位一次性的失衡。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.BowlbugSilk, 1m, 1m,
            value => $"每第3次攻击意图额外给予{Format(value)}层虚弱。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.LouseProgenitor, 10m, 5m,
            value => $"被召唤时，获得{Format(value)}层蜷身。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SlumberingBeetle, 10m, 5m,
            value => $"被召唤时，获得{Format(value)}层覆甲。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.ThievingHopper,
            1m, 0m, 0m, 1m,
            (rewards, extraOptions) => extraOptions > 0m
                ? $"战斗结束后，额外获得{Format(rewards)}次卡牌奖励，此次奖励额外获得{Format(extraOptions)}个可选项。"
                : $"战斗结束后，额外获得{Format(rewards)}次卡牌奖励。");
        AddExoskeletonDescription(descriptions, effects);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Myte, 3m, 1m,
            value => $"每第3次攻击意图的每段攻击额外给予{Format(value)}层中毒。");
        AddOvicopterDescription(descriptions, effects);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.DecimillipedeSegment, 6m, 3m,
            value => $"最大生命值+{Resource(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Entomancer, 1m, 1m,
            value => $"每次受到攻击时，使攻击者下回合力量-{Format(value)}。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.InfestedPrism, 2m, 1m,
            value => $"每次受到攻击时，使攻击者获得{Format(value)}层污染。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Crusher, 25m, 15m,
            value => $"奇数回合时，伤害增幅+{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Rocket, 20m, 10m,
            value => $"偶数回合时，伤害减免+{Format(value)}%。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.TheInsatiable,
            8m, 0m, 0m, 80m,
            (strength, damage) => damage > 0m
                ? $"第6回合开始时获得{Format(strength)}点力量；伤害增幅+{Format(damage)}%。"
                : $"第6回合开始时获得{Format(strength)}点力量。");
        AddKnowledgeDemonDescription(descriptions, effects);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.GlobeHead, 1m, 1m,
            value => $"玩家的能力牌能量消耗-{Format(value)}。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.TurretOperator,
            1m, 1m, 30m, 10m,
            (segments, penalty) => $"攻击意图段数+{Format(segments)}，伤害增幅-{Format(penalty)}%。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.LivingShield,
            10m, 0m, 0m, 5m,
            (plating, reduction) => reduction > 0m
                ? $"被召唤时获得{Format(plating)}层覆甲；伤害减免+{Format(reduction)}%。"
                : $"被召唤时获得{Format(plating)}层覆甲。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Axebot, 1m, 3m,
            value => $"被召唤时，召唤一只巨斧机器人Lv.{Format(value)}（幻影），其拥有爪牙且初始不获得库存。");
        AddOwlMagistrateDescription(descriptions, effects);
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.DevotedSculptor,
            4m, 0m, 0m, 3m,
            (ritual, damage) => damage > 0m
                ? $"被召唤时获得{Format(ritual)}点仪式；每次行动后伤害增幅+{Format(damage)}%。"
                : $"被召唤时获得{Format(ritual)}点仪式。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.FrogKnight, 12m, 6m,
            value => $"被召唤时，获得{Format(value)}层覆甲。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.SlimedBerserker,
            8m, 0m, 15m, 8m,
            (hp, damage) => $"最大生命值+{Resource(hp)}，伤害增幅+{Format(damage)}%。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.TheLost,
            4m, 1m, 2m, 1m,
            (playerLoss, strength) => $"被召唤时，玩家力量-{Format(playerLoss)}，自身获得{Format(strength)}点力量。");
        AddForgottenDescription(descriptions, effects);
        AddScrollOfBitingDescription(descriptions, effects, resourceOwner);
        AddFabricatorDescription(descriptions, effects);
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.MechaKnight,
            3m, 0m, 0m, 10m,
            (artifact, damage) => damage > 0m
                ? $"被召唤时获得{Format(artifact)}层人工制品；伤害增幅+{Format(damage)}%。"
                : $"被召唤时获得{Format(artifact)}层人工制品。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.SoulNexus,
            1m, 0m, 0m, 10m,
            (doubleDamage, damage) => damage > 0m
                ? $"被召唤时获得{Format(doubleDamage)}层双倍伤害；伤害增幅+{Format(damage)}%。"
                : $"被召唤时获得{Format(doubleDamage)}层双倍伤害。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.FlailKnight,
            2m, 0m, 0m, 6m,
            (strength, damage) => damage > 0m
                ? $"每第2次强化意图额外获得{Format(strength)}点力量；伤害增幅+{Format(damage)}%。"
                : $"每第2次强化意图额外获得{Format(strength)}点力量。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.SpectralKnight, 1m, 1m,
            value => $"玩家前{Format(value)}次打出的消耗牌不会消耗。");
        AddMagiKnightDescription(descriptions, effects);
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Queen, 1m, 5m,
            value => $"被召唤时，召唤一只火炬头聚合体Lv.{Format(value)}（幻影），其拥有爪牙。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.TestSubject,
            1m, 0m, 0m, 5m,
            (strength, damage) => damage > 0m
                ? $"每死亡一个单位，获得{Format(strength)}点力量；伤害增幅+{Format(damage)}%。"
                : $"每死亡一个单位，获得{Format(strength)}点力量。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.Aeonglass, 1m, 1m,
            value => $"被召唤时，玩家从抽牌堆中选取{Format(value)}张牌加入手牌。");
        AddDualAmountDescription(descriptions, effects, PhantomFusionEffectType.MysteriousKnight,
            2m, 1m, 6m, 3m,
            (strength, plating) => $"被召唤时，获得{Format(strength)}点力量和{Format(plating)}层覆甲。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.FakeMerchant, 20m, 15m,
            value => $"商人售价-{Format(value)}%。");
        AddAmountDescription(descriptions, effects, PhantomFusionEffectType.FlatHand, 1m, 1m,
            value => $"获得{Format(value)}层空间扭曲-伪：受到攻击前掷1d100，若出目大于100-33*1.5^(层数-1)，伤害减半。其余掷骰结算与最强之盾相同。");
        return descriptions;
    }

    internal static int ToInt(decimal value) =>
        Math.Max(0, (int)Math.Floor(Math.Max(0m, value)));

    private static PhantomFusionEffectType GetEffectType(ModelId monsterId)
    {
        MonsterModel monster = ModelDb.GetByIdOrNull<MonsterModel>(monsterId);
        return monster switch
        {
            MysteriousKnight => PhantomFusionEffectType.MysteriousKnight,
            FlailKnight => PhantomFusionEffectType.FlailKnight,
            Flyconid => PhantomFusionEffectType.Flyconid,
            BruteRubyRaider => PhantomFusionEffectType.BruteRubyRaider,
            AssassinRubyRaider => PhantomFusionEffectType.AssassinRubyRaider,
            AxeRubyRaider => PhantomFusionEffectType.AxeRubyRaider,
            CrossbowRubyRaider => PhantomFusionEffectType.CrossbowRubyRaider,
            TrackerRubyRaider => PhantomFusionEffectType.TrackerRubyRaider,
            Fogmog => PhantomFusionEffectType.Fogmog,
            Mawler => PhantomFusionEffectType.Mawler,
            FuzzyWurmCrawler => PhantomFusionEffectType.FuzzyWurmCrawler,
            Inklet => PhantomFusionEffectType.Inklet,
            SnappingJaxfruit => PhantomFusionEffectType.SnappingJaxfruit,
            SlitheringStrangler => PhantomFusionEffectType.SlitheringStrangler,
            LeafSlimeS => PhantomFusionEffectType.LeafSlimeS,
            LeafSlimeM => PhantomFusionEffectType.LeafSlimeM,
            TwigSlimeS => PhantomFusionEffectType.TwigSlimeS,
            TwigSlimeM => PhantomFusionEffectType.TwigSlimeM,
            ShrinkerBeetle => PhantomFusionEffectType.ShrinkerBeetle,
            VineShambler => PhantomFusionEffectType.VineShambler,
            Nibbit => PhantomFusionEffectType.Nibbit,
            CubexConstruct => PhantomFusionEffectType.CubexConstruct,
            Byrdonis => PhantomFusionEffectType.Byrdonis,
            BygoneEffigy => PhantomFusionEffectType.BygoneEffigy,
            PhrogParasite => PhantomFusionEffectType.PhrogParasite,
            Vantom => PhantomFusionEffectType.Vantom,
            KinPriest => PhantomFusionEffectType.KinPriest,
            CeremonialBeast => PhantomFusionEffectType.CeremonialBeast,
            CrestHopper => PhantomFusionEffectType.CrestHopper,
            GoldHopper => PhantomFusionEffectType.GoldHopper,
            HealHopper => PhantomFusionEffectType.HealHopper,
            SmallHopper => PhantomFusionEffectType.SmallHopper,
            AbyssKnocker => PhantomFusionEffectType.AbyssKnocker,
            ClayKnocker => PhantomFusionEffectType.ClayKnocker,
            Knocker => PhantomFusionEffectType.Knocker,
            TezukaSaki => PhantomFusionEffectType.TezukaSaki,
            Toadpole => PhantomFusionEffectType.Toadpole,
            DampCultist => PhantomFusionEffectType.DampCultist,
            CalcifiedCultist => PhantomFusionEffectType.CalcifiedCultist,
            GremlinMerc => PhantomFusionEffectType.GremlinMerc,
            FatGremlin => PhantomFusionEffectType.FatGremlin,
            Seapunk => PhantomFusionEffectType.Seapunk,
            FossilStalker => PhantomFusionEffectType.FossilStalker,
            LivingFog => PhantomFusionEffectType.LivingFog,
            CorpseSlug => PhantomFusionEffectType.CorpseSlug,
            TwoTailedRat => PhantomFusionEffectType.TwoTailedRat,
            SewerClam => PhantomFusionEffectType.SewerClam,
            HauntedShip => PhantomFusionEffectType.HauntedShip,
            SludgeSpinner => PhantomFusionEffectType.SludgeSpinner,
            PunchConstruct => PhantomFusionEffectType.PunchConstruct,
            SkulkingColony => PhantomFusionEffectType.SkulkingColony,
            TerrorEel => PhantomFusionEffectType.TerrorEel,
            PhantasmalGardener => PhantomFusionEffectType.PhantasmalGardener,
            LagavulinMatriarch => PhantomFusionEffectType.LagavulinMatriarch,
            SoulFysh => PhantomFusionEffectType.SoulFysh,
            WaterfallGiant => PhantomFusionEffectType.WaterfallGiant,
            Tunneler => PhantomFusionEffectType.Tunneler,
            SpinyToad => PhantomFusionEffectType.SpinyToad,
            Chomper => PhantomFusionEffectType.Chomper,
            HunterKiller => PhantomFusionEffectType.HunterKiller,
            TheObscura => PhantomFusionEffectType.TheObscura,
            BowlbugEgg => PhantomFusionEffectType.BowlbugEgg,
            BowlbugNectar => PhantomFusionEffectType.BowlbugNectar,
            BowlbugRock => PhantomFusionEffectType.BowlbugRock,
            BowlbugSilk => PhantomFusionEffectType.BowlbugSilk,
            LouseProgenitor => PhantomFusionEffectType.LouseProgenitor,
            SlumberingBeetle => PhantomFusionEffectType.SlumberingBeetle,
            ThievingHopper => PhantomFusionEffectType.ThievingHopper,
            Exoskeleton => PhantomFusionEffectType.Exoskeleton,
            Myte => PhantomFusionEffectType.Myte,
            Ovicopter => PhantomFusionEffectType.Ovicopter,
            DecimillipedeSegment => PhantomFusionEffectType.DecimillipedeSegment,
            Entomancer => PhantomFusionEffectType.Entomancer,
            InfestedPrism => PhantomFusionEffectType.InfestedPrism,
            Crusher => PhantomFusionEffectType.Crusher,
            Rocket => PhantomFusionEffectType.Rocket,
            TheInsatiable => PhantomFusionEffectType.TheInsatiable,
            KnowledgeDemon => PhantomFusionEffectType.KnowledgeDemon,
            GlobeHead => PhantomFusionEffectType.GlobeHead,
            TurretOperator => PhantomFusionEffectType.TurretOperator,
            LivingShield => PhantomFusionEffectType.LivingShield,
            Axebot => PhantomFusionEffectType.Axebot,
            OwlMagistrate => PhantomFusionEffectType.OwlMagistrate,
            DevotedSculptor => PhantomFusionEffectType.DevotedSculptor,
            FrogKnight => PhantomFusionEffectType.FrogKnight,
            SlimedBerserker => PhantomFusionEffectType.SlimedBerserker,
            TheLost => PhantomFusionEffectType.TheLost,
            TheForgotten => PhantomFusionEffectType.TheForgotten,
            ScrollOfBiting => PhantomFusionEffectType.ScrollOfBiting,
            Fabricator => PhantomFusionEffectType.Fabricator,
            MechaKnight => PhantomFusionEffectType.MechaKnight,
            SoulNexus => PhantomFusionEffectType.SoulNexus,
            SpectralKnight => PhantomFusionEffectType.SpectralKnight,
            MagiKnight => PhantomFusionEffectType.MagiKnight,
            Queen => PhantomFusionEffectType.Queen,
            TestSubject => PhantomFusionEffectType.TestSubject,
            Aeonglass => PhantomFusionEffectType.Aeonglass,
            FakeMerchantMonster => PhantomFusionEffectType.FakeMerchant,
            FlatHand => PhantomFusionEffectType.FlatHand,
            _ => PhantomFusionEffectType.None
        };
    }

    private static Dictionary<PhantomFusionEffectType, EffectState> Parse(
        string effectData)
    {
        Dictionary<PhantomFusionEffectType, EffectState> effects = new();
        if (string.IsNullOrWhiteSpace(effectData))
        {
            return effects;
        }

        foreach (string item in effectData.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split(':');
            if (parts.Length is not (2 or 4) ||
                !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int typeValue) ||
                !Enum.IsDefined(
                    typeof(PhantomFusionEffectType),
                    typeValue))
            {
                continue;
            }

            PhantomFusionEffectType type =
                (PhantomFusionEffectType)typeValue;
            if (parts.Length == 4 &&
                int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int fusionCount) &&
                decimal.TryParse(
                    parts[2],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal inheritedPrimary) &&
                decimal.TryParse(
                    parts[3],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal inheritedSecondary))
            {
                EffectState state = new(
                    Math.Clamp(fusionCount, 0, 1_000_000),
                    Math.Clamp(inheritedPrimary, 0m, 1_000_000m),
                    Math.Clamp(inheritedSecondary, 0m, 1_000_000m));
                if (state.FusionCount > 0 ||
                    state.LegacyInheritedPrimary > 0m ||
                    state.LegacyInheritedSecondary > 0m)
                {
                    effects[type] = NormalizeState(type, state);
                }

                continue;
            }

            if (parts.Length == 2 &&
                decimal.TryParse(
                    parts[1],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal legacyStacks) &&
                legacyStacks > 0m)
            {
                effects[type] = NormalizeState(type, ConvertLegacyState(
                    type,
                    Math.Min(1_000_000m, legacyStacks)));
            }
        }

        return effects;
    }

    private static EffectState NormalizeState(
        PhantomFusionEffectType type,
        EffectState state)
    {
        if (type != PhantomFusionEffectType.ThievingHopper)
        {
            return state;
        }

        // Repeated reward grants become choices in one reward, including old saves.
        decimal primary = (state.FusionCount > 0 ? 1m : 0m) +
            state.LegacyInheritedPrimary;
        decimal overflow = Math.Max(0m, primary - 1m);
        return state with
        {
            LegacyInheritedPrimary = state.LegacyInheritedPrimary - overflow,
            LegacyInheritedSecondary = Math.Min(
                1_000_000m,
                state.LegacyInheritedSecondary + overflow)
        };
    }

    private static string Serialize(
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        return string.Join(',', effects
            .Where(pair => pair.Key != PhantomFusionEffectType.None &&
                (pair.Value.FusionCount > 0 ||
                 pair.Value.LegacyInheritedPrimary > 0m ||
                 pair.Value.LegacyInheritedSecondary > 0m))
            .OrderBy(pair => (int)pair.Key)
            .Select(pair =>
                string.Join(
                    ':',
                    ((int)pair.Key).ToString(CultureInfo.InvariantCulture),
                    pair.Value.FusionCount.ToString(
                        CultureInfo.InvariantCulture),
                    FormatSerialized(pair.Value.LegacyInheritedPrimary),
                    FormatSerialized(pair.Value.LegacyInheritedSecondary))));
    }

    private static EffectState GetState(
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        PhantomFusionEffectType type)
    {
        return effects.TryGetValue(type, out EffectState state)
            ? state
            : default;
    }

    private static decimal GetEffect(
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        PhantomFusionEffectType type,
        decimal baseValue,
        decimal additionalValue)
    {
        return CalculateEffect(
            GetState(effects, type),
            baseValue,
            additionalValue,
            useSecondary: false);
    }

    private static decimal GetSecondaryEffect(
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        PhantomFusionEffectType type,
        decimal baseValue,
        decimal additionalValue)
    {
        return CalculateEffect(
            GetState(effects, type),
            baseValue,
            additionalValue,
            useSecondary: true);
    }

    private static decimal CalculateEffect(
        EffectState state,
        decimal baseValue,
        decimal additionalValue,
        bool useSecondary)
    {
        decimal inherited = useSecondary
            ? state.LegacyInheritedSecondary
            : state.LegacyInheritedPrimary;
        int fusionCount = Math.Max(0, state.FusionCount);
        decimal countedEffect = fusionCount <= 0
            ? 0m
            : baseValue + (fusionCount - 1m) * additionalValue;
        return Math.Max(0m, countedEffect + Math.Max(0m, inherited));
    }

    private static EffectState ConvertLegacyState(
        PhantomFusionEffectType type,
        decimal stacks)
    {
        int directCount = Math.Clamp(
            (int)Math.Floor(stacks),
            0,
            1_000_000);
        decimal fraction = Math.Max(0m, stacks - directCount);
        EffectDefinition definition = GetDefinition(type);
        decimal inheritedPrimary = directCount == 0
            ? definition.PrimaryBase * fraction
            : definition.PrimaryAdditional * fraction;
        decimal inheritedSecondary = directCount == 0
            ? definition.SecondaryBase * fraction
            : definition.SecondaryAdditional * fraction;
        return new EffectState(
            directCount,
            inheritedPrimary,
            inheritedSecondary);
    }

    private static EffectDefinition GetDefinition(
        PhantomFusionEffectType type)
    {
        return type switch
        {
            PhantomFusionEffectType.Flyconid => new(2m, 1m),
            PhantomFusionEffectType.BruteRubyRaider => new(1m, 1m),
            PhantomFusionEffectType.AssassinRubyRaider => new(5m, 2m),
            PhantomFusionEffectType.AxeRubyRaider => new(3m, 1m),
            PhantomFusionEffectType.CrossbowRubyRaider => new(5m, 2m),
            PhantomFusionEffectType.TrackerRubyRaider => new(1m, 1m),
            PhantomFusionEffectType.Fogmog => new(1m, 1m),
            PhantomFusionEffectType.Mawler => new(1m, 1m),
            PhantomFusionEffectType.FuzzyWurmCrawler => new(5m, 2m),
            PhantomFusionEffectType.Inklet => new(1m, 1m),
            PhantomFusionEffectType.SnappingJaxfruit => new(2m, 1m),
            PhantomFusionEffectType.SlitheringStrangler => new(5m, 2m),
            PhantomFusionEffectType.LeafSlimeS => new(2m, 1m),
            PhantomFusionEffectType.LeafSlimeM => new(3m, 2m),
            PhantomFusionEffectType.TwigSlimeS => new(2m, 1m, 1m, 0m),
            PhantomFusionEffectType.TwigSlimeM => new(2m, 1m, 1m, 0m),
            PhantomFusionEffectType.ShrinkerBeetle => new(2m, 1m),
            PhantomFusionEffectType.VineShambler => new(2m, 2m),
            PhantomFusionEffectType.Nibbit => new(3m, 1m),
            PhantomFusionEffectType.CubexConstruct => new(1m, 1m),
            PhantomFusionEffectType.Byrdonis => new(1m, 1m),
            PhantomFusionEffectType.BygoneEffigy => new(1m, 1m),
            PhantomFusionEffectType.PhrogParasite => new(6m, 3m),
            PhantomFusionEffectType.Vantom => new(3m, 2m),
            PhantomFusionEffectType.KinPriest => new(3m, 2m, 2m, 0m),
            PhantomFusionEffectType.CeremonialBeast => new(5m, 3m, 1m, 0m),
            PhantomFusionEffectType.CrestHopper => new(5m, 2m),
            PhantomFusionEffectType.GoldHopper => new(5m, 3m),
            PhantomFusionEffectType.HealHopper => new(3m, 1m),
            PhantomFusionEffectType.SmallHopper => new(1m, 1m),
            PhantomFusionEffectType.AbyssKnocker => new(5m, 3m),
            PhantomFusionEffectType.ClayKnocker => new(5m, 2m, 2m, 1m),
            PhantomFusionEffectType.Knocker => new(3m, 1m, 1m, 1m),
            PhantomFusionEffectType.TezukaSaki => new(0m, 10m, 3m, 0m),
            PhantomFusionEffectType.Toadpole => new(4m, 2m),
            PhantomFusionEffectType.DampCultist => new(3m, 1m),
            PhantomFusionEffectType.CalcifiedCultist => new(1m, 0m, 0m, 2m),
            PhantomFusionEffectType.GremlinMerc => new(3m, 1m, 3m, 0m),
            PhantomFusionEffectType.FatGremlin => new(5m, 3m),
            PhantomFusionEffectType.Seapunk => new(3m, 1m),
            PhantomFusionEffectType.FossilStalker => new(1m, 0m, 0m, 3m),
            PhantomFusionEffectType.LivingFog => new(1m, 1m),
            PhantomFusionEffectType.CorpseSlug => new(1m, 0m, 0m, 5m),
            PhantomFusionEffectType.TwoTailedRat => new(1m, 1m),
            PhantomFusionEffectType.SewerClam => new(4m, 2m),
            PhantomFusionEffectType.HauntedShip => new(2m, 1m, 3m, 1m),
            PhantomFusionEffectType.SludgeSpinner => new(1m, 1m, 1m, 0m),
            PhantomFusionEffectType.PunchConstruct => new(1m, 1m),
            PhantomFusionEffectType.SkulkingColony => new(60m, -10m),
            PhantomFusionEffectType.TerrorEel => new(99m, 0m, 0m, 5m),
            PhantomFusionEffectType.PhantasmalGardener => new(6m, 3m),
            PhantomFusionEffectType.LagavulinMatriarch => new(8m, 4m),
            PhantomFusionEffectType.SoulFysh => new(2m, 1m),
            PhantomFusionEffectType.WaterfallGiant => new(2m, 1m),
            PhantomFusionEffectType.Tunneler => new(16m, 8m),
            PhantomFusionEffectType.SpinyToad => new(8m, 4m),
            PhantomFusionEffectType.Chomper => new(2m, 2m),
            PhantomFusionEffectType.HunterKiller => new(1m, 1m),
            PhantomFusionEffectType.TheObscura => new(1m, 2m),
            PhantomFusionEffectType.BowlbugEgg => new(0m, 2m, 1m, 0m),
            PhantomFusionEffectType.BowlbugNectar => new(1m, 1m, 8m, 0m),
            PhantomFusionEffectType.BowlbugRock => new(1m, 1m),
            PhantomFusionEffectType.BowlbugSilk => new(1m, 1m),
            PhantomFusionEffectType.LouseProgenitor => new(10m, 5m),
            PhantomFusionEffectType.SlumberingBeetle => new(10m, 5m),
            PhantomFusionEffectType.ThievingHopper => new(1m, 0m, 0m, 1m),
            PhantomFusionEffectType.Exoskeleton => new(30m, -5m),
            PhantomFusionEffectType.Myte => new(3m, 1m),
            PhantomFusionEffectType.Ovicopter => new(1m, 2m, 3m, 0m),
            PhantomFusionEffectType.DecimillipedeSegment => new(6m, 3m),
            PhantomFusionEffectType.Entomancer => new(1m, 1m),
            PhantomFusionEffectType.InfestedPrism => new(2m, 1m),
            PhantomFusionEffectType.Crusher => new(25m, 15m),
            PhantomFusionEffectType.Rocket => new(20m, 10m),
            PhantomFusionEffectType.TheInsatiable => new(8m, 0m, 0m, 80m),
            PhantomFusionEffectType.KnowledgeDemon => new(12m, 0m, 1m, 1m),
            PhantomFusionEffectType.GlobeHead => new(1m, 1m),
            PhantomFusionEffectType.TurretOperator => new(1m, 1m, 30m, 10m),
            PhantomFusionEffectType.LivingShield => new(10m, 0m, 0m, 5m),
            PhantomFusionEffectType.Axebot => new(1m, 3m),
            PhantomFusionEffectType.OwlMagistrate => new(1m, 0m, 0m, 3m),
            PhantomFusionEffectType.DevotedSculptor => new(4m, 0m, 0m, 3m),
            PhantomFusionEffectType.FrogKnight => new(12m, 6m),
            PhantomFusionEffectType.SlimedBerserker => new(8m, 0m, 15m, 8m),
            PhantomFusionEffectType.TheLost => new(4m, 1m, 2m, 1m),
            PhantomFusionEffectType.TheForgotten => new(1m, 0m, 0m, 1m),
            PhantomFusionEffectType.ScrollOfBiting => new(1m, 0m, 0m, 1m),
            PhantomFusionEffectType.Fabricator => new(1m, 1m, 2m, 0m),
            PhantomFusionEffectType.MechaKnight => new(3m, 0m, 0m, 10m),
            PhantomFusionEffectType.SoulNexus => new(1m, 0m, 0m, 10m),
            PhantomFusionEffectType.FlailKnight => new(2m, 0m, 0m, 6m),
            PhantomFusionEffectType.SpectralKnight => new(1m, 1m),
            PhantomFusionEffectType.MagiKnight => new(1m, 1m, -12m, 6m),
            PhantomFusionEffectType.Queen => new(1m, 5m),
            PhantomFusionEffectType.TestSubject => new(1m, 0m, 0m, 5m),
            PhantomFusionEffectType.Aeonglass => new(1m, 1m),
            PhantomFusionEffectType.MysteriousKnight => new(2m, 1m, 6m, 3m),
            PhantomFusionEffectType.FakeMerchant => new(20m, 15m),
            PhantomFusionEffectType.FlatHand => new(1m, 1m),
            _ => default
        };
    }

    private static string FormatSerialized(decimal value)
    {
        return value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
    }

    private static void AddAmountDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        PhantomFusionEffectType type,
        decimal baseValue,
        decimal additionalValue,
        Func<decimal, string> createDescription)
    {
        decimal value = GetEffect(
            effects,
            type,
            baseValue,
            additionalValue);
        if (ToInt(value) > 0 || value % 1m != 0m)
        {
            descriptions.Add(
                $"{GetEffectTitle(type)}：{createDescription(value)}");
        }
    }

    private static void AddDualAmountDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        PhantomFusionEffectType type,
        decimal primaryBase,
        decimal primaryAdditional,
        decimal secondaryBase,
        decimal secondaryAdditional,
        Func<decimal, decimal, string> createDescription)
    {
        decimal primary = GetEffect(
            effects,
            type,
            primaryBase,
            primaryAdditional);
        decimal secondary = GetSecondaryEffect(
            effects,
            type,
            secondaryBase,
            secondaryAdditional);
        if (primary > 0m || secondary > 0m)
        {
            descriptions.Add(
                $"{GetEffectTitle(type)}：{createDescription(primary, secondary)}");
        }
    }

    private static void AddSkulkingColonyDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        if (!effects.ContainsKey(PhantomFusionEffectType.SkulkingColony)) return;

        decimal percent = GetEffect(effects, PhantomFusionEffectType.SkulkingColony, 60m, -10m);
        descriptions.Add(percent > 0m
            ? $"鬼祟珊瑚群：被召唤时，获得等同于最大生命值{Format(percent)}%的硬化外壳（最低1层）。"
            : "鬼祟珊瑚群：被召唤时，获得1层硬化外壳（每回合最多失去1点生命值）。");
    }

    private static void AddExoskeletonDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        // The percentage can reach zero while the summon effect still grants
        // its minimum of one layer. Keep that effect visible in fusion choices.
        if (!effects.ContainsKey(PhantomFusionEffectType.Exoskeleton))
        {
            return;
        }

        decimal percent = GetEffect(
            effects,
            PhantomFusionEffectType.Exoskeleton,
            30m,
            -5m);
        descriptions.Add(percent > 0m
            ? $"外骨骼虫：被召唤时，获得等同于最大生命值{Format(percent)}%的难以杀灭（最低1层）。"
            : "外骨骼虫：被召唤时，获得1层难以杀灭。");
    }

    private static void AddSludgeSpinnerDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        decimal potency = GetEffect(
            effects,
            PhantomFusionEffectType.SludgeSpinner,
            1m,
            1m);
        if (potency > 0m)
        {
            int interval = Math.Max(1, 5 - ToInt(potency));
            descriptions.Add(
                $"淤泥旋螺：每第{interval}次行动，给予敌方全体1层虚弱和1层脆弱。");
        }
    }

    private static void AddTwigDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        PhantomFusionEffectType type,
        Creature resourceOwner)
    {
        decimal hp = GetSecondaryEffect(effects, type, 1m, 0m);
        decimal damage = GetEffect(effects, type, 2m, 1m);
        if (hp > 0m || damage > 0m)
        {
            descriptions.Add(
                $"{GetEffectTitle(type)}：最大生命值+{Format(HbrResourceValueHelper.Fixed(resourceOwner, hp))}，伤害增幅+{Format(damage)}%。");
        }
    }

    private static void AddKinPriestDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        int followerCount = ToInt(GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.KinPriest,
            2m,
            0m));
        int followerLevel = ToInt(GetEffect(
            effects,
            PhantomFusionEffectType.KinPriest,
            3m,
            2m));
        if (followerCount > 0 && followerLevel > 0)
        {
            descriptions.Add(
                $"同族神官：被召唤时，召唤{followerCount}只同族信徒Lv.{followerLevel}（幻影），其拥有爪牙。");
        }
    }

    private static void AddCeremonialBeastDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        int strength = ToInt(GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.CeremonialBeast,
            1m,
            0m));
        decimal damage = GetEffect(
            effects,
            PhantomFusionEffectType.CeremonialBeast,
            5m,
            3m);
        if (strength > 0 || damage > 0m)
        {
            descriptions.Add(
                $"仪式兽：生命值高于一半时获得{strength}点力量；每次攻击意图后伤害增幅+{Format(damage)}%。");
        }
    }

    private static void AddClayKnockerDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        Creature resourceOwner)
    {
        decimal damage = GetEffect(
            effects,
            PhantomFusionEffectType.ClayKnocker,
            5m,
            2m);
        decimal dp = GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.ClayKnocker,
            2m,
            1m);
        if (damage > 0m || dp > 0m)
        {
            descriptions.Add(
                $"ClayKnocker：伤害增幅+{Format(damage)}%，最大偏导护盾+{Format(HbrResourceValueHelper.Fixed(resourceOwner, dp))}。");
        }
    }

    private static void AddKnockerDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        Creature resourceOwner)
    {
        decimal damage = GetEffect(
            effects,
            PhantomFusionEffectType.Knocker,
            3m,
            1m);
        decimal dp = GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.Knocker,
            1m,
            1m);
        if (damage > 0m || dp > 0m)
        {
            descriptions.Add(
                $"Knocker：伤害增幅+{Format(damage)}%，最大偏导护盾+{Format(HbrResourceValueHelper.Fixed(resourceOwner, dp))}。");
        }
    }

    private static void AddTezukaDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        Creature resourceOwner)
    {
        int strength = ToInt(GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.TezukaSaki,
            3m,
            0m));
        decimal dp = GetEffect(
            effects,
            PhantomFusionEffectType.TezukaSaki,
            0m,
            10m);
        if (strength > 0 || dp > 0m)
        {
            string description = $"偏导护盾归零后，获得{strength}点力量";
            if (dp > 0m)
            {
                description += $"；最大偏导护盾+{Format(HbrResourceValueHelper.Fixed(resourceOwner, dp))}";
            }

            descriptions.Add("手冢咲：" + description + "。");
        }
    }

    private static void AddOvicopterDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        int eggLevel = ToInt(GetEffect(
            effects,
            PhantomFusionEffectType.Ovicopter,
            1m,
            2m));
        int eggCount = ToInt(GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.Ovicopter,
            3m,
            0m));
        if (eggLevel > 0 && eggCount > 0)
        {
            descriptions.Add(
                $"直飞产卵虫：被召唤时，召唤{eggCount}只结实的卵Lv.{eggLevel}（幻影），其拥有爪牙。");
        }
    }

    private static void AddKnowledgeDemonDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        decimal dissolve = GetEffect(
            effects,
            PhantomFusionEffectType.KnowledgeDemon,
            12m,
            0m);
        int intervalReduction = ToInt(GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.KnowledgeDemon,
            1m,
            1m));
        if (dissolve > 0m)
        {
            descriptions.Add(
                $"知识恶魔：每第{Math.Max(1, 5 - intervalReduction)}次行动，给予敌方全体{Format(dissolve)}点瓦解。");
        }
    }

    private static void AddOwlMagistrateDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        decimal soar = GetEffect(
            effects,
            PhantomFusionEffectType.OwlMagistrate,
            1m,
            0m);
        decimal reduction = GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.OwlMagistrate,
            0m,
            3m);
        if (soar > 0m || reduction > 0m)
        {
            string description = "生命值低于25%时，获得翱翔";
            if (reduction > 0m)
            {
                description += $"；伤害减免+{Format(reduction)}%";
            }

            descriptions.Add("猫头鹰法官：" + description + "。");
        }
    }

    private static void AddForgottenDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        decimal dexterityScaling = GetEffect(
            effects,
            PhantomFusionEffectType.TheForgotten,
            1m,
            0m);
        decimal dexterity = GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.TheForgotten,
            0m,
            1m);
        if (dexterityScaling > 0m || dexterity > 0m)
        {
            string description =
                "攻击意图额外计算玩家的敏捷层数";
            if (dexterity > 0m)
            {
                description += $"；被召唤时，玩家获得{Format(dexterity)}点敏捷";
            }

            descriptions.Add("遗忘之物：" + description + "。");
        }
    }

    private static void AddScrollOfBitingDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects,
        Creature resourceOwner)
    {
        decimal hpPerKill = GetEffect(
            effects,
            PhantomFusionEffectType.ScrollOfBiting,
            1m,
            0m);
        decimal extra = GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.ScrollOfBiting,
            0m,
            1m);
        if (hpPerKill > 0m || extra > 0m)
        {
            string description =
                $"每次斩杀时，最大生命值+{Format(HbrResourceValueHelper.Fixed(resourceOwner, hpPerKill))}";
            if (extra > 0m)
            {
                description +=
                    $"；立即获得{Format(HbrResourceValueHelper.Fixed(resourceOwner, extra))}点最大生命值和{Format(extra * 3m)}%伤害增幅";
            }

            descriptions.Add("咬人卷轴：" + description + "。");
        }
    }

    private static void AddFabricatorDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        int level = ToInt(GetEffect(
            effects,
            PhantomFusionEffectType.Fabricator,
            1m,
            1m));
        int count = ToInt(GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.Fabricator,
            2m,
            0m));
        if (level > 0 && count > 0)
        {
            descriptions.Add(
                $"组装师：被召唤时，从守护、噪音、戳刺和电击机器人中随机召唤{count}只Lv.{level}幻影，它们拥有爪牙。");
        }
    }

    private static void AddMagiKnightDescription(
        ICollection<string> descriptions,
        IReadOnlyDictionary<PhantomFusionEffectType, EffectState> effects)
    {
        int suppressionTier = ToInt(GetEffect(
            effects,
            PhantomFusionEffectType.MagiKnight,
            1m,
            1m));
        decimal damage = GetSecondaryEffect(
            effects,
            PhantomFusionEffectType.MagiKnight,
            -12m,
            6m);
        if (suppressionTier <= 0 && damage <= 0m)
        {
            return;
        }

        string description = suppressionTier >= 3
            ? "所有敌方失去进阶8、9的怪物强化及进阶10的高维外壳强化（不移除双重Boss）"
            : suppressionTier >= 2
                ? "所有敌方失去进阶9的怪物强化及进阶10的高维外壳强化（不移除双重Boss）"
                : "所有敌方失去进阶10的高维外壳强化（不移除双重Boss）";
        if (damage > 0m)
        {
            description += $"；伤害增幅+{Format(damage)}%";
        }

        descriptions.Add("魔法骑士：" + description + "。");
    }

    private static string GetEffectTitle(PhantomFusionEffectType type)
    {
        return type switch
        {
            PhantomFusionEffectType.Flyconid => "飞蝇菌子",
            PhantomFusionEffectType.BruteRubyRaider => "劫掠者暴徒",
            PhantomFusionEffectType.AssassinRubyRaider => "劫掠者刺客",
            PhantomFusionEffectType.AxeRubyRaider => "劫掠者斧手",
            PhantomFusionEffectType.CrossbowRubyRaider => "劫掠者弩手",
            PhantomFusionEffectType.TrackerRubyRaider => "劫掠者追踪手",
            PhantomFusionEffectType.Fogmog => "雾菇",
            PhantomFusionEffectType.Mawler => "蛮兽",
            PhantomFusionEffectType.FuzzyWurmCrawler => "毛绒伏地虫",
            PhantomFusionEffectType.Inklet => "墨宝",
            PhantomFusionEffectType.SnappingJaxfruit => "闪光贾克斯果",
            PhantomFusionEffectType.SlitheringStrangler => "蛇行扼杀者",
            PhantomFusionEffectType.LeafSlimeS => "树叶史莱姆（小）",
            PhantomFusionEffectType.LeafSlimeM => "树叶史莱姆（中）",
            PhantomFusionEffectType.TwigSlimeS => "树枝史莱姆（小）",
            PhantomFusionEffectType.TwigSlimeM => "树枝史莱姆（中）",
            PhantomFusionEffectType.ShrinkerBeetle => "缩小甲虫",
            PhantomFusionEffectType.VineShambler => "藤蔓蹒跚者",
            PhantomFusionEffectType.Nibbit => "小啃兽",
            PhantomFusionEffectType.CubexConstruct => "立柱构造体",
            PhantomFusionEffectType.Byrdonis => "多尼斯异鸟",
            PhantomFusionEffectType.BygoneEffigy => "旧日雕像",
            PhantomFusionEffectType.PhrogParasite => "异蛙寄生虫",
            PhantomFusionEffectType.Vantom => "墨影幻灵",
            PhantomFusionEffectType.KinPriest => "同族神官",
            PhantomFusionEffectType.CeremonialBeast => "仪式兽",
            PhantomFusionEffectType.CrestHopper => "CrestHopper",
            PhantomFusionEffectType.GoldHopper => "GoldHopper",
            PhantomFusionEffectType.HealHopper => "HealHopper",
            PhantomFusionEffectType.SmallHopper => "SmallHopper",
            PhantomFusionEffectType.AbyssKnocker => "AbyssKnocker",
            PhantomFusionEffectType.ClayKnocker => "ClayKnocker",
            PhantomFusionEffectType.Knocker => "Knocker",
            PhantomFusionEffectType.TezukaSaki => "手冢咲",
            PhantomFusionEffectType.Toadpole => "蟾蜍蝌蚪",
            PhantomFusionEffectType.DampCultist => "潮湿邪教徒",
            PhantomFusionEffectType.CalcifiedCultist => "钙化邪教徒",
            PhantomFusionEffectType.GremlinMerc => "地精佣兵",
            PhantomFusionEffectType.FatGremlin => "胖地精",
            PhantomFusionEffectType.Seapunk => "海洋混混",
            PhantomFusionEffectType.FossilStalker => "化石追踪者",
            PhantomFusionEffectType.LivingFog => "活雾",
            PhantomFusionEffectType.CorpseSlug => "噬尸蛞蝓",
            PhantomFusionEffectType.TwoTailedRat => "双尾鼠",
            PhantomFusionEffectType.SewerClam => "下水道蚌",
            PhantomFusionEffectType.HauntedShip => "幽灵船",
            PhantomFusionEffectType.SludgeSpinner => "淤泥旋螺",
            PhantomFusionEffectType.PunchConstruct => "拳击构装体",
            PhantomFusionEffectType.SkulkingColony => "鬼祟珊瑚群",
            PhantomFusionEffectType.TerrorEel => "骇鳗",
            PhantomFusionEffectType.PhantasmalGardener => "花园幽灵鳗",
            PhantomFusionEffectType.LagavulinMatriarch => "乐加维林族母",
            PhantomFusionEffectType.SoulFysh => "灵魂异鱼",
            PhantomFusionEffectType.WaterfallGiant => "瀑布巨兽",
            PhantomFusionEffectType.Tunneler => "地道虫",
            PhantomFusionEffectType.SpinyToad => "棘刺蟾蜍",
            PhantomFusionEffectType.Chomper => "啃咬机",
            PhantomFusionEffectType.HunterKiller => "猎人杀手",
            PhantomFusionEffectType.TheObscura => "胧光怪",
            PhantomFusionEffectType.BowlbugEgg => "盛碗虫（卵）",
            PhantomFusionEffectType.BowlbugNectar => "盛碗虫（蜜）",
            PhantomFusionEffectType.BowlbugRock => "盛碗虫（石）",
            PhantomFusionEffectType.BowlbugSilk => "盛碗虫（丝）",
            PhantomFusionEffectType.LouseProgenitor => "虱虫之祖",
            PhantomFusionEffectType.SlumberingBeetle => "熟睡甲虫",
            PhantomFusionEffectType.ThievingHopper => "偷窃草蜢",
            PhantomFusionEffectType.Exoskeleton => "外骨骼虫",
            PhantomFusionEffectType.Myte => "异螨",
            PhantomFusionEffectType.Ovicopter => "直飞产卵虫",
            PhantomFusionEffectType.DecimillipedeSegment => "残杀千足虫",
            PhantomFusionEffectType.Entomancer => "蜂群术士",
            PhantomFusionEffectType.InfestedPrism => "感染棱柱",
            PhantomFusionEffectType.Crusher => "碾碎爪",
            PhantomFusionEffectType.Rocket => "火箭",
            PhantomFusionEffectType.TheInsatiable => "无厌沙虫",
            PhantomFusionEffectType.KnowledgeDemon => "知识恶魔",
            PhantomFusionEffectType.GlobeHead => "电球头",
            PhantomFusionEffectType.TurretOperator => "高塔炮手",
            PhantomFusionEffectType.LivingShield => "活体盾",
            PhantomFusionEffectType.Axebot => "巨斧机器人",
            PhantomFusionEffectType.OwlMagistrate => "猫头鹰法官",
            PhantomFusionEffectType.DevotedSculptor => "虔诚雕刻师",
            PhantomFusionEffectType.FrogKnight => "青蛙骑士",
            PhantomFusionEffectType.SlimedBerserker => "史莱姆狂战士",
            PhantomFusionEffectType.TheLost => "失落之物",
            PhantomFusionEffectType.TheForgotten => "遗忘之物",
            PhantomFusionEffectType.ScrollOfBiting => "咬人卷轴",
            PhantomFusionEffectType.Fabricator => "组装师",
            PhantomFusionEffectType.MechaKnight => "机甲骑士",
            PhantomFusionEffectType.SoulNexus => "灵魂枢纽",
            PhantomFusionEffectType.FlailKnight => "连枷骑士",
            PhantomFusionEffectType.SpectralKnight => "幽灵骑士",
            PhantomFusionEffectType.MagiKnight => "魔法骑士",
            PhantomFusionEffectType.Queen => "女王",
            PhantomFusionEffectType.TestSubject => "实验体",
            PhantomFusionEffectType.Aeonglass => "永世沙漏",
            PhantomFusionEffectType.MysteriousKnight => "神秘骑士",
            PhantomFusionEffectType.FakeMerchant => "商人？？？",
            PhantomFusionEffectType.FlatHand => "FlatHand",
            _ => string.Empty
        };
    }

    private static string Format(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
