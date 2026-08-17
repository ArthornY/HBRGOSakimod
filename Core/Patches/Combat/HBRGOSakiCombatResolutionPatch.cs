using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.CheckWinCondition))]
internal static class HBRGOSakiCombatWinDeferralPatch
{
    private static bool Prefix(ref Task<bool> __result)
    {
        if (RunManager.Instance.ActionExecutor.CurrentlyRunningAction is not PlayCardAction
            {
                State: GameActionState.Executing,
                Player.Character: HbrgoSaki
            })
        {
            return true;
        }

        __result = Task.FromResult(false);
        return false;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class HBRGOSakiCardPlayCleanupPatch
{
    private static void Postfix(CardModel __instance, ref Task __result)
    {
        if (__instance.Owner?.Character is HbrgoSaki)
        {
            __result = CleanupAfterPlay(__result, __instance);
        }
    }

    private static async Task CleanupAfterPlay(Task playTask, CardModel card)
    {
        try
        {
            await playTask;
        }
        finally
        {
            if (CombatManager.Instance.IsInProgress &&
                CombatManager.Instance.IsEnding)
            {
                bool isRootPlayCard = IsRootPlayCardAction(card);
                await RemoveFromPlayPile(card);
                if (isRootPlayCard)
                {
                    await ClearOwnerPlayPile(card);
                }
            }
        }
    }

    private static bool IsRootPlayCardAction(CardModel card)
    {
        return RunManager.Instance.ActionExecutor.CurrentlyRunningAction is PlayCardAction action &&
            ReferenceEquals(action.NetCombatCard.ToCardModelOrNull(), card);
    }

    private static async Task RemoveFromPlayPile(CardModel card)
    {
        if (!card.HasBeenRemovedFromState && card.Pile?.Type == PileType.Play)
        {
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        }
    }

    private static async Task ClearOwnerPlayPile(CardModel card)
    {
        List<CardModel> cardsInPlay = PileType.Play.GetPile(card.Owner).Cards.ToList();
        foreach (CardModel cardInPlay in cardsInPlay)
        {
            await RemoveFromPlayPile(cardInPlay);
        }
    }
}
