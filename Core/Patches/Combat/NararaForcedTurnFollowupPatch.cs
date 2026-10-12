using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// A pending Shiba turn end is a boundary between complete damage commands,
// rather than a boundary after an entire potentially unbounded card chain.
[HarmonyPatch]
internal static class NararaForcedTurnDamageScopePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Single(method => method.Name == nameof(CreatureCmd.Damage) &&
            method.GetParameters() is { Length: 6 or 7 } parameters &&
            parameters[0].ParameterType == typeof(PlayerChoiceContext) &&
            parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
            parameters[2].ParameterType == typeof(decimal) &&
            parameters[3].ParameterType == typeof(ValueProp) &&
            parameters[4].ParameterType == typeof(Creature) &&
            parameters[5].ParameterType == typeof(CardModel));

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Creature dealer, CardModel cardSource,
        ref Task<IEnumerable<DamageResult>> __result,
        out NararaForcedTurnEndHelper.DamageScope __state)
    {
        __state = null;
        ICombatState combat = cardSource?.Owner?.Creature.CombatState ?? dealer?.CombatState ??
            CombatManager.Instance.DebugOnlyGetState();
        if (NararaForcedTurnEndHelper.ShouldStopFollowups(combat, out _))
        {
            __result = Task.FromResult<IEnumerable<DamageResult>>(Array.Empty<DamageResult>());
            return false;
        }
        __state = NararaForcedTurnEndHelper.BeginDamage(dealer, cardSource);
        return true;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NararaForcedTurnEndHelper.DamageScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state == null) return;
        try { __result = NararaForcedTurnEndHelper.FinishDamage(__result, __state); }
        finally { NararaForcedTurnEndHelper.RestoreDamageCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, NararaForcedTurnEndHelper.DamageScope __state)
    {
        if (__exception != null) NararaForcedTurnEndHelper.CloseDamage(__state);
        else NararaForcedTurnEndHelper.RestoreDamageCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class NararaForcedTurnAttackTargetsPatch
{
    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<Creature> __result)
    {
        if (NararaForcedTurnEndHelper.ShouldStopFollowups(__instance.Attacker?.CombatState, out _))
            __result = Array.Empty<Creature>();
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldPlay))]
internal static class NararaForcedTurnAutomaticCardPatch
{
    // Native AutoPlay handles a rejected card's removal/pile cleanup itself.
    // Keep that path instead of skipping its wrapper or throwing cancellation.
    private static void Postfix(ICombatState __0, CardModel __1, ref AbstractModel __2, ref bool __result)
    {
        if (__result && NararaForcedTurnEndHelper.ShouldStopAutomaticPlays(__0, out MonsterModel preventer))
        {
            __2 = preventer;
            __result = false;
        }
    }
}

[HarmonyPatch]
internal static class NararaForcedTurnReplayBoundaryPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo wrapper = AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper));
        Type state = wrapper.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType ??
            throw new InvalidOperationException("Card OnPlayWrapper is no longer async.");
        return AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        List<CodeInstruction> code = instructions.ToList();
        FieldInfo card = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this") ??
            throw new MissingFieldException(__originalMethod.DeclaringType?.FullName, "<>4__this");
        MethodInfo continuation = AccessTools.Method(typeof(NararaForcedTurnEndHelper),
            nameof(NararaForcedTurnEndHelper.ShouldContinueCardPlay));
        int changes = 0;
        for (int index = 0; index < code.Count; index++)
        {
            CodeInstruction instruction = code[index];
            if ((instruction.opcode == OpCodes.Blt || instruction.opcode == OpCodes.Blt_S) &&
                index >= 3 && code[index - 1].operand is FieldInfo count &&
                count.Name.StartsWith("<playCount>", StringComparison.Ordinal) &&
                code[index - 3].operand is FieldInfo iteration &&
                iteration.Name.StartsWith("<i>", StringComparison.Ordinal))
            {
                // The original index/count remain on the stack. Add the card
                // and replace only the replay continuation test, so all native
                // after-play hooks, finally blocks and pile cleanup still run.
                CodeInstruction load = new(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels);
                load.blocks.AddRange(instruction.blocks);
                yield return load;
                yield return new CodeInstruction(OpCodes.Ldfld, card);
                yield return new CodeInstruction(OpCodes.Call, continuation);
                yield return new CodeInstruction(OpCodes.Brtrue, instruction.operand);
                changes++;
            }
            else yield return instruction;
        }
        if (changes != 1)
            throw new InvalidOperationException("Expected exactly one native card play-count loop.");
    }
}
