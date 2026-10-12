using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrMemoryGardenRestStory
{
    internal const string EventId = "THEBEGINNING_HBRGO_SAKI";
    // Keep the original bit positions of the three returning members. Bit 2
    // belonged to Rana; the new Mutsumi bit cannot be satisfied by old saves.
    private const int RequiredCards = 1 | 4 | 8 | 16;

    private sealed record BeginClaim(RestSiteRoom Room, ElectronicSoldierManual[] Manuals);
    private static readonly ConditionalWeakTable<IRunState, BeginClaim> PendingStarts = new();

    internal static void RecordPermanentAcquisition(Player player, CardModel card)
    {
        if (!HbrMemoryGarden.CanAccess(player) ||
            player.GetRelic<ElectronicSoldierManual>() is not { } manual ||
            card == null || !card.IsMutable || !ReferenceEquals(card.Owner, player) ||
            card.Pile?.Type != PileType.Deck)
            return;

        RefreshPermanentCards(player);
    }

    internal static void RefreshPermanentCards(Player player)
    {
        if (!HbrMemoryGarden.CanAccess(player) ||
            player.GetRelic<ElectronicSoldierManual>() is not { } manual)
            return;

        int owned = player.Deck.Cards.Aggregate(0, (mask, card) => mask | (card switch
        {
            HerSongIsMyScreamHbrgoSaki => 1,
            LostStarSongHbrgoSaki => 4,
            SoyoAidHbrgoSaki => 8,
            HalfBodyHbrgoSaki => 16,
            _ => 0
        }));

        bool complete = HasAllCryChicCards(player);
        bool previouslyComplete = (manual.MemoryGardenObtainedCardMask & RequiredCards) == RequiredCards;
        string readyAt = !complete ? string.Empty
            : !previouslyComplete || string.IsNullOrEmpty(manual.MemoryGardenRestStoryReadyAt)
                ? LocationKey(player.RunState)
                : manual.MemoryGardenRestStoryReadyAt;

        manual.SetMemoryGardenObtainedCards(owned, readyAt);
    }

    private static bool HasAllCryChicCards(Player player) =>
        player.GetRelic<CryChicCollection>() is { IsMelted: false } collection &&
        collection.DisplayAmount >= CryChicCollection.RequiredKinds;

    internal static void RestoreObtainedCards(IRunState runState)
    {
        if (runState == null) return;
        // Read the current permanent deck, including removals. A legacy readiness
        // marker based on the previous four-card collection must be earned again.
        foreach (Player player in runState.Players)
        {
            TeamCollectionRelics.Restore(player);
            RefreshPermanentCards(player);
        }
    }

    internal static bool TryBeginAtRestSite(IRunState runState, RestSiteRoom room)
    {
        if (runState == null || room == null || !ReferenceEquals(runState.CurrentRoom, room))
            return false;

        RestoreObtainedCards(runState);

        ElectronicSoldierManual[] manuals = runState.Players
            .Select(player => player.GetRelic<ElectronicSoldierManual>())
            .Where(manual => manual != null).ToArray();
        if (manuals.Any(manual => manual.MemoryGardenRestStorySeen) ||
            (runState is RunState state && state.VisitedEventIds.Any(id =>
                HbrStoryDefinition.NormalizeEventId(id.Entry) == EventId)))
            return false;

        string location = LocationKey(runState);
        bool eligible = runState.Players.Any(player =>
            HbrMemoryGarden.CanAccess(player) &&
            player.GetRelic<ElectronicSoldierManual>() is { } manual &&
            HasAllCryChicCards(player) &&
            !string.IsNullOrEmpty(manual.MemoryGardenRestStoryReadyAt) &&
            !string.Equals(manual.MemoryGardenRestStoryReadyAt, location, StringComparison.Ordinal));
        if (!eligible) return false;

        // Shared events play once for the run, including mixed 31O/30A parties.
        // The normal room-entry save precedes this claim. Quitting mid-story thus
        // restores both the claim and rewards to the same pre-room snapshot.
        PendingStarts.Remove(runState);
        PendingStarts.Add(runState, new BeginClaim(room, manuals));
        foreach (ElectronicSoldierManual manual in manuals)
            manual.SetMemoryGardenRestStorySeen(true);
        return true;
    }

    internal static void ConfirmBeginAtRestSite(IRunState runState, RestSiteRoom room)
    {
        if (runState != null && PendingStarts.TryGetValue(runState, out BeginClaim claim) &&
            ReferenceEquals(claim.Room, room))
            PendingStarts.Remove(runState);
    }

    internal static void CancelBeginAtRestSite(IRunState runState, RestSiteRoom room)
    {
        if (runState == null || !PendingStarts.TryGetValue(runState, out BeginClaim claim) ||
            !ReferenceEquals(claim.Room, room))
            return;

        PendingStarts.Remove(runState);
        foreach (ElectronicSoldierManual manual in claim.Manuals)
            manual.SetMemoryGardenRestStorySeen(false);
    }

    private static string LocationKey(IRunState runState)
    {
        if (runState == null) return string.Empty;
        // Room IDs change on load; TotalFloor can change on same-node re-entry.
        // The act and map coordinate stay stable across both operations.
        return runState.CurrentMapCoord is { } coord
            ? FormattableString.Invariant($"{runState.CurrentActIndex}:{coord.col}:{coord.row}")
            : FormattableString.Invariant($"{runState.CurrentActIndex}:none");
    }
}
