using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
namespace HBRGOSakiMod.Core.Helpers;
// Normal and OD combat copies share their permanent physical origin, while
// generated/copy cards retain their own identity. Nothing survives this combat.
internal static class ThirtyOneBPhysicalCardState
{
    private sealed class State { internal decimal Merit; internal bool Played; }
    private sealed class Battle { internal readonly ConditionalWeakTable<CardModel, State> Cards = new(); }
    private static readonly ConditionalWeakTable<ICombatState, Battle> Battles = new();
    private static State Get(CardModel card)
    {
        if (card?.IsMutable != true || card.CombatState is not { } combat) return null;
        CardModel physical = !card.IsDupe && !CardCopyContextHelper.IsMarkedCopy(card) ? card.DeckVersion ?? card : card;
        return Battles.GetOrCreateValue(combat).Cards.GetOrCreateValue(physical);
    }
    internal static decimal GetMerit(CardModel card) => Get(card)?.Merit ?? 0m;
    internal static void AddMerit(CardModel card, decimal amount) { if (Get(card) is { } state) state.Merit += amount; }
    internal static bool HasPlayed(CardModel card) => Get(card)?.Played == true;
    internal static void MarkPlayed(CardModel card) { if (Get(card) is { } state) state.Played = true; }
    internal static void Reset(CardModel card) { if (Get(card) is { } state) { state.Played = false; state.Merit = 0; } }
}
