using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

// A swordheart payment may close the mode in BeforeCardPlayed. Finish that
// card, its nested automatic plays and native pile cleanup before ending its
// owner's turn. Never await the enclosing action from one of its own hooks.
internal static class MagicSwordTurnEndHelper
{
    internal sealed class PlayChain
    {
        internal Player Player;
        internal ICombatState Combat;
        internal int ActivePlays;
        internal bool Failed;
        internal TurnEndRequest Pending;
    }

    internal sealed class CardPlayScope
    {
        internal PlayChain Chain;
        internal bool Closed;
    }

    internal sealed class TurnEndRequest
    {
        internal Creature Owner;
        internal ICombatState Combat;
        internal int TurnNumber;
        internal object OverclockToken;
    }

    private static readonly ConditionalWeakTable<Player, PlayChain> Plays = new();

    // Native exposes only an all-player cancellation switch. Its queue layout
    // is shared by stable and beta: apply the same filter to this owner alone,
    // retaining executing actions, hook actions and every teammate's queue.
    private static readonly MethodInfo GetQueue = AccessTools.Method(typeof(ActionQueueSet), "GetQueue", [typeof(ulong)])
        ?? throw new MissingMethodException(typeof(ActionQueueSet).FullName, "GetQueue");
    private static readonly FieldInfo QueueActions = AccessTools.Field(GetQueue.ReturnType, "actions")
        ?? throw new MissingFieldException(GetQueue.ReturnType.FullName, "actions");
    private static readonly FieldInfo QueueCancelling = AccessTools.Field(GetQueue.ReturnType, "isCancellingPlayerDrivenCombatActions")
        ?? throw new MissingFieldException(GetQueue.ReturnType.FullName, "isCancellingPlayerDrivenCombatActions");

    internal static CardPlayScope BeginCardPlay(CardModel card)
    {
        Player player = card?.Owner;
        ICombatState combat = player?.Creature?.CombatState;
        if (player?.Character is not HbrgoSaki31O || combat == null) return null;

        if (Plays.TryGetValue(player, out PlayChain previous) && !ReferenceEquals(previous.Combat, combat))
            Plays.Remove(player);
        PlayChain chain = Plays.GetValue(player, _ => new PlayChain { Player = player, Combat = combat });
        chain.ActivePlays++;
        return new CardPlayScope { Chain = chain };
    }

    internal static void EndCardPlay(CardPlayScope scope, bool successful)
    {
        if (scope == null || scope.Closed) return;
        scope.Closed = true;
        PlayChain chain = scope.Chain;
        chain.Failed |= !successful;
        if (--chain.ActivePlays > 0) return;
        if (Plays.TryGetValue(chain.Player, out PlayChain current) && ReferenceEquals(chain, current))
            Plays.Remove(chain.Player);
        if (!chain.Failed && chain.Pending != null) EndCurrentTurn(chain.Pending);
    }

    // Call after the entire exit/finisher chain has completed. If an original
    // card is still resolving, its outermost successful wrapper flushes this.
    internal static void Request(Creature owner)
    {
        if (!CanEnd(owner)) return;
        Player player = owner.Player;
        var request = new TurnEndRequest
        {
            Owner = owner,
            Combat = owner.CombatState,
            TurnNumber = player.PlayerCombatState.TurnNumber,
            OverclockToken = OverclockImmediateTurnCoordinator.GetTurnToken(player)
        };
        if (Plays.TryGetValue(player, out PlayChain chain) && chain.ActivePlays > 0 &&
            ReferenceEquals(chain.Combat, request.Combat))
        {
            chain.Pending = request;
            return;
        }
        EndCurrentTurn(request);
    }

    private static bool CanEnd(Creature owner)
    {
        Player player = owner?.Player;
        CombatManager manager = CombatManager.Instance;
        ICombatState combat = owner?.CombatState;
        return player?.Character is HbrgoSaki31O && owner.IsAlive && combat != null &&
            player.PlayerCombatState?.Phase == PlayerTurnPhase.Play && combat.CurrentSide == CombatSide.Player &&
            manager.IsInProgress && !manager.IsOverOrEnding && !manager.EndingPlayerTurnPhaseOne &&
            !manager.EndingPlayerTurnPhaseTwo && ReferenceEquals(manager.DebugOnlyGetState(), combat) &&
            manager.IsPartOfPlayerTurn(player) && !manager.IsPlayerReadyToEndTurn(player);
    }

    private static void EndCurrentTurn(TurnEndRequest request)
    {
        Creature owner = request.Owner;
        Player player = owner.Player;
        if (!CanEnd(owner) || !ReferenceEquals(owner.CombatState, request.Combat) ||
            player.PlayerCombatState.TurnNumber != request.TurnNumber ||
            !ReferenceEquals(OverclockImmediateTurnCoordinator.GetTurnToken(player), request.OverclockToken)) return;

        ActionQueueSet queues = RunManager.Instance.ActionQueueSet;
        object queue = GetQueue.Invoke(queues, [player.NetId]);
        bool wasCancelling = (bool)QueueCancelling.GetValue(queue);
        try
        {
            CancelPendingPlayerActions(queue);
            // Native waits for player-driven actions itself; the scope above
            // additionally covers standalone automatic cards. OD's existing
            // end-turn coordinator receives the normal ready vote unchanged.
            PlayerCmd.EndTurn(player, canBackOut: false);
        }
        catch
        {
            QueueCancelling.SetValue(queue, wasCancelling);
            throw;
        }
    }

    private static void CancelPendingPlayerActions(object queue)
    {
        QueueCancelling.SetValue(queue, true);
        var actions = (List<GameAction>)QueueActions.GetValue(queue);
        for (int index = 0; index < actions.Count; index++)
        {
            GameAction action = actions[index];
            if (!ActionQueueSet.IsGameActionPlayerDriven(action) ||
                action.ActionType is GameActionType.NonCombat or GameActionType.Any ||
                action.State != GameActionState.WaitingForExecution) continue;
            action.Cancel();
            actions.RemoveAt(index--);
        }
    }
}
