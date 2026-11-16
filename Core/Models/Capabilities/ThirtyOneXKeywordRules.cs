using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public enum ThirtyOneXMember { CarolReaper, LiYingxia, IreneRedmayne, VritikaBalakrishnan, MariaDeAngelis, CharlottaSkopovskaya }

public interface IThirtyOneXMemberCard
{
    ThirtyOneXMember ThirtyOneXMember { get; }
}

internal static class ThirtyOneXKeywordRules
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
    { "卡罗尔・利帕", "李映夏", "艾琳・雷德梅茵", "芙莉缇卡・巴拉克里希兰", "玛利亚・德・安洁莉斯", "夏洛塔・斯科波夫斯卡娅" });
    internal static ThirtyOneXMember? GetMember(CardModel card) => card is IThirtyOneXMemberCard member &&
        (uint)member.ThirtyOneXMember < TotalKinds ? member.ThirtyOneXMember : null;
    internal static bool IsThirtyOneXCard(CardModel card) => GetMember(card).HasValue;
    internal static int GetPermanentDistinctCount(Player player) => BitOperations.PopCount(GetPermanentMemberMask(player));
    internal static uint GetPermanentMemberMask(Player player)
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
        return cache.Members;
    }
}
