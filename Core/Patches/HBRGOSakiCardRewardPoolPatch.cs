using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.CardPools;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

internal static class HBRGOSakiCardRewardPoolHelper
{
    internal const int MaximumRewardOptions = 64;

    internal static int GetExtraRewardOptionCount(int extraOptions)
    {
        return (int)Math.Clamp(3L + extraOptions, 1L, MaximumRewardOptions);
    }

    internal static int LimitRewardOptionCount(
        Player player, int requestedCount, CardCreationOptions options)
    {
        if (!IsHBRGOSaki(player) && !HasGardenCandidates(options))
        {
            return requestedCount;
        }

        // Restored rewards also pass here; never draw more distinct cards than exist.
        CardCreationOptions effectiveOptions = RestrictRewardOptions(player, options);
        bool multiplayer = player.RunState.Players.Count > 1;
        int availableCount = effectiveOptions.GetPossibleCards(player)
            .Where(card => effectiveOptions.RarityOdds == CardRarityOddsType.Uniform
                ? card.Rarity is not (CardRarity.Basic or CardRarity.Ancient)
                : card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .Where(card => card.MultiplayerConstraint != (multiplayer
                ? CardMultiplayerConstraint.SingleplayerOnly
                : CardMultiplayerConstraint.MultiplayerOnly))
            .Select(card => card.Id)
            .Distinct()
            .Take(MaximumRewardOptions)
            .Count();
        return Math.Clamp(requestedCount, 0, availableCount);
    }

    private static readonly System.Reflection.PropertyInfo
        CustomCardPoolProperty = AccessTools.Property(
            typeof(CardCreationOptions),
            "CustomCardPool");

    private static bool HasGardenCandidates(CardCreationOptions options) =>
        options.CardPools.Any(pool => pool.AllCards.Any(HbrMemoryGardenCardCatalog.IsGardenCard)) ||
        (CustomCardPoolProperty?.GetValue(options) as IEnumerable<CardModel>)?
            .Any(HbrMemoryGardenCardCatalog.IsGardenCard) == true;

    internal static bool IsHBRGOSaki(Player player)
    {
        return player?.Character is HbrgoSaki;
    }

    internal static IEnumerable<CardPoolModel> GetRewardCardPools(
        Player player,
        bool includeColorless = false)
    {
        yield return player.Character switch
        {
            HbrgoSaki30A => ModelDb.CardPool<HBRGOSaki30ACardPool>(),
            HbrgoSaki31O => ModelDb.CardPool<HBRGOSaki31OCardPool>(),
            _ => ModelDb.CardPool<HBRGOSakiCardPool>()
        };
        if (includeColorless)
        {
            yield return ModelDb.CardPool<ColorlessCardPool>();
        }
    }

    internal static IEnumerable<CardModel> GetUnlockedRewardCards(
        Player player,
        bool includeColorless = false,
        bool excludeOwnedPowers = true)
    {
        IEnumerable<CardModel> cards = player.Character is HbrgoSaki30A
            ? HbrgoSaki31OCardFilter.GetThirtyACards()
            : GetRewardCardPools(player)
                .SelectMany(pool => pool.GetUnlockedCards(
                    player.UnlockState,
                    player.RunState.CardMultiplayerConstraint));
        if (includeColorless)
        {
            cards = cards.Concat(
                ModelDb.CardPool<ColorlessCardPool>()
                    .GetUnlockedCards(
                        player.UnlockState,
                        player.RunState.CardMultiplayerConstraint));
        }

        cards = cards.Where(card =>
            IsRewardCard(player, card, includeColorless));
        if (excludeOwnedPowers)
        {
            cards = cards.Where(card => CanOfferCard(player, card));
        }

        return cards.Distinct();
    }

    internal static bool IsRewardCard(Player player, CardModel card, bool includeColorless = false)
    {
        if (card == null || HBRGOSakiContentPack.RetiredCardTypes.Contains(card.GetType())) return false;
        if (HbrMemoryGardenCardCatalog.IsGardenCard(card)) return false;

        if (!HbrgoSaki31OCardFilter.IsAvailableTo(
                player?.Character,
                card))
        {
            return false;
        }

        if (card is MagicSwordLuckySevenHbrgoSaki
            or MagicSwordLuckySevenOpeningHbrgoSaki
            or UnfetteredHbrgoSaki
            or MagicSwordModeHbrgoSaki
            or FeintHbrgoSaki
            or HighSongHbrgoSaki
            or ShadowDanceSongHbrgoSaki
            or BlueSkySongHbrgoSaki
            or LostDaysSongHbrgoSaki)
        {
            return false;
        }

        bool isCharacterCard = player.Character is HbrgoSaki30A
            ? HbrgoSaki31OCardFilter.IsThirtyAAllowed(card)
            : player.Character.CardPool.AllCardIds.Contains(card.Id);
        return isCharacterCard ||
            includeColorless && IsColorlessCard(card);
    }

    internal static bool CanOfferCard(Player player, CardModel card)
    {
        if (card == null || HBRGOSakiContentPack.RetiredCardTypes.Contains(card.GetType())) return false;
        if (HbrMemoryGardenCardCatalog.IsGardenCard(card)) return false;

        if (!HbrgoSaki31OCardFilter.IsAvailableTo(
                player?.Character,
                card))
        {
            return false;
        }

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

        CardModel[] explicitCards =
            (CustomCardPoolProperty?.GetValue(options) as
                IEnumerable<CardModel>)?.ToArray();
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
            return WithFilteredCards(
                player,
                options,
                filteredColorlessCards,
                includeColorless: true,
                colorlessOdds);
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
            return WithFilteredCards(
                player,
                options,
                rewardCards,
                includeColorless,
                rewardOdds);
        }

        CardModel[] customCards = explicitCards
            .Where(card => IsRewardCard(player, card, includeColorless))
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
        return WithFilteredCards(
            player,
            options,
            customCards,
            includeColorless,
            odds);
    }

