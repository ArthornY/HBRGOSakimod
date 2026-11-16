using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Capabilities;

public enum ThirtyOneFMember { YanagiMion, MaruyamaKanata, HanamuraShiki, MatsuokaChiroru, NatsumeInori, KurosawaMaki }

public interface IThirtyOneFMemberCard
{
    ThirtyOneFMember ThirtyOneFMember { get; }
}

internal static class ThirtyOneFKeywordRules
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
    { "柳美音", "丸山奏多", "华村诗纪", "松冈千吕瑠", "夏目祈", "黑泽真希" });
    internal static ThirtyOneFMember? GetMember(CardModel card) => card is IThirtyOneFMemberCard member &&
        (uint)member.ThirtyOneFMember < TotalKinds ? member.ThirtyOneFMember : null;
    internal static bool IsThirtyOneFCard(CardModel card) => GetMember(card).HasValue;
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

