using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class SaviorGrandEntranceDamageScopePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method => method.GetParameters() is var parameters && parameters.Length is 6 or 7 &&
            parameters[1].ParameterType == typeof(IEnumerable<Creature>) && parameters[2].ParameterType == typeof(decimal) &&
            parameters[5].ParameterType == typeof(CardModel))
        .OrderByDescending(method => method.GetParameters().Length).First();

    // RollMultiHitDamagePatch prepares the actual winning dice first.
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Creature dealer, CardModel cardSource, ValueProp props,
        out SaviorGrandEntranceCombatHelper.DamageScope __state) =>
        __state = SaviorGrandEntranceCombatHelper.BeginDamage(dealer, cardSource, props);
    private static void Postfix(SaviorGrandEntranceCombatHelper.DamageScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        try { __result = SaviorGrandEntranceCombatHelper.FinishDamage(__result, __state); }
        finally { SaviorGrandEntranceCombatHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, SaviorGrandEntranceCombatHelper.DamageScope __state)
    {
        if (__exception != null) SaviorGrandEntranceCombatHelper.CloseDamage(__state);
        else SaviorGrandEntranceCombatHelper.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(ItsMygoIncomingDamageCapture), nameof(ItsMygoIncomingDamageCapture.AppendResults))]
internal static class SaviorGrandEntranceRawReceiptPatch
{
    private static void Prefix(ref IEnumerable<DamageResult> results)
    {
        IReadOnlyList<DamageResult> materialized = results as IReadOnlyList<DamageResult> ?? results.ToArray();
        results = materialized;
        SaviorGrandEntranceCombatHelper.BindRawRollReceipts(materialized);
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
internal static class SaviorGrandEntranceHealPatch
{
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        if (SaviorGrandEntranceCombatHelper.TakeMaxHpHeal(creature) ||
            !SaviorGrandEntranceCombatHelper.TryGetHealingEnemyOwner(creature, out Creature owner) || amount <= 0m)
            return true;
        __result = SaviorGrandEntranceCombatHelper.ConvertEnemyHealing(creature, owner, amount);
        return false;
    }
}

[HarmonyPatch]
internal static class SaviorGrandEntranceMaxHpHealPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo method = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp));
        return AccessTools.Method(method.GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType, "MoveNext");
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal));
        MethodInfo replacement = AccessTools.Method(typeof(SaviorGrandEntranceCombatHelper),
            nameof(SaviorGrandEntranceCombatHelper.HealFromMaxHp));
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(original)) { yield return instruction; continue; }
            changed++;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
        }
        if (changed != 1) throw new InvalidOperationException("Expected one native GainMaxHp healing call.");
    }
}

[HarmonyPatch]
internal static class SaviorGrandEntrancePhaseHealPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AsyncBody(typeof(TestSubject), "Revive");
        yield return AsyncBody(typeof(IllusionPower), "ReviveMove");
    }

    private static MethodBase AsyncBody(Type type, string name)
    {
        MethodInfo method = AccessTools.Method(type, name);
        Type state = method?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMethodException(type.FullName, name + " async state machine");
        return AccessTools.Method(state, "MoveNext")
            ?? throw new MissingMethodException(state.FullName, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal));
        MethodInfo replacement = AccessTools.Method(typeof(SaviorGrandEntranceCombatHelper),
            nameof(SaviorGrandEntranceCombatHelper.HealFromPhaseTransition));
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(original)) { yield return instruction; continue; }
            changed++;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
        }
        if (changed != 1) throw new InvalidOperationException("Expected one native phase restoration healing call.");
    }
}