    private static CardCreationOptions WithFilteredCards(
        Player player,
        CardCreationOptions options,
        IReadOnlyList<CardModel> cards,
        bool includeColorless,
        CardRarityOddsType rarityOdds)
    {
        CardModel[] allowedCards = cards.ToArray();
        bool onlyColorless = allowedCards.Length > 0 &&
            allowedCards.All(IsColorlessCard);
        CardPoolModel[] pools = onlyColorless
            ? [ModelDb.CardPool<ColorlessCardPool>()]
            : GetRewardCardPools(player, includeColorless).ToArray();
        Func<CardModel, bool> filter = card =>
            allowedCards.Any(allowed => allowed.Id == card.Id) &&
            MatchesOriginalFilter(options, card);
        return CardCreationOptionsCompatibility.Create(options, pools, rarityOdds, filter);
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

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward),
    new[] { typeof(Player), typeof(int), typeof(CardCreationOptions) })]
internal static class HbrgoSakiCardRewardOptionCountPatch
{
    private static void Prefix(Player player, ref int cardCount, CardCreationOptions options)
    {
        cardCount = HBRGOSakiCardRewardPoolHelper.LimitRewardOptionCount(
            player, cardCount, options);
    }
}

[HarmonyPatch(
    typeof(CardFactory),
    nameof(CardFactory.GetDefaultTransformationOptions))]
internal static class HbrgoSaki31OCardTransformationPatch
{
    private static void Postfix(
        CardModel original,
        bool isInCombat,
        ref IEnumerable<CardModel> __result)
    {
        CharacterModel character = original?.Owner?.Character;
        if (character is HbrgoSaki)
        {
            Player owner = original.Owner;
            IEnumerable<CardModel> cards = character is HbrgoSaki30A
                ? HbrgoSaki31OCardFilter.GetThirtyACards()
                : character.CardPool.GetUnlockedCards(
                    owner.UnlockState,
                    owner.RunState.CardMultiplayerConstraint);
            __result = cards
                .Where(card => card.Id != original.Id)
                .Where(card => card.Rarity != CardRarity.Ancient)
                .Where(card => HbrgoSaki31OCardFilter.IsAvailableTo(
                    character,
                    card));
        }

        if (!isInCombat || original?.Pile?.Type == PileType.Deck)
            __result = HbrMemoryGardenCardAcquisition.WithoutGardenCards(__result);
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

        __result = CardCreationOptionsCompatibility.WithCardPools(
            __result,
            HBRGOSakiCardRewardPoolHelper.GetRewardCardPools(player));
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
    [HarmonyPriority(Priority.Last - 2)]
    private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
    {
        __result = HbrMemoryGardenCardAcquisition.WithoutGardenCards(__result);

        if (!HBRGOSakiCardRewardPoolHelper.IsHBRGOSaki(player))
        {
            return;
        }

        CardModel[] originalCards = __result.ToArray();
        if (originalCards.Length > 0 && originalCards.All(card => card.Pool is ColorlessCardPool))
        {
            __result = originalCards.Distinct();
            return;
        }

        __result = originalCards
            .Concat(HBRGOSakiCardRewardPoolHelper.GetUnlockedRewardCards(
                player,
                excludeOwnedPowers: false))
            .Where(card => HBRGOSakiCardRewardPoolHelper.IsRewardCard(player, card))
            .Distinct();
    }
}
