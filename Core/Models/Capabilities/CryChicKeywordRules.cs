using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal static class CryChicKeywordRules
{
    internal static readonly string KeywordId =
        ModContentRegistry.GetQualifiedKeywordId("HBRGOSakiMod", "CRYCHIC");

    internal static bool IsCryChicCard(CardModel card) => card is
        HalfBodyHbrgoSaki or HerSongIsMyScreamHbrgoSaki or
        LostStarSongHbrgoSaki or SoyoAidHbrgoSaki;

    internal static int GetPermanentDistinctCount(Player player) => player?.Deck?.Cards
        .Where(card => card.Owner == player && !card.HasBeenRemovedFromState && IsCryChicCard(card))
        .Select(card => card.GetType()).Distinct().Count() ?? 0;

    internal static IReadOnlyList<CardModel> GetCanonicalCards() => new CardModel[]
    {
        ModelDb.Card<HalfBodyHbrgoSaki>(),
        ModelDb.Card<HerSongIsMyScreamHbrgoSaki>(),
        ModelDb.Card<LostStarSongHbrgoSaki>(),
        ModelDb.Card<SoyoAidHbrgoSaki>()
    };
}
