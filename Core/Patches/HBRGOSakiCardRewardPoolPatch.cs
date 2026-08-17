using System.Collections.Generic;
using System.Linq;
using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

internal static class HBRGOSakiCardRewardPoolHelper
{
    internal static bool IsHBRGOSaki(Player player)
    {
        return player?.Character is HbrgoSaki;
    }

    internal static IEnumerable<CardPoolModel> GetRewardCardPools(bool includeColorless = false)
    {
        yield return ModelDb.CardPool<HBRGOSakiCardPool>();
        if (includeColorless)
        {
            yield return ModelDb.CardPool<ColorlessCardPool>();
        }
    }

    internal static IEnumerable<CardModel> GetUnlockedRewardCards(Player player, bool includeColorless = false)
    {
        return GetRewardCardPools(includeColorless)
            .SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
            .Where(card => IsRewardCard(card, includeColorless))
            .Where(card => CanOfferCard(player, card))
            .Distinct();
    }

    internal static bool IsRewardCard(CardModel card, bool includeColorless = false)
    {
        if (card is MagicSwordLuckySevenHbrgoSaki
            or MagicSwordModeHbrgoSaki
            or FeintHbrgoSaki
            or HighSongHbrgoSaki
            or ShadowDanceSongHbrgoSaki
            or BlueSkySongHbrgoSaki
            or LostDaysSongHbrgoSaki)
        {
            return false;
        }

        try
        {
            return card.Pool is HBRGOSakiCardPool || includeColorless && card.Pool is ColorlessCardPool;
        }
        catch (InvalidProgramException)
        {
            return false;
        }
    }

    internal static bool CanOfferCard(Player player, CardModel card)
    {
        if (card.Type != CardType.Power)
        {
            return true;
        }

        return player.Deck?.Cards?.Any(deckCard => deckCard.Id == card.Id) != true;
    }

    internal static CardCreationOptions RestrictRewardOptions(Player player, CardCreationOptions options)
    {
        if (!IsHBRGOSaki(player))
        {
            return options;
        }

        if (options.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications))
        {
            return options;
        }

        CardModel[] explicitCards = options.CustomCardPool?.ToArray();
        if (options.CardPools.Count > 0 &&
            options.CardPools.All(pool => pool is ColorlessCardPool))
        {
            return options;
        }

        if (explicitCards is { Length: > 0 } &&
            explicitCards.All(IsColorlessCard))
        {
            CardModel[] filteredColorlessCards = ApplyOriginalFilter(options, explicitCards).ToArray();
            CardRarityOddsType colorlessOdds = UsesSingleRarity(filteredColorlessCards)
                ? CardRarityOddsType.Uniform
                : options.RarityOdds;
            return options.WithCustomPool(filteredColorlessCards, colorlessOdds);
        }

        bool includeColorless =
            options.CardPools.Any(pool => pool is ColorlessCardPool) ||
            explicitCards?.Any(IsColorlessCard) == true;
        if (explicitCards == null)
        {
            CardModel[] rewardCards = ApplyOriginalFilter(
                    options,
                    GetUnlockedRewardCards(player, includeColorless))
                .ToArray();
            CardRarityOddsType rewardOdds = UsesSingleRarity(rewardCards) ? CardRarityOddsType.Uniform : options.RarityOdds;
            return options.WithCustomPool(rewardCards, rewardOdds);
        }

        CardModel[] customCards = explicitCards
            .Where(card => IsRewardCard(card, includeColorless))
            .Where(card => CanOfferCard(player, card))
            .Where(card => MatchesOriginalFilter(options, card))
            .Distinct()
            .ToArray();
        if (customCards.Length == 0)
        {
            customCards = ApplyOriginalFilter(
                    options,
                    GetUnlockedRewardCards(player, includeColorless))
                .ToArray();
        }

        CardRarityOddsType odds = UsesSingleRarity(customCards) ? CardRarityOddsType.Uniform : options.RarityOdds;
        return options.WithCustomPool(customCards, odds);
    }

    private static bool UsesSingleRarity(IReadOnlyList<CardModel> cards)
    {
        return cards.Count > 0 && cards.All(card => card.Rarity == cards[0].Rarity);
    }

    private static IEnumerable<CardModel> ApplyOriginalFilter(
        CardCreationOptions options,
        IEnumerable<CardModel> cards)
    {
        return cards.Where(card => MatchesOriginalFilter(options, card));
    }

    private static bool MatchesOriginalFilter(CardCreationOptions options, CardModel card)
    {
        return options.CardPoolFilter?.Invoke(card) != false;
    }

    private static bool IsColorlessCard(CardModel card)
    {
        try
        {
            return card.Pool is ColorlessCardPool;
        }
        catch (InvalidProgramException)
        {
            return false;
        }
    }
}

[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.ForRoom))]
internal static class CardCreationOptionsForRoomPatch
{
    private static void Postfix(Player player, RoomType roomType, ref CardCreationOptions __result)
    {
        if (!HBRGOSakiCardRewardPoolHelper.IsHBRGOSaki(player))
        {
            return;
        }

        __result = __result.WithCardPools(HBRGOSakiCardRewardPoolHelper.GetRewardCardPools());
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardRewardCreationOptions))]
internal static class HookModifyCardRewardCreationOptionsPatch
{
    private static void Postfix(Player player, ref CardCreationOptions __result)
    {
        __result = HBRGOSakiCardRewardPoolHelper.RestrictRewardOptions(player, __result);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMerchantCardPool))]
internal static class HookModifyMerchantCardPoolPatch
{
    private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
    {
        if (!HBRGOSakiCardRewardPoolHelper.IsHBRGOSaki(player))
        {
            return;
        }

        CardModel[] originalCards = __result.ToArray();
        if (originalCards.Length > 0 && originalCards.All(card => card.Pool is ColorlessCardPool))
        {
            __result = originalCards
                .Where(card => HBRGOSakiCardRewardPoolHelper.CanOfferCard(player, card))
                .Distinct();
            return;
        }

        __result = originalCards
            .Concat(HBRGOSakiCardRewardPoolHelper.GetUnlockedRewardCards(player))
            .Where(card => HBRGOSakiCardRewardPoolHelper.IsRewardCard(card))
            .Where(card => HBRGOSakiCardRewardPoolHelper.CanOfferCard(player, card))
            .Distinct();
    }
}
