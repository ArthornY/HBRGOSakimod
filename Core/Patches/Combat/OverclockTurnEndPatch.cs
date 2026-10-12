using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CombatManager), "AfterAllPlayersReadyToEndTurn")]
internal static class OverclockTurnEndPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (!OverclockImmediateTurnCoordinator.TryEnd(out Task task)) return true;
        __result = task;
        return false;
    }
}

// Beta awaits a one-shot end-turn signal before entering phase one. Consuming
// that signal for OD would strand the ordinary turn on the enemy-phase signal.
// Keep the ready votes/UI, but deliver only ordinary turn ends to that signal.
// Stable has no matching call, so its original SetReady method is untouched.
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
internal static class OverclockTurnEndSignalPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo wrapper = AccessTools.Method(typeof(OverclockTurnEndSignalPatch), nameof(TrySignalTurnEnd));
        foreach (CodeInstruction instruction in instructions)
        {
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                instruction.operand is MethodInfo method && method.Name == "TrySetResult" &&
                method.DeclaringType is { IsGenericType: true } sourceType &&
                sourceType.GetGenericTypeDefinition() == typeof(TaskCompletionSource<>) &&
                sourceType.GenericTypeArguments[0].Name == "EndTurnSignal")
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = wrapper.MakeGenericMethod(sourceType.GenericTypeArguments[0]);
            }
            yield return instruction;
        }
    }

    private static bool TrySignalTurnEnd<T>(TaskCompletionSource<T> source, T signal)
    {
        if (!OverclockImmediateTurnCoordinator.TryEnd(out Task task)) return source.TrySetResult(signal);
        TaskHelper.RunSafely(task);
        return false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class OverclockResetPatch
{
    private static void Prefix() => OverclockImmediateTurnCoordinator.Clear();
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.HandlePlayerDeath))]
internal static class OverclockPlayerDeathPatch
{
    private static void Prefix(object[] __args)
    {
        // Beta prepends a nullable CombatId; stable starts with Player.
        foreach (object argument in __args)
            if (argument is Player player)
            {
                OverclockImmediateTurnCoordinator.OnPlayerDeath(player);
                break;
            }
    }
}
