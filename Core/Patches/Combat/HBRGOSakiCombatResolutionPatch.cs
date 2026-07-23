using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
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
                CombatManager.Instance.IsEnding &&
                !card.HasBeenRemovedFromState &&
                card.Pile?.Type == PileType.Play)
            {
                await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            }
        }
    }
}
