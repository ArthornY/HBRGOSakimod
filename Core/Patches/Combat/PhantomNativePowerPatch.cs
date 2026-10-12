using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PlatingPower), nameof(PlatingPower.BeforeSideTurnStart))]
internal static class PhantomPlatingOpeningBlockPatch
{
    private static bool Prefix(PlatingPower __instance, ref Task __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() != true) return true;
        // Vanilla preloads enemy block before the first player turn. A phantom
        // is also a non-player creature, but must earn its plating block at turn end.
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(PlatingPower), nameof(PlatingPower.AfterSideTurnStart))]
internal static class PhantomPlatingDecayPatch
{
    private static bool Prefix(PlatingPower __instance, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState, ref Task __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() != true) return true;
        __result = participants.Contains(__instance.Owner) && combatState.RoundNumber > 1
            ? PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), __instance,
                -__instance.DynamicVars["Decrement"].BaseValue, null, null)
            : Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(RampartPower), nameof(RampartPower.AfterSideTurnStart))]
internal static class PhantomRampartPatch
{
    private static bool Prefix(RampartPower __instance, CombatSide side,
        ICombatState combatState, ref Task __result)
    {
        Creature owner = __instance.Owner;
        if (owner?.HasPower<PhantomPower>() != true) return true;
        __result = owner.IsAlive && side != owner.Side
            ? ProtectAlliedTurrets(__instance, combatState)
            : Task.CompletedTask;
        return false;
    }

    private static async Task ProtectAlliedTurrets(RampartPower power, ICombatState combatState)
    {
        foreach (Creature ally in combatState.Allies.Where(creature =>
            creature.IsAlive && creature.Side == power.Owner.Side && creature.Monster is TurretOperator))
        {
            await CreatureCmd.GainBlock(ally, power.Amount, ValueProp.Unpowered, null);
        }
    }
}

[HarmonyPatch(typeof(ReattachPower), "DoFadeOutOnAllSegments")]
internal static class PhantomReattachFadePatch
{
    private static bool Prefix(ReattachPower __instance) =>
        __instance.Owner?.PetOwner == null || GetFadeTargets(__instance.Owner.CombatState, __instance).Count > 0;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method &&
                method == AccessTools.PropertyGetter(typeof(ICombatState), nameof(ICombatState.Enemies)))
            {
                var owner = new CodeInstruction(OpCodes.Ldarg_0);
                owner.labels.AddRange(instruction.labels);
                owner.blocks.AddRange(instruction.blocks);
                yield return owner;
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(PhantomReattachFadePatch), nameof(GetFadeTargets)));
            }
            else
            {
                yield return instruction;
            }
        }
    }

    internal static IReadOnlyList<Creature> GetFadeTargets(ICombatState state, ReattachPower power) =>
        power.Owner?.PetOwner == null ? state.Enemies : state.Allies.Where(creature =>
            creature.Side == power.Owner.Side && creature.IsDead &&
            creature.Monster is DecimillipedeSegment).ToArray();
}
