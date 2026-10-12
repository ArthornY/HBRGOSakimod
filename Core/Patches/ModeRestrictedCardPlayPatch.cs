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
    private static bool Prefix(
        CardModel __instance,
        PlayerChoiceContext choiceContext,
        Creature target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals,
        out ModeTransitionPlayState __state,
        ref Task __result)
    {
        __state = ModeTransitionHelper.BeginCardPlay(__instance);
        if (__state.RequiredMode == ReptureCardMode.None)
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
            __state);
        return false;
    }

    private static void Postfix(
        CardModel __instance,
        ModeTransitionPlayState __state,
        ref Task __result)
    {
        if (!__state.HasEffectContext ||
            __state.RequiredMode != ReptureCardMode.None)
        {
            return;
        }

        __result = FinishUnchangedPlay(
            __result,
            __instance,
            __state);
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
        bool modeChanged = false;
        try
        {
            modeChanged =
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
        finally
        {
            try
            {
                if (state.QuickSwitch != null)
                {
                    await state.QuickSwitch.FinishReservation(
                        choiceContext,
                        card,
                        modeChanged);
                }

            }
            finally
            {
                ModeTransitionHelper.EndCardPlay(
                    card,
                    state);
            }
        }
    }

    private static async Task FinishUnchangedPlay(
        Task original,
        CardModel card,
        ModeTransitionPlayState state)
    {
        try
        {
            await original;
        }
        finally
        {
            ModeTransitionHelper.EndCardPlay(
                card,
                state);
        }
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
