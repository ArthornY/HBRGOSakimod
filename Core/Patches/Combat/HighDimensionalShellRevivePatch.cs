using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(
    typeof(CreatureCmd),
    nameof(CreatureCmd.Heal),
    typeof(Creature),
    typeof(decimal),
    typeof(bool))]
internal static class HighDimensionalShellRevivePatch
{
    private static void Prefix(Creature creature, out bool __state)
    {
        __state = creature?.IsDead == true;
    }

    private static void Postfix(
        Creature creature,
        bool __state,
        ref Task __result)
    {
        if (!__state)
        {
            return;
        }

        __result = GrantShellAfterRevive(__result, creature);
    }

    private static async Task GrantShellAfterRevive(
        Task originalTask,
        Creature creature)
    {
        await originalTask;
        if (creature?.IsAlive != true ||
            creature.CombatState == null ||
            !creature.CombatState.Enemies.Contains(creature) ||
            !HighDimensionalShellCombatStartPatch.ShouldGrantShells(
                creature.CombatState))
        {
            return;
        }

        await HighDimensionalShellCombatStartPatch.GrantShell(
            creature,
            replaceExisting: true);
    }
}

[HarmonyPatch(
    typeof(MonsterModel),
    nameof(MonsterModel.SetMoveImmediate),
    typeof(MoveState),
    typeof(bool))]
internal static class HighDimensionalShellStunDeathPatch
{
    private static void Prefix(
        MonsterModel __instance,
        ref bool forceTransition)
    {
        if (forceTransition ||
            __instance.Creature.IsAlive ||
            !__instance.Creature.IsStunned ||
            !__instance.Creature.HasPower<BreakPower>())
        {
            return;
        }

        // A death or revival state must supersede the unperformed shell-break stun.
        forceTransition = true;
    }
}
