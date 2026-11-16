using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ModeRestrictedCardPlayPatch
{
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(
        CardModel __instance,
        PlayerChoiceContext choiceContext,
        Creature target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals,
        ref Task __result)
    {
        ModeTransitionPlayState state = ModeTransitionHelper.BeginCardPlay(__instance);
        if (state.RequiredMode == ReptureCardMode.None && !DualSwordsChaseHelper.NeedsPlayScope(__instance))
        {
            return true;
        }

        __result = SwitchThenPlay(
            __instance,
            choiceContext,
            target,
            isAutoPlay,
            resources,
            skipCardPileVisuals,
            state);
        return false;
    }

    private static async Task SwitchThenPlay(
        CardModel card,
        PlayerChoiceContext choiceContext,
        Creature target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals,
        ModeTransitionPlayState state)
    {
        using var chase = DualSwordsChaseHelper.BeginPlay(card, isAutoPlay);
        await ModeTransitionHelper.EnterRequiredModeBeforePlay(
            choiceContext,
            card,
            state.RequiredMode);
        await OnPlayWrapperOriginal(
            card,
            choiceContext,
            target,
            isAutoPlay,
            resources,
            skipCardPileVisuals);
    }

    [HarmonyReversePatch]
    [HarmonyPatch(
        typeof(CardModel),
        nameof(CardModel.OnPlayWrapper))]
    private static Task OnPlayWrapperOriginal(
        CardModel instance,
        PlayerChoiceContext choiceContext,
        Creature target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals)
    {
        throw new NotImplementedException(
            "Harmony reverse patch stub");
    }
}
