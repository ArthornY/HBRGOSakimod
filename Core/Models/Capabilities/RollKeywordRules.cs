using System;
using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal static class RollKeywordRules
{
    internal const int RollMinimum = 1;
    internal const int RollMaximum = 100;
    internal const int RollExclusiveMaximum = RollMaximum + 1;

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

        // Every card in this mod's pool owns Roll regardless of who is
        // currently controlling it. This also covers cards obtained by a
        // different character through rewards, shops, or copying effects.
        if (card.Pool is HBRGOSakiCardPool)
        {
            return true;
        }

        return card.Owner != null && IsRollEnabled(card.Owner);
    }

    internal static bool IsRollEnabled(Player player)
    {
        if (player?.Character is HbrgoSaki)
        {
            return true;
        }

        return player?.RunState?.Players?.Any(candidate =>
            candidate.Character is HbrgoSaki) == true;
    }

    internal static bool UsesSakiRules(CardModel card)
    {
        return card?.Owner?.Character is HbrgoSaki ||
               card?.Owner == null && card?.Pool is HBRGOSakiCardPool;
    }

    internal static bool UsesSakiRules(Player player)
    {
        return player?.Character is HbrgoSaki;
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
        if (IsCriticalRoll(player, roll))
        {
            return SoulRollTier.Critical;
        }

        int firstTierLowerBound = Math.Max(
            RollMinimum,
            50 - GetChargeRangeBonus(player));
        if (roll >= firstTierLowerBound && roll <= 75)
        {
            return SoulRollTier.First;
        }

        if (roll is >= 76 and <= 90)
        {
            return SoulRollTier.Second;
        }

        return roll > RollMaximum || roll is >= 91 and <= 99
            ? SoulRollTier.Third
            : SoulRollTier.None;
    }

    internal static bool IsCriticalRoll(Player player, int roll)
    {
        int criticalUpperBound =
            RollMaximum + GetChargeRangeBonus(player);
        return roll >= RollMaximum && roll <= criticalUpperBound;
    }

    internal static decimal GetAttackOrSkillValueMultiplier(
        Player player,
        int roll)
    {
        decimal bonus = GetRollTier(player, roll) switch
        {
            SoulRollTier.First => FirstTierValueBonus,
            SoulRollTier.Second => SecondTierValueBonus,
            SoulRollTier.Third => ThirdTierValueBonus,
            SoulRollTier.Critical => CriticalValueBonus,
            _ => 0m
        };
        return 1m + bonus * GetEffectMultiplier(player, roll);
    }

    internal static (int DrawAmount, int EnergyAmount) GetPowerCardEffect(
        Player player,
        int roll)
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
        if (roll <= 0 || IsCriticalRoll(player, roll))
        {
            return 0;
        }

        int sevenCount = roll.ToString().Count(digit => digit == '7');
        if (sevenCount > 0)
        {
            return sevenCount;
        }

        return roll % 7 == 0 &&
               player?.Creature?.HasPower<SwordheartClarityPower>() == true
            ? 1
            : 0;
    }

    private static int GetChargeRangeBonus(Player player)
    {
        int chargeAmount =
            player?.Creature?.GetPower<ChargePower>()?.Amount ?? 0;
        return Math.Max(0, chargeAmount) *
               ChargePower.RangeShiftPerStack;
    }
}
