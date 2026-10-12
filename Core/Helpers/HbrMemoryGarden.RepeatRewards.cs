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

    private static CharacterEntry[] PendingRepeatCharacters(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered)
    {
        if (!IsRepeatMemoryEvent(player, story)) return Array.Empty<CharacterEntry>();
        HashSet<string> met = new(encountered, StringComparer.Ordinal);
        HashSet<string> claims = ReadRepeatClaims(player.GetRelic<ElectronicSoldierManual>());
        string visit = RepeatVisitKey(player, story);
        return Characters.Where(character => met.Contains(character.Id) && !claims.Contains(visit + character.Id)).ToArray();
    }

    internal static bool HasRepeatMemoryRewards(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered) => PendingRepeatCharacters(player, story, encountered).Length > 0;

    internal static bool NeedsRepeatMemoryRewardStep(Player player, HbrStoryEvent story,
        IEnumerable<string> encountered)
    {
        // Shared event options must match for every player, including teammates
        // without the manual and players who already claimed this node's reward.
        HashSet<string> met = new(encountered, StringComparer.Ordinal);
        return Characters.Any(character => met.Contains(character.Id)) &&
            player.RunState.Players.Any(teammate => IsRepeatMemoryEvent(teammate, story));
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
