using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    // Stored on this player's run relic, never in profile preferences or global state.
    // Older saves omit the property and start with no encountered characters.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryGardenMetCharacters { get; protected set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryGardenRepeatRewardClaims { get; protected set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenObtainedCardMask { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryGardenRestStoryReadyAt { get; protected set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MemoryGardenRestStorySeen { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ThirtyACollectionUpgradeTier { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryGardenSharedUpgrades { get; protected set; } = string.Empty;

    internal void SetMemoryGardenSharedUpgrades(string value)
    {
        AssertMutable();
        MemoryGardenSharedUpgrades = value;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel clonedBy)
    {
        HbrMemoryGardenProgression.Synchronize(card);
        if (card?.IsMutable == true && ReferenceEquals(card.Owner, Owner) &&
            (oldPileType == PileType.Deck || card.Pile?.Type == PileType.Deck))
        {
            await TeamCollectionRelics.EnsureObtained(Owner);
            HbrMemoryGardenRestStory.RefreshPermanentCards(Owner);
            await ThirtyACollectionUpgrades.Resolve(this);
        }
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        MemoryGardenUpgradePointsHelper.OnRoomEntered(this, room);
        HbrMemoryGardenProgression.Synchronize(Owner);
        await TeamCollectionRelics.EnsureObtained(Owner);
        await ThirtyACollectionUpgrades.Resolve(this);
    }

    internal void UnlockThirtyACollectionTier(int tier)
    {
        AssertMutable();
        ThirtyACollectionUpgradeTier = System.Math.Clamp(System.Math.Max(ThirtyACollectionUpgradeTier, tier), 0, 2);
    }

    internal void SetMemoryGardenObtainedCards(int mask, string readyAt)
    {
        AssertMutable();
        MemoryGardenObtainedCardMask = mask;
        MemoryGardenRestStoryReadyAt = readyAt;
    }

    internal void SetMemoryGardenRestStorySeen(bool seen)
    {
        AssertMutable();
        MemoryGardenRestStorySeen = seen;
    }

    internal void SetMemoryGardenEncounters(string encounters)
    {
        AssertMutable();
        MemoryGardenMetCharacters = encounters;
    }

    internal void SetMemoryGardenRepeatRewardClaims(string claims)
    {
        AssertMutable();
        MemoryGardenRepeatRewardClaims = claims;
    }

    internal void InheritMemoryProgressFrom(ElectronicSoldierManual source)
    {
        AssertMutable();
        source.SynchronizeMemoryGardenUpgradePoints();
        // Ketsu deliberately redistributes six-sense values. Encounter unlocks,
        // map nodes and garden reward receipts belong to the run, not that reset.
        MemoryGardenMetCharacters = source.MemoryGardenMetCharacters;
        MemoryGardenRepeatRewardClaims = source.MemoryGardenRepeatRewardClaims;
        MemoryGardenObtainedCardMask = source.MemoryGardenObtainedCardMask;
        MemoryGardenRestStoryReadyAt = source.MemoryGardenRestStoryReadyAt;
        MemoryGardenRestStorySeen = source.MemoryGardenRestStorySeen;
        ThirtyACollectionUpgradeTier = source.ThirtyACollectionUpgradeTier;
        ThirtyACollectionUpgradeClaims = source.ThirtyACollectionUpgradeClaims;
        MemoryGardenSharedUpgrades = source.MemoryGardenSharedUpgrades;
        MemoryGardenDampenRestores = source.MemoryGardenDampenRestores;
        MemoryFragmentMapData = source.MemoryFragmentMapData;
        MemoryGardenUpgradePoints = source.MemoryGardenUpgradePoints;
        MemoryGardenSixSenseUpgradeTier = source.MemoryGardenSixSenseUpgradeTier;
        MemoryGardenUpgradeRewardedRooms = source.MemoryGardenUpgradeRewardedRooms;
        HbrBattleProgressData = source.HbrBattleProgressData;
    }
}
