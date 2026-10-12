using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class NararaShibaCombatStartupPatch
{
    private static bool Prepare() => typeof(CombatManager).Assembly.GetType(
        "MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer") == null;

    private static MethodBase TargetMethod()
    {
        // Beta already drains the unpaused queue before installing starting
        // powers. Stable can otherwise wait forever on its own paused queue.
        MethodInfo startup = AccessTools.DeclaredMethod(
            typeof(CombatManager), nameof(CombatManager.StartCombatInternal), Type.EmptyTypes);
        if (startup == null || startup.IsStatic || startup.ReturnType != typeof(Task))
            throw new MissingMethodException(typeof(CombatManager).FullName, "Task StartCombatInternal()");

        AsyncStateMachineAttribute stateMachine = startup.GetCustomAttribute<AsyncStateMachineAttribute>()
            ?? throw new InvalidOperationException("CombatManager.StartCombatInternal has no async state machine.");
        return AccessTools.DeclaredMethod(stateMachine.StateMachineType, "MoveNext", Type.EmptyTypes)
            ?? throw new MissingMethodException(stateMachine.StateMachineType.FullName, "MoveNext()");
    }

    private static Task WaitForNararaStartup(Task originalTask)
    {
        if (CombatManager.Instance.DebugOnlyGetState()?.Encounter is not NararaShibaBoss ||
            RunManager.Instance?.ActionExecutor is not { } executor)
            return originalTask;

        return DrainNararaStartupQueue(originalTask, executor);
    }

    private static async Task DrainNararaStartupQueue(Task originalTask, ActionExecutor executor)
    {
        if (executor.IsPaused && executor.IsRunning)
            Log.Info("[HBRGOSakiMod] Resuming paused Narara startup queue; action=" +
                (executor.CurrentlyRunningAction?.GetType().Name ?? "pending"));

        // Keep the native queue-completion task and its exceptions. Restore the
        // pause before native AfterCreatureAdded and combat-start hooks continue.
        executor.Unpause();
        try
        {
            await originalTask;
        }
        finally
        {
            executor.Pause();
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo wait = AccessTools.DeclaredMethod(
            typeof(ActionExecutor), nameof(ActionExecutor.FinishedExecutingActions), Type.EmptyTypes)
            ?? throw new MissingMethodException(typeof(ActionExecutor).FullName, "FinishedExecutingActions()");
        MethodInfo helper = AccessTools.DeclaredMethod(
            typeof(NararaShibaCombatStartupPatch), nameof(WaitForNararaStartup), new[] { typeof(Task) })
            ?? throw new MissingMethodException(typeof(NararaShibaCombatStartupPatch).FullName, nameof(WaitForNararaStartup));

        List<CodeInstruction> result = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
        int[] matches = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.Calls(wait))
            .Select(item => item.index)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Expected one ActionExecutor.FinishedExecutingActions call in CombatManager.StartCombatInternal; found {matches.Length}.");

        // Insert after the native Task producer, retaining its branch labels,
        // exception boundaries and the original GetAwaiter/continuation path.
        result.Insert(matches[0] + 1, new CodeInstruction(OpCodes.Call, helper));
        return result;
    }
}
