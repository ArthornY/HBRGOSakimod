using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyACollectionUpgrades
{
    private sealed class PendingState { internal bool Resolving; }
    private static readonly ConditionalWeakTable<ElectronicSoldierManual, PendingState> Pending = new();

    internal static int GetUnlockedTier(int distinctKinds) => distinctKinds >= 5 ? 2 : distinctKinds >= 3 ? 1 : 0;

    // Milestones belong to the card kind for this run, not to an individual
    // copy. Import old per-card receipts before offering an unclaimed tier.
    internal static void SynchronizeClaims(CardModel card)
    {
        if (card?.IsMutable != true || card.HasBeenRemovedFromState ||
            !ThirtyAKeywordRules.IsThirtyACard(card) ||
            card.Owner?.GetRelic<ElectronicSoldierManual>() is not { IsMutable: true } manual)
            return;

        CardModel[] copies = GetOwnedCopies(manual.Owner, card).ToArray();
        int claimedTier = copies.Max(copy =>
            copy.Capability<ThirtyAKeywordCapability>().CollectionUpgradesClaimed);
        claimedTier = Math.Max(claimedTier,
            manual.GetThirtyACollectionUpgradeClaim(card.Id.Entry));
        ClaimKind(manual, card, claimedTier);
    }

    internal static async Task Resolve(ElectronicSoldierManual manual)
    {
        if (manual?.IsMutable != true || manual.Owner is not { } player ||
            player.GetRelic<ThirtyACollection>() is not { IsMelted: false } collection ||
            !MemoryGardenSelectionScope.IsCurrentRun(player)) return;

        foreach (CardModel card in player.Deck.Cards.ToArray().Where(candidate =>
                     IsPermanentThirtyACard(player, candidate)))
            SynchronizeClaims(card);

        // Remember crossings during combat, but defer the choice until a safe
        // room. Copies share both the selected effects and the claimed tiers.
        manual.UnlockThirtyACollectionTier(GetUnlockedTier(collection.DisplayAmount));
        if (!CanChoose(player) || manual.ThirtyACollectionUpgradeTier == 0) return;

        PendingState state = Pending.GetOrCreateValue(manual);
        // A nested deck-change hook must not await its own outer selection.
        if (state.Resolving) return;
        state.Resolving = true;
        try
        {
            while (CanChoose(player))
            {
                CardModel card = player.Deck.Cards.ToArray().FirstOrDefault(candidate =>
                    IsPermanentThirtyACard(player, candidate) &&
                    candidate.Capability<ThirtyAKeywordCapability>().CollectionUpgradesClaimed < manual.ThirtyACollectionUpgradeTier);
                if (card == null) return;

                int nextTier = card.Capability<ThirtyAKeywordCapability>().CollectionUpgradesClaimed + 1;
                // A fully upgraded card cannot receive another effect. Count
                // the entitlement as fulfilled, without blocking other cards.
                if (HbrMemoryGardenUpgradeCatalog.GetAvailableEffectIds(card).Count == 0)
                {
                    ClaimKind(manual, card, manual.ThirtyACollectionUpgradeTier);
                    continue;
                }

                bool upgraded = await HbrMemoryGarden.ChooseUpgrade(card,
                    () => CanChoose(player) && IsPermanentThirtyACard(player, card) &&
                        manual.GetThirtyACollectionUpgradeClaim(card.Id.Entry) < nextTier &&
                        manual.ThirtyACollectionUpgradeTier >= nextTier,
                    nextTier == 1 ? "thirtyAUpgradeNotice3" : "thirtyAUpgradeNotice5");
                // Closing the run or losing the UI leaves the reward pending
                // for the next safe room; do not reroll in a tight loop.
                if (!upgraded) return;
                ClaimKind(manual, card, nextTier);
            }
        }
        finally
        {
            state.Resolving = false;
        }
    }

    private static void ClaimKind(ElectronicSoldierManual manual, CardModel card, int tier)
    {
        manual.ClaimThirtyACollectionUpgrade(card.Id.Entry, tier);
        int claimedTier = manual.GetThirtyACollectionUpgradeClaim(card.Id.Entry);
        foreach (CardModel copy in GetOwnedCopies(manual.Owner, card))
            copy.Capability<ThirtyAKeywordCapability>().ClaimCollectionUpgrade(claimedTier);
    }

    private static IEnumerable<CardModel> GetOwnedCopies(Player player, CardModel card) =>
        player.Deck.Cards
            .Concat(player.PlayerCombatState?.AllCards ?? Enumerable.Empty<CardModel>())
            .Append(card)
            .Where(copy => copy?.IsMutable == true && !copy.HasBeenRemovedFromState &&
                ReferenceEquals(copy.Owner, player) && copy.Id == card.Id)
            .Distinct();

    private static bool CanChoose(Player player) => MemoryGardenSelectionScope.IsCurrentRun(player) &&
        player.GetRelic<ThirtyACollection>() is { IsMelted: false } &&
        player.RunState.CurrentRoom is not CombatRoom;

    private static bool IsPermanentThirtyACard(Player player, CardModel card) =>
        card?.IsMutable == true && !card.HasBeenRemovedFromState && ReferenceEquals(card.Owner, player) &&
        card.Pile?.Type == PileType.Deck && ThirtyAKeywordRules.IsThirtyACard(card) &&
        player.Deck.Cards.Any(owned => ReferenceEquals(owned, card));
}
