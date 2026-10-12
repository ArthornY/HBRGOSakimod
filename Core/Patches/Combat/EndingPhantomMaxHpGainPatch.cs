using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp))]
internal static class EndingPhantomMaxHpGainPatch
{
    private sealed record GainState(decimal MaxHp, decimal CurrentHp);

    private static void Prefix(Creature creature, out GainState __state)
    {
        __state = CombatManager.Instance.IsEnding && creature?.IsAlive == true &&
            PhantomHelper.IsOwnedMainPhantom(creature, creature.PetOwner)
                ? new GainState(LargeCreatureHealthHelper.GetMaxHp(creature),
                    LargeCreatureHealthHelper.GetCurrentHp(creature)) : null;
    }

    private static void Postfix(Creature creature, GainState __state, ref Task __result)
    {
        if (__state != null) __result = RestoreMissingHp(__result, creature, __state);
    }

    private static async Task RestoreMissingHp(Task originalTask, Creature creature, GainState state)
    {
        await originalTask;
        if (!creature.IsAlive) return;
        decimal gainedMaxHp = Math.Max(0m, LargeCreatureHealthHelper.GetMaxHp(creature) - state.MaxHp);
        decimal expectedHp = Math.Min(LargeCreatureHealthHelper.GetMaxHp(creature), state.CurrentHp + gainedMaxHp);
        decimal missingHp = expectedHp - LargeCreatureHealthHelper.GetCurrentHp(creature);
        if (gainedMaxHp <= 0 || missingHp <= 0) return;

        // Native Heal skips non-players during combat teardown. Final-kill
        // growth still needs its matching HP, without reviving or healing twice.
        creature.SetCurrentHpInternal(expectedHp);
        if (creature.PetOwner?.GetRelic<MomentOfReign>() is { } reign)
            await reign.AfterCurrentHpChanged(creature, missingHp);
    }
}
