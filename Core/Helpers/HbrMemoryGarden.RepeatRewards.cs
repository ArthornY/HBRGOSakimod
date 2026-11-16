using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static partial class HbrMemoryGarden
{
    internal static bool IsRepeatMemoryEvent(Player player, HbrStoryEvent story)
    {
        if (!CanAccess(player) || !HbrMemoryFragmentPool.IsMemoryFragment(story)) return false;
        var run = player.RunState;
        // Selection already adds the current event to VisitedEventIds. The map
        // history distinguishes a previous visit from this node, including SL.
        return run.MapPointHistory.SelectMany(act => act)
            .Where(point => !ReferenceEquals(point, run.CurrentMapPointHistoryEntry))
            .Any(point => point.Rooms.Any(room => room.RoomType == RoomType.Event && room.ModelId == story.Id));
    }

    private static string RepeatVisitKey(Player player, HbrStoryEvent story)
    {
        var run = player.RunState;
        string location = run.CurrentMapCoord is { } coord
            ? FormattableString.Invariant($"{run.CurrentActIndex}:{coord.col}:{coord.row}")
            : FormattableString.Invariant($"{run.CurrentActIndex}:none:{run.MapPointHistory.Sum(act => act.Count)}");
        return location + ":" + story.Id.Entry + ":";
    }

    private static HashSet<string> ReadRepeatClaims(ElectronicSoldierManual manual) => new(
        (manual.MemoryGardenRepeatRewardClaims ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries),
        StringComparer.Ordinal);

    // Save this node's entry knowledge before RecordEncounters unlocks its
    // newly chosen character. The existing per-owner receipt field survives
    // event replay/SL, so reloading cannot turn a first encounter into a repeat.
    internal static void CaptureRepeatMemoryEligibility(Player player, HbrStoryEvent story)
    {
        if (player == null || !HbrMemoryFragmentPool.IsMemoryFragment(story)) return;
        IEnumerable<Player> participants = story.IsShared ? player.RunState.Players : new[] { player };
        foreach (Player participant in participants)
        {
            if (!CanAccess(participant)) continue;
            ElectronicSoldierManual manual = participant.GetRelic<ElectronicSoldierManual>();
            HashSet<string> claims = ReadRepeatClaims(manual);
            string prefix = RepeatVisitKey(participant, story) + "@met:";
            if (claims.Any(claim => claim.StartsWith(prefix, StringComparison.Ordinal))) continue;
            claims.Add(prefix + string.Join(",", ReadEncounters(manual.MemoryGardenMetCharacters)
                .OrderBy(id => id, StringComparer.Ordinal)));
            manual.SetMemoryGardenRepeatRewardClaims(string.Join("|", claims.Order(StringComparer.Ordinal)));
        }
    }

    private static HashSet<string> RepeatEligibleCharacters(Player player, HbrStoryEvent story)
    {
        string prefix = RepeatVisitKey(player, story) + "@met:";
        string stored = ReadRepeatClaims(player.GetRelic<ElectronicSoldierManual>())
            .FirstOrDefault(claim => claim.StartsWith(prefix, StringComparison.Ordinal));
        return stored == null ? new HashSet<string>(StringComparer.Ordinal) : ReadEncounters(stored[prefix.Length..]);
    }

    private static CharacterEntry[] PendingRepeatCharacters(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered)
    {
        if (!IsRepeatMemoryEvent(player, story)) return Array.Empty<CharacterEntry>();
        HashSet<string> met = new(encountered, StringComparer.Ordinal);
        met.IntersectWith(RepeatEligibleCharacters(player, story));
        HashSet<string> claims = ReadRepeatClaims(player.GetRelic<ElectronicSoldierManual>());
        string visit = RepeatVisitKey(player, story);
        return Characters.Where(character => met.Contains(character.Id) && !claims.Contains(visit + character.Id)).ToArray();
    }

    internal static bool HasRepeatMemoryRewards(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered) => PendingRepeatCharacters(player, story, encountered).Length > 0;

    internal static bool NeedsRepeatMemoryRewardStep(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered)
    {
        if (!story.IsShared) return HasRepeatMemoryRewards(player, story, encountered);
        // Shared event options must match for every player, including teammates
        // without the manual and players who already claimed this node's reward.
        return player.RunState.Players.Any(teammate => HasRepeatMemoryRewards(teammate, story, encountered));
    }

    internal static async Task<bool> ClaimRepeatMemoryRewards(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered)
    {
        bool CanClaim() => IsRepeatMemoryEvent(player, story) &&
            player.RunState.CurrentRoom is EventRoom room && room.CanonicalEvent.Id == story.Id;
        if (!CanClaim()) return false;
        MemoryGardenSelectionScope scope = MemoryGardenSelectionScope.Capture(player);
        ElectronicSoldierManual manual = player.GetRelic<ElectronicSoldierManual>();
        string visit = RepeatVisitKey(player, story);
        bool completed = true;
        foreach (CharacterEntry character in PendingRepeatCharacters(player, story, encountered))
        {
            scope.ThrowIfInvalid();
            if (!CanClaim()) return false;
            ModelId cardId = ModelDb.GetId(character.CardType);
            CardModel owned = FindOwnedCard(player, cardId);
            if (owned != null)
            {
                // Fully upgraded cards have already exhausted their legal effects.
                if (HbrMemoryGardenUpgradeCatalog.GetAvailableEffectIds(owned).Count > 0 &&
                    !await ChooseUpgrade(owned, CanClaim, "repeatMemoryUpgradeNotice"))
                {
                    scope.ThrowIfInvalid();
                    completed = false;
                    continue;
                }
            }
            else
            {
                CardModel granted = player.RunState.CreateCard(ModelDb.GetById<CardModel>(cardId), player);
                CardModel previousPermit = AuthorizedCard.Value;
                AuthorizedCard.Value = granted;
                try
                {
                    CardPileAddResult result = await CardPileCmd.Add(granted, PileType.Deck);
                    scope.ThrowIfInvalid();
                    if (!result.success)
                    {
                        if (granted.Pile == null && !granted.HasBeenRemovedFromState) granted.RemoveFromState();
                        completed = false;
                        continue;
                    }
                }
                finally { AuthorizedCard.Value = previousPermit; }
            }

            scope.ThrowIfInvalid();
            HashSet<string> claims = ReadRepeatClaims(manual);
            claims.Add(visit + character.Id);
            manual.SetMemoryGardenRepeatRewardClaims(string.Join("|", claims.Order(StringComparer.Ordinal)));
        }
        return completed;
    }
}
