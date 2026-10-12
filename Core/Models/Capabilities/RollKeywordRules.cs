using System;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards.Mocks;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal static class RollKeywordRules
{
    internal const int RollMinimum = 1;
    internal const int RollMaximum = 100;
    internal const int RollExclusiveMaximum = RollMaximum + 1;
    internal const int GuaranteedCriticalMinimum = 777;

    private const decimal FirstTierValueBonus = 0.15m;
    private const decimal SecondTierValueBonus = 0.20m;
    private const decimal ThirdTierValueBonus = 0.30m;
    private const decimal CriticalValueBonus = 0.50m;

    internal static bool IsSupportedCardType(CardModel card)
    {
        return card?.Type is CardType.Attack or CardType.Skill or CardType.Power;
    }

    internal static bool IsRollEnabled(CardModel card)
    {
        if (!IsSupportedCardType(card))
        {
            return false;
        }

        if (card is MockCardModel)
        {
            return false;
        }

        if (card is FateHbrgoSaki or HasteHbrgoSaki or
            RemoveDpHbrgoSaki or
            StrengthAmplificationHbrgoSaki or
            CommandStrikeHbrgoSaki or
            PhantomChoiceHbrgoSaki or
            HalfBodyHbrgoSaki or
            DistantPrayerHbrgoSaki or
            EcstaticCorridorHbrgoSaki)
        {
            return false;
        }

        if (card.IsMutable &&
            card.Owner?.Creature?.HasPower<FatePower>() == true)
        {
            return false;
        }

        // Every card in this mod's pool owns Roll regardless of who is
        // currently controlling it. This also covers cards obtained by a
        // different character through rewards, shops, or copying effects.
        if (HBRGOSakiContentPack.IsRegisteredCardType(card.GetType()) ||
            IsCommonCardPool(card))
        {
            return true;
        }

        return card.IsMutable &&
               card.Owner != null &&
               IsRollEnabled(card.Owner);
    }

    internal static bool IsRollEnabled(Player player)
    {
        if (player?.Creature?.HasPower<FatePower>() == true)
        {
            return false;
        }

        if (UsesCommonRules(player))
        {
            return true;
        }

        return player?.RunState?.Players?.Any(UsesCommonRules) == true;
    }

    internal static bool UsesCommonRules(Player player) =>
        player?.Character is HbrgoSaki or HbrgoHotaru;

    internal static bool UsesSakiRules(CardModel card)
    {
        if (card == null ||
            card is MockCardModel)
        {
            return false;
        }

        return card.IsMutable
            ? card.Owner?.Character is HbrgoSaki31O
            : HbrgoSaki31OCardFilter.IsThirtyOneOExclusive(card);
    }

    internal static bool UsesSakiRules(Player player)
    {
        return player?.Character is HbrgoSaki31O;
    }

    internal static bool IsCopiedCard(CardModel card)
    {
        return card?.IsDupe == true ||
               DualSwordsCopyHelper.IsDualSwordsCopy(card);
    }

    internal static bool IsMimicTechniqueCard(CardModel card)
    {
        return card is MimicCursedScytheHbrgoSaki
            or MimicDreamBubbleHbrgoSaki
            or MimicEvilEyeTidalSlashHbrgoSaki
            or MimicExorcismArrowHbrgoSaki
            or MimicFullBodyShotHbrgoSaki
            or MimicReminiscenceHbrgoSaki
            or MimicIceFlowerThousandArrowsHbrgoSaki
            or MimicMaidCultivationHbrgoSaki
            or MimicMoonlightHbrgoSaki
            or MimicSaviorStrikeHbrgoSaki
            or MimicSpecialMealHbrgoSaki
            or MimicThunderGaleHbrgoSaki
            or ExtraGaugeStrikeHbrgoSaki;
    }

    internal static SoulRollTier GetRollTier(Player player, int roll)
    {
        return GetRollTier(player, SoulRollBreakdown.FromFinalRoll(roll));
    }

    internal static SoulRollTier GetRollTier(Player player, SoulRollBreakdown roll)
    {
        if (roll == null)
        {
            return SoulRollTier.None;
        }

        if (roll.ResolvedTier is { } resolvedTier)
        {
            return resolvedTier;
        }

        if (roll.IsForcedCritical ||
            IsAbsoluteCriticalRoll(player, roll.FinalRoll) ||
            roll.BaseRoll >= GetCriticalMinimum(player) && roll.BaseRoll <= RollMaximum)
        {
            return SoulRollTier.Critical;
        }

        return GetNonCriticalTier(roll.FinalRoll);
    }

    private static SoulRollTier GetNonCriticalTier(int finalRoll)
    {
        if (finalRoll is >= 50 and <= 75)
        {
            return SoulRollTier.First;
        }

        if (finalRoll is >= 76 and <= 90)
        {
            return SoulRollTier.Second;
        }

        return finalRoll >= 91
            ? SoulRollTier.Third
            : SoulRollTier.None;
    }

    internal static SoulRollBreakdown WithoutCritical(SoulRollBreakdown roll) =>
        new(roll.BaseRoll, roll.FinalRoll, roll.DiceSource, roll.Bonuses)
        {
            IsForcedCritical = false,
            ResolvedTier = GetNonCriticalTier(roll.FinalRoll)
        };

    internal static SoulRollBreakdown ResolveRollTier(Player player, SoulRollBreakdown roll)
    {
        if (roll == null || roll.ResolvedTier.HasValue)
        {
            return roll;
        }

        return new SoulRollBreakdown(roll.BaseRoll, roll.FinalRoll, roll.DiceSource, roll.Bonuses)
        {
            IsForcedCritical = roll.IsForcedCritical,
            ResolvedTier = GetRollTier(player, roll)
        };
    }

    // An integer without a breakdown represents an unmodified roll.
    internal static bool IsCriticalRoll(Player player, int roll) =>
        IsCriticalRoll(player, SoulRollBreakdown.FromFinalRoll(roll));

    internal static bool IsCriticalRoll(Player player, SoulRollBreakdown roll) =>
        GetRollTier(player, roll) == SoulRollTier.Critical;

    internal static int GetCriticalMinimum(Player player) => (int)Math.Max(RollMinimum,
        (long)GetCriticalMinimumForCharge(player?.Creature?.GetPower<ChargePower>()?.Amount ?? 0) -
        SaviorGrandEntranceCombatHelper.GetRawCriticalMinimumReduction(player?.Creature));

    internal static int GetCriticalMinimumForCharge(int charge) => (int)Math.Max(
        RollMinimum, RollMaximum - (long)Math.Max(0, charge) * ChargePower.RangeShiftPerStack);

    internal static int GetAbsoluteCriticalMinimum(Player player) =>
        GetAbsoluteCriticalMinimumForCharge(player?.Creature?.GetPower<ChargePower>()?.Amount ?? 0);

    internal static int GetAbsoluteCriticalMinimumForCharge(int charge) => (int)Math.Max(
        RollMinimum, GuaranteedCriticalMinimum - (long)Math.Max(0, charge) * ChargePower.AbsoluteCriticalReductionPerStack);

    internal static bool IsAbsoluteCriticalRoll(Player player, int roll) =>
        roll >= GetAbsoluteCriticalMinimum(player);

    internal static decimal GetAttackOrSkillValueMultiplier(
        Player player,
        int roll)
    {
        return GetAttackOrSkillValueMultiplier(player, SoulRollBreakdown.FromFinalRoll(roll));
    }

    internal static decimal GetAttackOrSkillValueMultiplier(
        Player player,
        SoulRollBreakdown roll)
        => GetAttackOrSkillValueMultiplier(player, roll, CardType.Skill);

    internal static decimal GetAttackOrSkillValueMultiplier(
        Player player,
        SoulRollBreakdown roll,
        CardType cardType)
    {
        decimal bonus = GetRollTier(player, roll) switch
        {
            SoulRollTier.First => FirstTierValueBonus,
            SoulRollTier.Second => SecondTierValueBonus,
            SoulRollTier.Third => ThirdTierValueBonus,
            SoulRollTier.Critical => CriticalValueBonus,
            _ => 0m
        };
        return 1m + bonus * GetEffectMultiplier(player, roll) *
            MindsEyeCombatHelper.GetRollEffectMultiplier(player?.Creature, cardType);
    }

    internal static (int DrawAmount, int EnergyAmount) GetPowerCardEffect(
        Player player,
        int roll)
    {
        return GetPowerCardEffect(player, SoulRollBreakdown.FromFinalRoll(roll));
    }

    internal static (int DrawAmount, int EnergyAmount) GetPowerCardEffect(
        Player player,
        SoulRollBreakdown roll)
    {
        (int drawAmount, int energyAmount) =
            GetRollTier(player, roll) switch
            {
                SoulRollTier.First => (1, 0),
                SoulRollTier.Second => (1, 1),
                SoulRollTier.Third => (2, 1),
                SoulRollTier.Critical => (2, 2),
                _ => (0, 0)
            };
        int multiplier = GetEffectMultiplier(player, roll);
        return (drawAmount * multiplier, energyAmount * multiplier);
    }

    internal static int GetEffectMultiplier(Player player, int roll)
    {
        return GetEffectMultiplier(player, SoulRollBreakdown.FromFinalRoll(roll));
    }

    internal static int GetEffectMultiplier(Player player, SoulRollBreakdown roll)
    {
        if (player?.Creature == null)
        {
            return 1;
        }

        int multiplier =
            CrimsonBladeFlowPower.GetSoulOfAnchorEffectMultiplier(
                player.Creature);
        if (IsCriticalRoll(player, roll) &&
            player.Creature.HasPower<TinyMomentsPower>())
        {
            multiplier *= TinyMomentsPower.CriticalEffectMultiplier;
        }

        return Math.Max(1, multiplier);
    }

    internal static int GetRegularSwordheartAmount(Player player, int roll)
    {
        return GetRegularSwordheartAmount(player, SoulRollBreakdown.FromFinalRoll(roll));
    }

    internal static int GetRegularSwordheartAmount(Player player, SoulRollBreakdown roll)
    {
        if (player?.Character is not HbrgoSaki31O ||
            roll == null ||
            roll.FinalRoll <= 0 ||
            IsCriticalRoll(player, roll))
        {
            return 0;
        }

        int sevenCount = roll.FinalRoll.ToString().Count(digit => digit == '7');
        if (sevenCount > 0)
        {
            return sevenCount;
        }

        return roll.FinalRoll % 7 == 0 &&
               player?.Creature?.HasPower<SwordheartClarityPower>() == true
            ? 1
            : 0;
    }

    internal static bool UsesHotaruRules(CardModel card) =>
        card?.IsMutable == true && card.Owner != null
            ? card.Owner.Character is HbrgoHotaru
            : TryGetCardPool(card, out CardPoolModel pool) && pool is HbrgoHotaruCardPool;

    private static bool IsCommonCardPool(CardModel card)
    {
        return TryGetCardPool(card, out CardPoolModel pool) &&
               pool is HBRGOSakiCardPool or
                   HBRGOSaki30ACardPool or
                   HBRGOSaki31OCardPool or
                   HBRGOSakiMemoryGardenCardPool or
                   HbrgoHotaruCardPool;
    }

    private static bool TryGetCardPool(
        CardModel card,
        out CardPoolModel pool)
    {
        pool = null;
        if (card == null ||
            card is MockCardModel)
        {
            return false;
        }

        try
        {
            pool = card.Pool;
            return pool != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (InvalidProgramException)
        {
            return false;
        }
    }
}
