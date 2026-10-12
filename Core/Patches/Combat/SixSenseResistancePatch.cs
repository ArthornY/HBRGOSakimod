using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class SixSenseResistanceCombatStartPatch
{
    private static void Postfix(ICombatState combatState, ref Task __result) =>
        __result = Apply(__result, combatState);

    private static async Task Apply(Task original, ICombatState combatState)
    {
        await original;
        if (combatState == null) return;
        foreach (Creature enemy in combatState.Enemies.ToArray()) await EnsureResistance(enemy);
    }

    internal static async Task EnsureResistance(Creature enemy)
    {
        if (!SixSenseCombatHelper.IsEligibleEnemy(enemy) || enemy.HasPower<SixSenseResistancePower>()) return;
        await PowerCmd.Apply<SixSenseResistancePower>(new ThrowingPlayerChoiceContext(), enemy, 1m, enemy, null);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCreatureAddedToCombat))]
internal static class SixSenseResistanceCreatureAddedPatch
{
    private static void Postfix(Creature creature, ref Task __result) =>
        __result = Apply(__result, creature);

    private static async Task Apply(Task original, Creature creature)
    {
        await original;
        await SixSenseResistanceCombatStartPatch.EnsureResistance(creature);
    }
}
