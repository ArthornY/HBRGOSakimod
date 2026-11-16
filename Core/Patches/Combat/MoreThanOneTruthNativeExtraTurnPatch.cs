using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native extra turns still execute native turn setup. OD and personal turns
// have isolated setup and call this same effect boundary directly instead.
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
internal static class MoreThanOneTruthNativeExtraTurnPatch
{
    private static void Postfix(PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        if (!CombatManager.Instance.PlayersTakingExtraTurn.Contains(player) ||
            OverclockImmediateTurnCoordinator.IsActiveFor(player) ||
            PersonalImmediateTurnCoordinator.IsActiveFor(player)) return;
        __result = Finish(__result, choiceContext, player);
    }
    private static async Task Finish(Task original, PlayerChoiceContext context, Player player)
    {
        await original;
        await MoreThanOneTruthCombatHelper.OnExtraTurnEntered(context, player);
    }
}
