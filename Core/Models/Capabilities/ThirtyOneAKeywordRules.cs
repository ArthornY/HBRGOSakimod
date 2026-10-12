using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal static class ThirtyOneAKeywordRules
{
    internal static bool IsThirtyOneACard(CardModel card) =>
        card is MyLegendBeginsHbrgoSaki or HelloWorldHbrgoSaki or SaviorGrandEntranceHbrgoSaki
            or WorldsFinestHbrgoSaki or MoreThanOneTruthHbrgoSaki or CrushYouLikeABugHbrgoSaki;

    internal static int GetPermanentDistinctCount(Player player) =>
        player?.Deck?.Cards.Where(card => ReferenceEquals(card.Owner, player) &&
                !card.HasBeenRemovedFromState && IsThirtyOneACard(card))
            .Select(card => card.Id).Distinct().Count() ?? 0;
}
