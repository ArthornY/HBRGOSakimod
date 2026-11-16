using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

// The teammate's queue is never paused, cancelled, reordered or restored.
// Any lifecycle commands and native hook choices still run for this owner.
internal sealed class PersonalTurnQueueGuard
{
    private static readonly MethodInfo GetQueue = AccessTools.Method(typeof(ActionQueueSet), "GetQueue", [typeof(ulong)]);
    private static readonly FieldInfo Actions = AccessTools.Field(GetQueue.ReturnType, "actions");
    private static readonly FieldInfo Paused = AccessTools.Field(GetQueue.ReturnType, "isPaused");
    private static readonly FieldInfo Cancelling = AccessTools.Field(GetQueue.ReturnType, "isCancellingPlayerDrivenCombatActions");
    private readonly object _queue;
    private readonly bool _wasPaused;
    private readonly bool _wasCancelling;

    internal PersonalTurnQueueGuard(ActionQueueSet queues, ulong owner)
    {
        _queue = GetQueue.Invoke(queues, [owner]);
        _wasPaused = (bool)Paused.GetValue(_queue);
        _wasCancelling = (bool)Cancelling.GetValue(_queue);
    }

    internal void Lock()
    {
        Paused.SetValue(_queue, true);
        BlockManual();
    }

    internal void BlockManual()
    {
        Cancelling.SetValue(_queue, true);
        var actions = (List<GameAction>)Actions.GetValue(_queue);
        for (int index = 0; index < actions.Count; index++)
        {
            GameAction action = actions[index];
            if (!ActionQueueSet.IsGameActionPlayerDriven(action) ||
                action.ActionType is GameActionType.Any or GameActionType.NonCombat ||
                action.State != GameActionState.WaitingForExecution) continue;
            action.Cancel();
            actions.RemoveAt(index--);
        }
    }

    internal void AllowPlay()
    {
        Paused.SetValue(_queue, false);
        Cancelling.SetValue(_queue, false);
    }

    internal void Restore()
    {
        Paused.SetValue(_queue, _wasPaused);
        Cancelling.SetValue(_queue, _wasCancelling);
    }
}
