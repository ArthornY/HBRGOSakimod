using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public enum ThirtyOneDMember { NikaidoMisato, IshiiIroha, InochiFubuki, MurofushiRisa, DateAkari, MizuharaAina }

public interface IThirtyOneDMemberCard
{
    ThirtyOneDMember ThirtyOneDMember { get; }
}

internal static class ThirtyOneDKeywordRules
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
    { "二阶堂三乡", "石井色叶", "命吹雪", "室伏理沙", "伊达朱里", "瑞原蓝娜" });
    internal static ThirtyOneDMember? GetMember(CardModel card) => card is IThirtyOneDMemberCard member &&
        (uint)member.ThirtyOneDMember < TotalKinds ? member.ThirtyOneDMember : null;
    internal static bool IsThirtyOneDCard(CardModel card) => GetMember(card).HasValue;
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
