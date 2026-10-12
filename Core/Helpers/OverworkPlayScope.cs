using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// CardPlay has no source pile: the native wrapper moves the card into Play
// before BeforeCardPlayed, for both manual and automatic plays.
internal static class OverworkPlayScope
{
    internal sealed class Entry
    {
        internal Entry Previous;
        internal HashSet<OverworkHbrgoSaki> HeldOverwork;
    }

    private sealed class State { internal Entry Current; }
    private static readonly ConditionalWeakTable<CardModel, State> States = new();

    internal static Entry Begin(CardModel card)
    {
        State state = States.GetOrCreateValue(card);
        Entry entry = new()
        {
            Previous = state.Current,
            HeldOverwork = card.Pile?.Type == PileType.Hand
                ? card.Owner.PlayerCombatState.Hand.Cards
                    .OfType<OverworkHbrgoSaki>()
                    .Where(overwork => !ReferenceEquals(overwork, card))
                    .ToHashSet()
                : new()
        };
        state.Current = entry;
        return entry;
    }

    internal static bool WasHeldForPlay(OverworkHbrgoSaki overwork, CardPlay play) =>
        play?.IsFirstInSeries == true &&
        States.TryGetValue(play.Card, out State state) &&
        state.Current?.HeldOverwork.Contains(overwork) == true;

    internal static void End(CardModel card, Entry entry)
    {
        if (States.TryGetValue(card, out State state) && ReferenceEquals(state.Current, entry))
            state.Current = entry.Previous;
    }
}
