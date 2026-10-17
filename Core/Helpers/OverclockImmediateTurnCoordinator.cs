using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Models.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

// An inserted OD phase suspends the ordinary turn. Never call native StartTurn:
// it clears block, advances counters and cannot be undone by restoring a hand.
internal static class OverclockImmediateTurnCoordinator
{
    private enum Phase { Entering, Playing, Leaving }

    private sealed class Session
    {
        internal readonly Overclock Relic;
        internal readonly ICombatState Combat;
        internal readonly Player[] Players;
        internal readonly Dictionary<Player, OverclockSuspendedHand> Hands = new();
        internal readonly Dictionary<Player, bool> Ready = new();
        internal readonly Dictionary<NararaNullificationPower, int> NullificationSelections = new();
        internal Phase Phase;
        internal bool EndRequested;
        internal Task Transition = Task.CompletedTask;

        internal Session(Overclock relic, ICombatState combat, Player[] players)
        {
            Relic = relic;
            Combat = combat;
            Players = players;
        }
    }

    private static Session _current;
    private sealed class ResistanceReward
    {
        internal bool Pending;
        internal bool Canceled;
    }
    private static readonly ConditionalWeakTable<ICombatState, ResistanceReward> ResistanceRewards = new();
    private static bool _lifecycleRegistered;
    private static readonly FieldInfo QueuesField = AccessTools.Field(typeof(ActionQueueSet), "_actionQueues");
    private static readonly FieldInfo ActionsField = QueuesField == null ? null :
        AccessTools.Field(QueuesField.FieldType.GetGenericArguments()[0], "actions");

