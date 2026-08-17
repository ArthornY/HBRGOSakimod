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
        if (!ShouldGrantShells(combatState))
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies.Where(enemy => enemy.IsAlive))
        {
            await GrantShell(enemy, replaceExisting: false);
        }
    }

    internal static bool ShouldGrantShells(ICombatState combatState)
    {
        return combatState != null &&
            combatState.Players.Any(
                player => player.Character is HbrgoSaki);
    }

    internal static async Task GrantShell(
        Creature enemy,
        bool replaceExisting)
    {
        if (enemy == null || !enemy.IsAlive)
        {
            return;
        }

        HighDimensionalShellPower existingShell =
            enemy.GetPower<HighDimensionalShellPower>();
        if (!replaceExisting && existingShell != null)
        {
            return;
        }

        if (replaceExisting && existingShell != null)
        {
            await PowerCmd.Remove(existingShell);
        }

        BreakPower breakPower = enemy.GetPower<BreakPower>();
        if (replaceExisting && breakPower != null)
        {
            await PowerCmd.Remove(breakPower);
        }

        DestructionRatePower destructionRate =
            enemy.GetPower<DestructionRatePower>();
        if (replaceExisting && destructionRate != null)
        {
            await PowerCmd.Remove(destructionRate);
        }

        if (enemy.HasPower<HighDimensionalShellPower>())
        {
            return;
        }

        await PowerCmd.Apply<HighDimensionalShellPower>(
            new ThrowingPlayerChoiceContext(),
            enemy,
            HighDimensionalShellPower.CalculateAmount(enemy),
            enemy,
            null);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCreatureAddedToCombat))]
internal static class HighDimensionalShellCreatureAddedPatch
{
    private static void Postfix(
        ICombatState combatState,
        Creature creature,
        ref Task __result)
    {
        __result = GrantShellAfterCreatureAdded(
            __result,
            combatState,
            creature);
    }

    private static async Task GrantShellAfterCreatureAdded(
        Task originalTask,
        ICombatState combatState,
        Creature creature)
    {
        await originalTask;
        if (creature?.IsAlive != true ||
            combatState == null ||
            !combatState.Enemies.Contains(creature) ||
            !HighDimensionalShellCombatStartPatch.ShouldGrantShells(
                combatState))
        {
            return;
        }

        await HighDimensionalShellCombatStartPatch.GrantShell(
            creature,
            replaceExisting: false);
    }
}
