using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rewards;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrMemoryGardenCardAcquisition
{
    internal static bool IsUnauthorized(CardModel card) =>
        HbrMemoryGardenCardCatalog.IsGardenCard(card) &&
        (!card.IsMutable || !HbrMemoryGarden.IsAcquisitionAuthorized(card.Owner, card));

    internal static IEnumerable<CardModel> WithoutGardenCards(IEnumerable<CardModel> cards) =>
        cards.Where(card => !HbrMemoryGardenCardCatalog.IsGardenCard(card));

    internal static IEnumerable<CardModel> GetOrdinaryTransformationCards(CardModel original) =>
        WithoutGardenCards(original.Owner.Character.CardPool.GetUnlockedCards(
            original.Owner.UnlockState, original.Owner.RunState.CardMultiplayerConstraint));

    internal static IEnumerable<CardTransformation> FilterPermanentTransformations(
        IEnumerable<CardTransformation> transformations)
    {
        foreach (CardTransformation transformation in transformations)
        {
            // A deck mutation stays permanent even when requested during combat.
            // Existing combat copies in hand/draw/discard remain unrestricted.
            if (transformation.Original.Pile?.Type != PileType.Deck)
            {
                yield return transformation;
                continue;
            }

            if (transformation.Replacement != null)
            {
                if (!IsUnauthorized(transformation.Replacement)) yield return transformation;
                continue;
            }

            if (transformation.ReplacementOptions != null)
            {
                CardModel[] candidates = WithoutGardenCards(transformation.ReplacementOptions).ToArray();
                // Omit a rejected transformation before CardCmd removes its original.
                if (candidates.Length > 0)
                    yield return new CardTransformation(transformation.Original, candidates);
                continue;
            }

            yield return transformation;
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.CloneCard))]
internal static class HbrMemoryGardenCardCopyAcquisitionPatch
{
    private static void Postfix(CardModel mutableCard, CardModel __result) =>
        HbrMemoryGarden.AuthorizeCopy(mutableCard, __result);
}

[HarmonyPatch(typeof(SpecialCardReward), nameof(SpecialCardReward.SetCustomDescriptionEncounterSource))]
internal static class HbrMemoryGardenStolenCardReturnPatch
{
    private static void Postfix(SpecialCardReward __instance, ModelId encounterId, CardModel ____card)
    {
        // SwipePower and Reward.FromSerializable both mark genuine theft
        // returns with this encounter; other special rewards stay restricted.
        if (encounterId == ModelDb.Encounter<ThievingHopperWeak>().Id)
            HbrMemoryGarden.AuthorizeTheftReturn(__instance.Player, ____card);
    }
}

// This API feeds permanent rewards, including explicit pools and NoCardPoolModifications.
// Combat generation goes through CardFactory.GetForCombat/CombatState.CreateCard instead.
[HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards))]
internal static class HbrMemoryGardenRewardCandidatesPatch
{
    [HarmonyPriority(Priority.Last - 2)]
    private static void Postfix(ref IEnumerable<CardModel> __result) =>
        __result = HbrMemoryGardenCardAcquisition.WithoutGardenCards(__result);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAddToDeck))]
internal static class HbrMemoryGardenDeckAcquisitionPatch
{
    [HarmonyPriority(Priority.Last - 2)]
    private static void Postfix(CardModel card, ref AbstractModel preventer, ref bool __result)
    {
        if (!__result || !HbrMemoryGardenCardAcquisition.IsUnauthorized(card)) return;
        __result = false;
        // CardPileCmd awaits preventer.AfterAddToDeckPrevented; the card supplies the
        // harmless base implementation without depending on any player's relics.
        preventer = card;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardBeingAddedToDeck))]
internal static class HbrMemoryGardenDeckModificationPatch
{
    [HarmonyPriority(Priority.Last - 2)]
    private static void Postfix(CardModel card, List<AbstractModel> modifyingModels, ref CardModel __result)
    {
        // This hook runs after ShouldAddToDeck, and also runs for transformations.
        // A modifier must not turn an otherwise allowed acquisition into a garden card.
        if (ReferenceEquals(card, __result) ||
            !HbrMemoryGardenCardAcquisition.IsUnauthorized(__result)) return;
        __result = card;
        modifyingModels?.Clear();
    }
}

[HarmonyPatch(typeof(CardFactory), "GetFilteredTransformationOptions")]
internal static class HbrMemoryGardenRandomTransformationPatch
{
    private static void Prefix(CardModel original, bool isInCombat, ref IEnumerable<CardModel> originalOptions)
    {
        if (isInCombat && original.Pile?.Type != PileType.Deck) return;
        CardModel[] supplied = originalOptions.ToArray();
        CardModel[] allowed = HbrMemoryGardenCardAcquisition.WithoutGardenCards(supplied).ToArray();
        if (allowed.Length == supplied.Length) return;

        // A directly invoked factory still needs a legal result when its source pool
        // consists only of garden cards. CardCmd's explicit all-garden batches are
        // skipped earlier; this fallback also lets existing garden cards transform out.
        originalOptions = allowed.Length > 0
            ? allowed
            : HbrMemoryGardenCardAcquisition.GetOrdinaryTransformationCards(original);
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Transform),
    new[] { typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle) })]
internal static class HbrMemoryGardenFixedTransformationPatch
{
    private static void Prefix(ref IEnumerable<CardTransformation> transformations) =>
        transformations = HbrMemoryGardenCardAcquisition.FilterPermanentTransformations(transformations).ToArray();
}
