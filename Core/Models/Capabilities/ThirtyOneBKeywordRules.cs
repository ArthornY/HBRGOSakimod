using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal enum ThirtyOneBMember { AoiErika, MinaseIchigo, MinaseSumomo, HiguchiSeika, HiiragiKozue, Byakko }

internal static class ThirtyOneBKeywordRules
{
    internal const int TotalKinds = 6;
    internal static IReadOnlyList<string> MemberNames { get; } = new[]
    { "苍井绘里香", "水濑莓", "水濑李", "樋口圣华", "柊木梢", "白虎" };

    internal static ThirtyOneBMember? GetMember(CardModel card) => card switch
    {
        ByakkoRoarHbrgoSaki => ThirtyOneBMember.Byakko,
        PeacefulMemoriesHbrgoSaki => ThirtyOneBMember.AoiErika,
        HellfireBurnHbrgoSaki => ThirtyOneBMember.MinaseIchigo,
        FrozenMistMeowHbrgoSaki => ThirtyOneBMember.MinaseSumomo,
        FreshDeathHbrgoSaki => ThirtyOneBMember.HiguchiSeika,
        InnocentRequiemHbrgoSaki => ThirtyOneBMember.HiiragiKozue,
        _ => null
    };
    internal static bool IsThirtyOneBCard(CardModel card) => GetMember(card).HasValue;
    internal static int GetPermanentDistinctCount(Player player) => player?.Deck?.Cards
        .Where(card => ReferenceEquals(card.Owner, player) && !card.HasBeenRemovedFromState && IsThirtyOneBCard(card))
        .Select(GetMember).Distinct().Count() ?? 0;
}
