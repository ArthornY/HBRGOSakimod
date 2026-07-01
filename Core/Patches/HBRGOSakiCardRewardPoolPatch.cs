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
            .Distinct();
    }

    internal static bool IsRewardCard(CardModel card, bool includeColorless = false)
    {
        if (card is MagicSwordLuckySevenHbrgoSaki or FeintHbrgoSaki)
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

        bool includeColorless = options.CardPools.Any(pool => pool is ColorlessCardPool);
        if (options.CustomCardPool == null)
        {
            return options.WithCardPools(GetRewardCardPools(includeColorless));
        }

        CardModel[] customCards = options.CustomCardPool
            .Where(card => IsRewardCard(card, includeColorless))
            .Distinct()
            .ToArray();
        if (customCards.Length == 0)
        {
            customCards = GetUnlockedRewardCards(player, includeColorless).ToArray();
        }

        CardRarityOddsType odds = UsesSingleRarity(customCards) ? CardRarityOddsType.Uniform : options.RarityOdds;
        return options.WithCustomPool(customCards, odds);
    }

    private static bool UsesSingleRarity(IReadOnlyList<CardModel> cards)
    {
        return cards.Count > 0 && cards.All(card => card.Rarity == cards[0].Rarity);
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
            __result = originalCards.Distinct();
            return;
        }

        __result = originalCards
            .Concat(HBRGOSakiCardRewardPoolHelper.GetUnlockedRewardCards(player))
            .Where(card => HBRGOSakiCardRewardPoolHelper.IsRewardCard(card))
            .Distinct();
    }
}
