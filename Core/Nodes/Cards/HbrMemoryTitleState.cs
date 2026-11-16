using System;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Nodes.Cards;

// How much of a memory garden card's name has come back (HbrMemoryTitleReveal). State is kept per card model, never
// per card node: NCard nodes come from the native node pool and move on to other cards, models do not.
// The rule covers card faces and the garden's own labels during a run. Out of a run (the compendium) names show in
// full; hover tips and logs that quote a card name are left alone.
internal static class HbrMemoryTitleState
{
    private static readonly ConditionalWeakTable<CardModel, object> Unowned = new();
    private static readonly ConditionalWeakTable<CardModel, StrongBox<int>> Held = new();
    private static readonly object Marker = new();

    // Garden previews of cards not obtained yet: none of the name has come back.
    internal static void MarkUnowned(CardModel card)
    {
        if (card != null) Unowned.AddOrUpdate(card, Marker);
    }

    internal static void MarkOwned(CardModel card)
    {
        if (card != null) Unowned.Remove(card);
    }

    // A recollection preview reads as before the upgrade until it is confirmed.
    internal static void Hold(CardModel card, int upgradeLevel)
    {
        if (card != null) Held.AddOrUpdate(card, new StrongBox<int>(upgradeLevel));
    }

    internal static void Release(CardModel card)
    {
        if (card != null) Held.Remove(card);
    }

    internal static bool Applies(CardModel card) =>
        card != null && HbrMemoryGarden.IsGardenCard(card) && RunManager.Instance.IsInProgress;

    // Canonical cards (characters not met yet) and previews of cards not obtained yet have earned nothing.
    internal static int Parts(CardModel card)
    {
        if (!card.IsMutable || Unowned.TryGetValue(card, out _)) return 0;
        int level = Held.TryGetValue(card, out StrongBox<int> held) ? held.Value : card.CurrentUpgradeLevel;
        return HbrMemoryTitleReveal.OwnedParts(level, card.MaxUpgradeLevel);
    }

    internal static int TotalParts(CardModel card) => HbrMemoryTitleReveal.TotalParts(card.MaxUpgradeLevel);

    // The name without the native "+N" that upgraded cards append.
    internal static string Name(CardModel card)
    {
        string full = card.Title ?? string.Empty;
        string name = card.TitleLocString.GetFormattedText() ?? string.Empty;
        return name.Length > 0 && full.StartsWith(name, StringComparison.Ordinal) ? name : full;
    }

    // The title (with the native "+N" after upgrades) as it reads now; the plain title where the rule does not apply.
    internal static string Title(CardModel card, long jumpStep = -1, int decodeFromParts = -1,
        double decodeElapsed = double.MaxValue)
    {
        string full = card?.Title ?? string.Empty;
        if (!Applies(card)) return full;
        string name = Name(card);
        string garbled = HbrMemoryTitleReveal.Garble(card.Id.Entry, name, Parts(card), TotalParts(card), jumpStep,
            decodeFromParts, decodeElapsed);
        return garbled + full[name.Length..];
    }

    // Characters that come back going from `fromParts` to what the card has earned now.
    internal static int DecodeCount(CardModel card, int fromParts) =>
        Applies(card) ? HbrMemoryTitleReveal.DecodeCount(Name(card), card.Id.Entry, fromParts, Parts(card), TotalParts(card)) : 0;
}
