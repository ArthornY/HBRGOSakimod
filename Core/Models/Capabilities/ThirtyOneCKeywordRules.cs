using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public enum ThirtyOneCMember { YamawakiVonIvar, BungoYayoi, KanzakiAdelheid, AmaneMiko, SakurabaSeira, SatsukiMari }

// Future garden cards opt in explicitly; no current card or guessed title is
// silently assigned to31C, and no placeholder card is added to its pool.
public interface IThirtyOneCMemberCard
{
    ThirtyOneCMember ThirtyOneCMember { get; }
}

internal static class ThirtyOneCKeywordRules
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
    { "山胁・冯・伊瓦尔", "丰后弥生", "神崎阿迪尔海德", "天音巫呼", "樱庭星罗", "佐月麻里" });
    internal static ThirtyOneCMember? GetMember(CardModel card) => card is IThirtyOneCMemberCard member &&
        (uint)member.ThirtyOneCMember < TotalKinds ? member.ThirtyOneCMember : null;
    internal static bool IsThirtyOneCCard(CardModel card) => GetMember(card).HasValue;
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
