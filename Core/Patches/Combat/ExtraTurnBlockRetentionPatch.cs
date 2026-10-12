using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldClearBlock))]
internal static class ExtraTurnBlockRetentionPatch
{
    internal static bool ShouldPreserveBlock(
        ICombatState combatState,
        Creature creature,
        CombatManager manager)
    {
        if (combatState == null || creature?.Player is not { } player ||
            manager == null || !manager.IsInProgress || manager.IsOverOrEnding ||
            !ReferenceEquals(manager.DebugOnlyGetState(), combatState) ||
            !ReferenceEquals(creature.CombatState, combatState) ||
            !ReferenceEquals(player.Creature, creature) || !creature.IsAlive ||
            combatState.CurrentSide != CombatSide.Player || creature.Side != CombatSide.Player ||
            player.PlayerCombatState?.Phase != PlayerTurnPhase.Start)
            return false;

        return manager.PlayersTakingExtraTurn.Contains(player);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        ICombatState combatState,
        Creature creature,
        ref bool __result)
    {
        // Keep every original predicate and preventer callback intact. Only
        // override a natural block clear for this native extra-turn player.
        if (__result && ShouldPreserveBlock(combatState, creature, CombatManager.Instance))
            __result = false;
    }
}