    internal static void EnsureLifecycle()
    {
        if (_lifecycleRegistered) return;
        _lifecycleRegistered = true;
        // Loss does not invoke AfterCombatEnd model hooks in the base game.
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    private static void OnCombatEnded(CombatRoom _) => Clear();

    internal static bool IsActiveFor(Player player) =>
        _current is { } session && IsCurrent(session) &&
        session.Players.Contains(player) && player.Creature.IsAlive;

    internal static object GetTurnToken(Player player) => IsActiveFor(player) ? _current : null;

    internal static void RequestResistanceReward(Player player)
    {
        ICombatState combat = player?.Creature?.CombatState;
        if (combat == null || player.Creature.IsAlive != true) return;
        if (IsActiveFor(player))
        {
            _current.Relic.PromoteRewardLevel(2);
            return;
        }
        ResistanceReward reward = ResistanceRewards.GetValue(combat, static _ => new());
        if (reward.Pending || reward.Canceled) return;
        reward.Pending = true;
        TaskHelper.RunSafely(ActivateResistanceReward(player, combat, reward, GetCurrentActionCompletion()));
    }

    private static Task GetCurrentActionCompletion() =>
        RunManager.Instance.ActionExecutor.CurrentlyRunningAction?.CompletionTask;

    private static async Task ActivateResistanceReward(Player player, ICombatState combat,
        ResistanceReward reward, Task action)
    {
        try
        {
            // Do not pause or detach hands while the causing card can still
            // await another automatic play's WaitForUnpause.
            while (true)
            {
                if (action != null) await action;
                await Task.Yield();
                CombatManager manager = CombatManager.Instance;
                if (reward.Canceled || !manager.IsInProgress || manager.IsOverOrEnding ||
                    !ReferenceEquals(manager.DebugOnlyGetState(), combat) ||
                    !ReferenceEquals(player.Creature.CombatState, combat) || !player.Creature.IsAlive) return;
                if (IsActiveFor(player)) { _current.Relic.PromoteRewardLevel(2); return; }
                Overclock relic = player.GetRelic<Overclock>();
                if (relic == null || relic.ActiveLevel > 0) return;
                if (relic.TryActivateRewardLevel(2)) return;
                // Another queued card may have begun before the completed
                // action's continuation. Wait its real completion, without
                // polling or weakening manual OD admission safeguards.
                Task next = GetCurrentActionCompletion();
                if (next == null || next.IsCompleted || ReferenceEquals(next, action)) return;
                action = next;
            }
        }
        finally { reward.Pending = false; }
    }

    internal static bool CanBegin(Player owner)
    {
        CombatManager manager = CombatManager.Instance;
        ICombatState combat = owner?.Creature.CombatState;
        return _current == null && combat != null && manager.IsInProgress &&
            !manager.IsOverOrEnding && !manager.EndingPlayerTurnPhaseOne &&
            !manager.EndingPlayerTurnPhaseTwo &&
            ReferenceEquals(manager.DebugOnlyGetState(), combat) &&
            combat.CurrentSide == CombatSide.Player &&
            owner.Creature.IsAlive &&
            !combat.Players.All(manager.IsPlayerReadyToEndTurn) &&
            !HasPendingChoice() &&
            !combat.Players.Any(manager.IsExecutingCardOrPotionEffect) &&
            // Native extra turns also have an idle Play phase. OD suspends
            // that phase just like an ordinary turn; the extra-turn list is
            // not a pending effect. Ready votes are suspended with the hand,
            // but every living teammate must remain in an idle Play phase.
            combat.Players.Where(player => player.Creature.IsAlive)
                .All(player => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play);
    }

    // Called only by the existing synchronized OD console action.
    internal static bool TryBegin(Overclock relic)
    {
        if (!CanBegin(relic.Owner)) return false;
        EnsureLifecycle();
        CombatManager manager = CombatManager.Instance;
        ICombatState combat = relic.Owner.Creature.CombatState;
        Player[] players = combat.Players
            .Where(player => player.Creature.IsAlive && player.Creature.Side == relic.Owner.Creature.Side)
            .ToArray();
        var session = new Session(relic, combat, players);
        // Cancel queued hand animations before removing their model cards.
        // All admission checks above depend on synchronized state, never UI.
        PauseInput();
        try
        {
            foreach (Player player in players)
            {
                session.Ready.Add(player, manager.IsPlayerReadyToEndTurn(player));
                if (!OverclockSuspendedHand.TrySuspend(player, out OverclockSuspendedHand hand))
                {
                    foreach (OverclockSuspendedHand saved in session.Hands.Values) saved.TryRestore();
                    ResumeInput();
                    return false;
                }
                session.Hands.Add(player, hand);
            }
        }
        catch (Exception error)
        {
            // A native pile notification can fail after an earlier teammate's
            // piles were suspended. Roll every completed snapshot back too.
            try
            {
                foreach (OverclockSuspendedHand saved in session.Hands.Values) saved.TryRestore();
            }
            finally { ResumeInput(); }
            Log.Warn($"[HBRGOSakiMod] OD deck suspension failed: {error.Message}");
            // The activating relic resets ActiveLevel on a declined admission;
            // throwing here would leave it marked active without any session.
            return false;
        }
        _current = session;
        foreach (Player player in players)
        {
            player.PlayerCombatState.Phase = PlayerTurnPhase.Start;
            manager.UndoReadyToEndTurn(player);
        }
        manager.OnEndedTurnLocally();

        // A draw hook can request a choice. Finish the activating action first,
        // otherwise its generated hook action would be waiting on its own queue.
        GameAction running = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
        session.Transition = TaskHelper.RunSafely(Enter(session, running?.CompletionTask));
        return true;
    }

    private static async Task Enter(Session session, Task activatingAction)
    {
        try
        {
            if (activatingAction != null) await activatingAction;
            // CompletionTask signals before AfterFinished removes the action.
            await Task.Yield();
            if (!await CanContinue(session)) return;
            // Populate every participating player's independent OD deck before
            // any draw/cannon hook can affect a teammate's still-empty piles.
            var enteredCards = new List<CardModel>();
            foreach (Player player in session.Players)
            {
                if (!player.Creature.IsAlive) continue;
                enteredCards.AddRange(session.Hands[player].PrepareOverclockDeck());
            }
            // These are new combat copies, so they must receive the same entry
            // hooks as other generated cards. Vital Spark and Galvanic attach
            // their afflictions here; their normal play hooks then resolve them.
            // Notify only the OD pool, never the suspended ordinary cards, and
            // finish all teammates' deck setup before any listener can draw.
            foreach (CardModel card in enteredCards)
            {
                if (!await CanContinue(session)) return;
                if (card.HasBeenRemovedFromState || card.Owner?.Creature.IsAlive != true ||
                    !ReferenceEquals(card.Owner.Creature.CombatState, session.Combat)) continue;
                await Hook.AfterCardEnteredCombat(session.Combat, card);
            }
            if (!await CanContinue(session)) return;
            // OD is its own player turn: suspend the ordinary turn's shared
            // attribute restrictions and draw a fresh pair before drawing cards.
            foreach (var creature in session.Combat.Enemies)
                if (creature.GetPower<NararaNullificationPower>() is { IsActive: true } power)
                {
                    session.NullificationSelections.Add(power, power.CaptureSelections());
                    power.SelectForPlayers(session.Players);
                }
            foreach (Player player in session.Players)
            {
                if (!await CanContinue(session)) return;
                if (!player.Creature.IsAlive) continue;
                var context = CreateContext(player);
                Task task = DealOverclockHand(session, player, context);
                await context.AssignTaskAndWaitForPauseOrCompletion(task);
                await context.WaitForCompletion();
            }
            if (!await CanContinue(session)) return;
            await OverclockAllyActionHelper.PerformEntryActions(session.Players);
            if (!await CanContinue(session)) return;
            session.Phase = Phase.Playing;
            foreach (Player player in session.Players)
                if (player.Creature.IsAlive) player.PlayerCombatState.Phase = PlayerTurnPhase.Play;
            if (session.EndRequested) await Leave(session, null);
            else ResumeInput();
        }
        catch
        {
            RecoverAfterFailure(session);
            throw;
        }
    }

    private static async Task DealOverclockHand(Session session, Player player, PlayerChoiceContext context)
    {
        await PlayerCmd.SetEnergy(Overclock.GetExtraTurnEnergy(session.Relic.ActiveLevel), player);
        if (!await CanContinue(session) || !player.Creature.IsAlive) return;
        // OD has its own cannon turn boundaries without replaying native turn
        // setup: keep block and the suspended turn's other ability rules intact.
        if (player.GetRelic<Millsage>() is { } millsage)
            await millsage.TriggerOverclockTurnStart(context, session);
        if (!await CanContinue(session) || !player.Creature.IsAlive) return;
        await CardPileCmd.Draw(context, Overclock.GetExtraTurnDraw(session.Relic.ActiveLevel), player, fromHandDraw: true);
    }

    // Keep native ready/undo events and votes; replace only the side transition.
    internal static bool TryEnd(out Task task)
    {
        task = Task.CompletedTask;
        Session session = _current;
        if (session == null || !IsCurrent(session)) return false;
        session.EndRequested = true;
        if (session.Phase != Phase.Playing) return true;
        session.Phase = Phase.Leaving;
        PauseInput();
        GameAction running = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
        task = session.Transition = Leave(session, running?.CompletionTask);
        return true;
    }

    private static async Task Leave(Session session, Task endingAction)
    {
        try
        {
            session.Phase = Phase.Leaving;
            PauseInput();
            if (endingAction != null) await endingAction;
            await Task.Yield();
            if (!await CanContinue(session)) return;
            foreach (Player player in session.Players)
            {
                if (!await CanContinue(session)) return;
                if (!player.Creature.IsAlive) continue;
                player.PlayerCombatState.Phase = PlayerTurnPhase.End;
                var context = CreateContext(player);
                Task task = FlushOverclockHand(session, player, context);
                await context.AssignTaskAndWaitForPauseOrCompletion(task);
                await context.WaitForCompletion();
            }
            if (await CanContinue(session)) Restore(session);
        }
        catch
        {
            RecoverAfterFailure(session);
            throw;
        }
    }

    private static async Task FlushOverclockHand(Session session, Player player, PlayerChoiceContext context)
    {
        // Resolve END cannons against the OD hand before its cleanup. Any END
        // draw belongs to this extra turn, not the suspended ordinary hand.
        if (player.GetRelic<Millsage>() is { } millsage)
            await millsage.TriggerOverclockTurnEnd(context, session);
        if (!await CanContinue(session) || !player.Creature.IsAlive) return;
        // Suspended cards neither exhaust nor lose their turn-local modifiers.
        // OD's unplayed hand is discarded before restoring the original hand.
        CardPile hand = player.PlayerCombatState.Hand;
        foreach (CardModel card in hand.Cards.ToArray())
        {
            if (!IsCurrent(session) || !player.Creature.IsAlive) return;
            if (card.Pile != hand) continue;
            if (card.HasTurnEndInHandEffect)
                await card.OnTurnEndInHandWrapper(context);
            else if (card.Keywords.Contains(CardKeyword.Ethereal) && Hook.ShouldEtherealTrigger(session.Combat, card))
                await OverclockCardCommandHelper.ExhaustEthereal(context, card);
        }
        if (IsCurrent(session) && player.Creature.IsAlive && !hand.IsEmpty)
            await CardPileCmd.Add(hand.Cards.ToArray(), PileType.Discard);
        // Hand cleanup may auto-play another copy after the cannon END phase.
        // Its temporary effect still belongs to this inserted turn.
        await HotaruSeeYouAgainHelper.OnTurnEnd(context, player, session);
        await HotaruEchoLocationPower.TurnEnd(context, player, session);
        await HotaruCommonCannonHelper.OnTurnEnd(context, player, session);
        await HotaruUncommonCardHelper.OnTurnEnd(context, player, session);
        await HotaruUncommonCannonHelper.OnTurnEnd(context, player, session);
        await HotaruRareRollHelper.OnTurnEnd(context, player, session);
        await HotaruRareCannonHelper.OnTurnEnd(context, player, session);
    }

    private static void Restore(Session session)
    {
        CombatManager manager = CombatManager.Instance;
        foreach (var selection in session.NullificationSelections)
            if (selection.Key.IsActive && ReferenceEquals(selection.Key.Owner.CombatState, session.Combat))
                selection.Key.RestoreSelections(selection.Value);
        session.NullificationSelections.Clear();
        foreach (Player player in session.Players)
        {
            if (!player.Creature.IsAlive)
            {
                session.Hands[player].Abandon();
                continue;
            }
            // The snapshot disposes the independent OD pool directly before
            // restoring all original piles. Do not flush here: failed deck
            // preparation may already have restored this player's real hand.
            session.Hands[player].TryRestore();
            player.PlayerCombatState.Phase = PlayerTurnPhase.Play;
            manager.UndoReadyToEndTurn(player);
        }
        session.Relic.FinishImmediateTurn();
        // Suppress native side transitions while restoring original votes.
        foreach (Player player in session.Players)
            if (player.Creature.IsAlive && session.Ready[player])
                manager.SetReadyToEndTurn(player, canBackOut: true);
        _current = null;
        ResumeInput();
        // If the initiator died, every remaining player may already have voted
        // to end the suspended ordinary turn. Reissue its transition once, now
        // that it can no longer be mistaken for ending OD.
        if (session.Combat.Players.All(manager.IsPlayerReadyToEndTurn))
        {
            Player readyPlayer = session.Players.FirstOrDefault(player => player.Creature.IsAlive);
            if (readyPlayer != null)
            {
                manager.UndoReadyToEndTurn(readyPlayer);
                PlayerCmd.EndTurn(readyPlayer, canBackOut: false);
            }
        }
    }

    private static void RecoverAfterFailure(Session session)
    {
        if (IsCurrent(session)) Restore(session);
        else if (ReferenceEquals(_current, session)) Clear();
    }

    internal static void OnPlayerDeath(Player player)
    {
        // Death cleanup cannot see a hand detached from the five native piles.
        // Dispose it now, so revival inside this same OD cannot restore it.
        if (_current is { } session &&
            ReferenceEquals(player?.Creature.CombatState, session.Combat) &&
            session.Hands.TryGetValue(player, out OverclockSuspendedHand hand))
            hand.Abandon();
    }

    internal static void Clear()
    {
        Session session = _current;
        _current = null;
        ICombatState combat = session?.Combat ?? CombatManager.Instance.DebugOnlyGetState();
        if (combat != null && ResistanceRewards.TryGetValue(combat, out ResistanceReward reward))
        {
            reward.Canceled = true;
            reward.Pending = false;
            ResistanceRewards.Remove(combat);
        }
        if (session != null)
        {
            foreach (OverclockSuspendedHand hand in session.Hands.Values) hand.Abandon();
            session.Relic.ResetCombatCharge();
            if (ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), session.Combat))
                RunManager.Instance.ActionQueueSet.UnpauseAllPlayerQueues();
        }
        if (combat != null)
            foreach (Player player in combat.Players)
                player.GetRelic<Overclock>()?.ResetCombatCharge();
    }

    private static bool IsCurrent(Session session) =>
        ReferenceEquals(_current, session) && CombatManager.Instance.IsInProgress &&
        !CombatManager.Instance.IsOverOrEnding &&
        ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), session.Combat);

    private static async Task<bool> CanContinue(Session session)
    {
        CombatManager manager = CombatManager.Instance;
        if (!ReferenceEquals(_current, session) || !manager.IsInProgress ||
            !ReferenceEquals(manager.DebugOnlyGetState(), session.Combat)) return false;
        // An entry action or a Burn can end combat while queues are paused.
        // IsOverOrEnding alone is not cleanup: complete the native win/loss
        // checkpoint after the whole effect, before abandoning this transition.
        if (manager.IsOverOrEnding) await manager.CheckWinCondition();
        return IsCurrent(session);
    }

    private static HookPlayerChoiceContext CreateContext(Player player) =>
        new(player, LocalContext.NetId ?? player.NetId, GameActionType.Combat);

    private static bool HasPendingChoice()
    {
        // Other players' automatic hooks can be waiting for a choice while the
        // activating console action runs. Card/potion effect depth misses these.
        if (QueuesField == null || ActionsField == null) return true;
        if (QueuesField.GetValue(RunManager.Instance.ActionQueueSet) is not IEnumerable queues) return true;
        foreach (object queue in queues)
            if (ActionsField.GetValue(queue) is IEnumerable<GameAction> actions)
                foreach (GameAction action in actions)
                    if (action.State is GameActionState.GatheringPlayerChoice or GameActionState.ReadyToResumeExecuting ||
                        (action is GenericHookGameAction && action.State is not (GameActionState.Finished or GameActionState.Canceled)))
                        return true;
        return false;
    }

    private static void PauseInput()
    {
        RunManager.Instance.ActionQueueSet.PauseAllPlayerQueues();
        RunManager.Instance.ActionQueueSet.StartCancellingAllPlayerDrivenCombatActions();
        CombatManager.Instance.OnEndedTurnLocally();
    }

    private static void ResumeInput()
    {
        RunManager.Instance.ActionQueueSet.UnpauseAllPlayerQueues();
        Player me = LocalContext.GetMe(CombatManager.Instance.DebugOnlyGetState());
        if (me != null && me.Creature.IsAlive && !CombatManager.Instance.IsPlayerReadyToEndTurn(me))
            CombatManager.Instance.UndoReadyToEndTurn(me);
        else
            CombatManager.Instance.OnEndedTurnLocally();
    }
}
