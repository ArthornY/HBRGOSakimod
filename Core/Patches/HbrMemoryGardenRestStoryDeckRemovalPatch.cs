using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

// Native RemoveFromDeck does not dispatch AfterCardChangedPiles. Refresh after
// the actual removal so a removed CRYCHIC member cannot leave a stale trigger.
[HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
internal static class HbrMemoryGardenRestStoryDeckRemovalPatch
{
    private static void Postfix(CardPile __instance, CardModel card)
    {
        if (__instance.Type == PileType.Deck && card?.IsMutable == true &&
            ReferenceEquals(__instance, card.Owner?.Deck))
        {
            TeamCollectionRelics.RefreshCounters(card.Owner);
            HbrMemoryGardenRestStory.RefreshPermanentCards(card.Owner);
        }
    }
}
