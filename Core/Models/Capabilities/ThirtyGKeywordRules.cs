using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public enum ThirtyGMember { ShirakawaYuina, KiryuMiya, TsukishiroMonaka, SugawaraChie, OgasawaraHisame, KuraSatomi }

public interface IThirtyGMemberCard
{
    ThirtyGMember ThirtyGMember { get; }
}

internal static class ThirtyGKeywordRules
{
    private sealed class DeckCount
    {
        internal bool Dirty = true;
        internal int Cards = -1;
        internal uint Members;
        internal Player Owner;
    }
    private static readonly ConditionalWeakTable<CardPile, DeckCount> DeckCounts = new();
    internal const int TotalKinds = 6;
    internal static IReadOnlyList<string> MemberNames { get; } = Array.AsReadOnly(new[]
    { "白河由依奈", "桐生美也", "月城最中", "菅原千惠", "小笠原绯雨", "藏里见" });
    internal static ThirtyGMember? GetMember(CardModel card) => card is IThirtyGMemberCard member &&
        (uint)member.ThirtyGMember < TotalKinds ? member.ThirtyGMember : null;
    internal static bool IsThirtyGCard(CardModel card) => GetMember(card).HasValue;
    internal static int GetPermanentDistinctCount(Player player)
    {
        CardPile deck = player?.Deck;
        if (deck == null) return 0;
        DeckCount cache = DeckCounts.GetValue(deck, pile =>
        {
            DeckCount count = new();
            pile.ContentsChanged += () => count.Dirty = true;
            return count;
        });
        if (cache.Dirty || cache.Cards != deck.Cards.Count || !ReferenceEquals(cache.Owner, player))
        {
            uint members = 0;
            foreach (CardModel card in deck.Cards)
                if (ReferenceEquals(card.Owner, player) && !card.HasBeenRemovedFromState && GetMember(card) is { } member)
                    members |= 1u << (int)member;
            cache.Members = members;
            cache.Cards = deck.Cards.Count;
            cache.Owner = player;
            cache.Dirty = false;
        }
        return BitOperations.PopCount(cache.Members);
    }
}
