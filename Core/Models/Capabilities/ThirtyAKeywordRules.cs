using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal static class ThirtyAKeywordRules
{
    internal const int TotalKinds = 5;

    internal static bool IsThirtyACard(CardModel card)
    {
        return card is HerSongIsMyScreamHbrgoSaki
            or MatchaParfaitHbrgoSaki
            or LostStarSongHbrgoSaki
            or AnonAidHbrgoSaki
            or SoyoAidHbrgoSaki;
    }

    internal static int GetPermanentDistinctCount(Player player)
    {
        return player?.Deck?.Cards
            .Where(card => card.Owner == player && !card.HasBeenRemovedFromState &&
                IsThirtyACard(card))
            .Select(card => card.GetType())
            .Distinct()
            .Count() ?? 0;
    }

    internal static bool HasAllPermanentCards(Player player) =>
        GetPermanentDistinctCount(player) == TotalKinds;

    internal static IReadOnlyList<CardModel> GetCanonicalCards()
    {
        return new CardModel[]
        {
            ModelDb.Card<HerSongIsMyScreamHbrgoSaki>(),
            ModelDb.Card<MatchaParfaitHbrgoSaki>(),
            ModelDb.Card<LostStarSongHbrgoSaki>(),
            ModelDb.Card<AnonAidHbrgoSaki>(),
            ModelDb.Card<SoyoAidHbrgoSaki>()
        };
    }
}
