using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class HighDimensionalShellCombatStartPatch
{
    private static void Postfix(ICombatState combatState, ref Task __result)
    {
        __result = ApplyShellsAfterCombatStartHooks(__result, combatState);
    }

    private static async Task ApplyShellsAfterCombatStartHooks(
        Task originalTask,
        ICombatState combatState)
    {
        await originalTask;
        if (combatState == null ||
            !combatState.Players.Any(player => player.Character is HbrgoSaki))
        {
            return;
        }

        PlayerChoiceContext choiceContext = new ThrowingPlayerChoiceContext();
        foreach (Creature enemy in combatState.Enemies.Where(enemy => enemy.IsAlive))
        {
            if (enemy.HasPower<HighDimensionalShellPower>())
            {
                continue;
            }

            await PowerCmd.Apply<HighDimensionalShellPower>(
                choiceContext,
                enemy,
                HighDimensionalShellPower.CalculateAmount(enemy),
                enemy,
                null);
        }
    }
}
