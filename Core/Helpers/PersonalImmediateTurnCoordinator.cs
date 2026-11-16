using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

// Unlike OD, this session borrows only one player's piles, energy and vote.
// Teammates keep playing their ordinary turns. Every mutation of entry/exit
// runs in a recorded managed action; completion continuations only request it.
internal static class PersonalImmediateTurnCoordinator
{
    internal enum TurnPhase { Entering, Playing, Leaving }
    internal sealed class Session
    {
        internal Player Owner;
        internal ICombatState Combat;
        internal PersonalTurnTicket<Player> Ticket;
        internal OverclockSuspendedHand Hand;
        internal PersonalTurnQueueGuard Queue;
        internal PersonalTurnReadyGuard Ready;
        internal bool SavedReady;
        internal PlayerTurnPhase SavedPhase;
        internal TurnPhase Phase;
        internal bool EndRequested;
    }

    private static readonly PersonalTurnSchedule<Player> Schedule = new();
    private static ICombatState _combat;
    private static Session _current;
    private static ActionExecutor _executor;
    private static bool _lifecycle;
    private static bool _restoringReady;
    private static bool _ordinaryEndDeferred;
    private static PersonalTurnTicket<Player> _beginSent;
    private static PersonalTurnTicket<Player> _finishSent;
    private static PersonalTurnQueueGuard _admissionGuard;
    private static Player _admissionOwner;

    internal static bool IsBusy => Schedule.IsBusy;
    internal static bool IsActiveFor(Player player) => _current is { } session &&
        ReferenceEquals(session.Owner, player) && IsCurrent(session) && player.Creature.IsAlive;
    internal static object GetTurnToken(Player player) => IsActiveFor(player) ? _current : null;
    internal static string RoomKey => _combat?.RunState is { } run
        ? MemoryGardenUpgradePointsHelper.GetRoomKey(run, run.CurrentRoom) : string.Empty;

    internal static void EnsureLifecycle()
    {
        HbrPersonalTurnNetActions.Initialize();
        if (!_lifecycle)
        {
            _lifecycle = true;
            CombatManager.Instance.CombatEnded += _ => Clear();
            CombatManager.Instance.TurnStarted += _ => TryRequestNextBegin();
        }
        ActionExecutor executor = RunManager.Instance?.ActionExecutor;
        if (ReferenceEquals(executor, _executor)) return;
        if (_executor != null) _executor.AfterActionExecuted -= AfterAction;
        _executor = executor;
        if (_executor != null) _executor.AfterActionExecuted += AfterAction;
    }

    internal static void Request(Player owner, int count)
    {
        if (owner?.Creature is not { IsAlive: true, CombatState: { } combat } || count <= 0 ||
            !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding) return;
        EnsureLifecycle();
        if (!ReferenceEquals(_combat, combat)) { Clear(); _combat = combat; }
        // This call is inside the source card/power command on every peer, so
        // pending is visible BEFORE any native all-ready/one-shot signal.
        Schedule.Request(owner, count);
        PrepareAdmission();
        GameAction action = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
        if (action == null || action.CompletionTask.IsCompleted) TryRequestNextBegin();
        // AfterAction is the normal admission point; no local continuation may
        // detach a hand or consume shuffle RNG ahead of a teammate's command.
    }

    private static void AfterAction(GameAction _) { TryRequestNextBegin(); TryRequestFinish(); }

    internal static void OnOverclockRestored() { PrepareAdmission(); TryRequestNextBegin(); }

    private static void PrepareAdmission()
    {
        if (_current != null || OverclockImmediateTurnCoordinator.HasSession || !LiveCombat()) return;
        Schedule.Prune(player => player?.Creature.IsAlive == true && ReferenceEquals(player.Creature.CombatState, _combat));
        Player owner = Schedule.Next?.Owner;
        if (ReferenceEquals(owner, _admissionOwner)) return;
        _admissionGuard?.Restore();
        _admissionGuard = null;
        _admissionOwner = owner;
        if (owner == null) return;
        _admissionGuard = new PersonalTurnQueueGuard(RunManager.Instance.ActionQueueSet, owner.NetId);
        // Source-command mutation on EVERY peer, before posting a begin action.
        // Block new ordinary inputs but let a current choice resume normally.
        _admissionGuard.BlockManual();
    }

    private static void TryRequestNextBegin()
    {
        if (_current != null || OverclockImmediateTurnCoordinator.HasSession || !LiveCombat()) return;
        PrepareAdmission();
        Schedule.Prune(player => player?.Creature.IsAlive == true && ReferenceEquals(player.Creature.CombatState, _combat));
        PersonalTurnTicket<Player> next = Schedule.Next;
        if (next == null) { ReleaseOrdinaryEnd(); return; }
        if (Equals(_beginSent, next) || !LocalContext.IsMe(next.Owner) ||
            RunManager.Instance.NetService?.Type == NetGameType.Replay) return;
        if (HbrPersonalTurnNetActions.RequestBegin(next, RoomKey)) _beginSent = next;
    }

    internal static async Task Begin(PlayerChoiceContext context, Player owner,
        long requestId, int ordinal, string roomKey)
    {
        _beginSent = null;
        if (!LiveCombat() || _current != null || OverclockImmediateTurnCoordinator.HasSession ||
            roomKey != RoomKey || !ReferenceEquals(owner?.Creature.CombatState, _combat) || !owner.Creature.IsAlive ||
            !Schedule.TryBegin(requestId, ordinal, owner)) return;
        CombatManager manager = CombatManager.Instance;
        var session = new Session
        {
            Owner = owner, Combat = _combat, Ticket = Schedule.Active,
            SavedReady = manager.IsPlayerReadyToEndTurn(owner), SavedPhase = owner.PlayerCombatState.Phase,
            Ready = new PersonalTurnReadyGuard(manager, owner),
            Queue = ReferenceEquals(_admissionOwner, owner) ? _admissionGuard :
                new PersonalTurnQueueGuard(RunManager.Instance.ActionQueueSet, owner.NetId), Phase = TurnPhase.Entering
        };
        _admissionOwner = null;
        _admissionGuard = null;
        _current = session;
        bool entered = false;
        bool failed = false;
        try
        {
            session.Queue.Lock();
            if (!OverclockSuspendedHand.TrySuspend(owner, out session.Hand)) return;
            owner.PlayerCombatState.Phase = PlayerTurnPhase.Start;
            session.Ready.UndoReady();
            CardModel[] cards = session.Hand.PrepareOverclockDeck();
            foreach (CardModel card in cards)
            {
                if (!IsCurrent(session)) return;
                if (!card.HasBeenRemovedFromState) await Hook.AfterCardEnteredCombat(session.Combat, card);
            }
            await PersonalTurnBoundaryHelper.Start(context, owner);
            if (!IsCurrent(session)) return;
            await PlayerCmd.SetEnergy(2 + MagicSwordModePower.GetTurnEnergyBonus(owner), owner);
            await HbrInsertedTurnStartHelper.Start(context, owner);
            if (!IsCurrent(session)) return;
            if (owner.GetRelic<Millsage>() is { } millsage)
                await millsage.TriggerOverclockTurnStart(context, session);
            if (!IsCurrent(session)) return;
            await CardPileCmd.Draw(context, 3, owner, fromHandDraw: true);
            await MoreThanOneTruthCombatHelper.OnExtraTurnEntered(context, owner);
            await OverclockAllyActionHelper.PerformEntryActions(new[] { owner });
            if (!IsCurrent(session)) return;
            session.Phase = TurnPhase.Playing;
            owner.PlayerCombatState.Phase = PlayerTurnPhase.Play;
            session.Queue.AllowPlay();
            if (LocalContext.IsMe(owner)) session.Ready.UndoReady();
            PersonalTurnPresentation.Show(owner);
            entered = true;
            TryRequestFinish();
        }
        catch { failed = true; throw; }
        finally
        {
            if (!entered && ReferenceEquals(_current, session))
            {
                try { Restore(session); }
                catch when (failed) { /* Preserve the original entry failure. */ }
            }
        }
    }

    // The native EndPlayerTurnAction is synchronized already. Intercept only
    // the participant before native votes/signals; peer votes remain native.
    internal static bool TryHandleEndTurn(Player player, bool canBackOut)
    {
        Session session = _current;
        if (_restoringReady || session == null || !ReferenceEquals(player, session.Owner) ||
            !player.Creature.IsAlive || !IsCurrent(session)) return false;
        session.EndRequested = true;
        session.Queue?.Lock();
        TryRequestFinish();
        return true;
    }

    private static void TryRequestFinish()
    {
        Session session = _current;
        if (session is not { Phase: TurnPhase.Playing, EndRequested: true } || !IsCurrent(session) ||
            Equals(_finishSent, session.Ticket) || !LocalContext.IsMe(session.Owner) ||
            RunManager.Instance.NetService?.Type == NetGameType.Replay) return;
        if (HbrPersonalTurnNetActions.RequestFinish(session.Ticket, RoomKey)) _finishSent = session.Ticket;
    }

    internal static async Task Finish(PlayerChoiceContext context, Player owner,
        long requestId, int ordinal, string roomKey)
    {
        _finishSent = null;
        Session session = _current;
        if (session == null || !ReferenceEquals(owner, session.Owner) || roomKey != RoomKey ||
            session.Ticket.RequestId != requestId || session.Ticket.Ordinal != ordinal ||
            session.Phase != TurnPhase.Playing) return;
        session.Phase = TurnPhase.Leaving;
        bool failed = false;
        try
        {
            session.Queue.Lock();
            owner.PlayerCombatState.Phase = PlayerTurnPhase.End;
            if (IsCurrent(session)) await PersonalTurnBoundaryHelper.Flush(context, owner, session);
        }
        catch { failed = true; throw; }
        finally
        {
            if (ReferenceEquals(_current, session))
            {
                YanagiButlerCombatHelper.OnTurnEnd(owner, session);
                VritikaCalculationCombatHelper.OnTurnEnd(owner, session);
                try { Restore(session); }
                catch when (failed) { /* Preserve the original boundary failure. */ }
            }
        }
    }

    private static void Restore(Session session)
    {
        CombatManager manager = CombatManager.Instance;
        Player owner = session.Owner;
        bool live = LiveCombat() && ReferenceEquals(owner.Creature.CombatState, session.Combat) && owner.Creature.IsAlive;
        ExceptionDispatchInfo failure = null;
        void Attempt(Action operation)
        {
            try { operation(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        Attempt(() => VritikaCalculationCombatHelper.OnTurnEnd(owner, session));
        Attempt(() =>
        {
            if (live) session.Hand?.TryRestore();
            else session.Hand?.Abandon();
        });
        // A card-pile observer may throw. Phase, input flags and the owner's
        // saved vote must still be attempted independently of that observer.
        Attempt(() => { if (owner.PlayerCombatState != null) owner.PlayerCombatState.Phase = session.SavedPhase; });
        Attempt(() => session.Queue?.Restore());
        try
        {
            _restoringReady = true;
            if (live)
            {
                Attempt(() =>
                {
                    if (session.Ready != null) session.Ready.UndoReady();
                    else manager.UndoReadyToEndTurnCompat(owner);
                });
                if (session.SavedReady) Attempt(() => manager.SetReadyToEndTurn(owner, canBackOut: session.Ready?.WasForced != true));
                if (LocalContext.IsMe(owner) && (session.SavedReady || session.SavedPhase != PlayerTurnPhase.Play))
                    Attempt(manager.OnEndedTurnLocally);
            }
        }
        finally
        {
            Attempt(() => session.Ready?.RestoreConstraint());
            _restoringReady = false;
            Schedule.Complete(session.Ticket.RequestId, session.Ticket.Ordinal);
            _current = null;
            _finishSent = null;
        }
        Attempt(TryRequestNextBegin);
        Attempt(ReleaseOrdinaryEnd);
        failure?.Throw();
    }

    internal static bool ShouldDeferOrdinaryEnd()
    {
        if (OverclockImmediateTurnCoordinator.HasSession || !Schedule.IsBusy || !LiveCombat()) return false;
        _ordinaryEndDeferred = true;
        return true;
    }

    private static void ReleaseOrdinaryEnd()
    {
        if (!_ordinaryEndDeferred || Schedule.IsBusy || OverclockImmediateTurnCoordinator.HasSession || !LiveCombat()) return;
        _ordinaryEndDeferred = false;
        CombatManager manager = CombatManager.Instance;
        if (!sessionPlayersReady(manager)) return;
        Player player = _combat.Players.FirstOrDefault(p => p.Creature.IsAlive);
        if (player == null) return;
        var ready = new PersonalTurnReadyGuard(manager, player);
        try
        {
            ready.UndoReady();
            PlayerCmd.EndTurn(player, canBackOut: false);
        }
        finally { ready.RestoreConstraint(); }
    }

    private static bool sessionPlayersReady(CombatManager manager) => _combat.Players.All(manager.IsPlayerReadyToEndTurn);
    private static bool LiveCombat() => _combat != null && CombatManager.Instance.IsInProgress &&
        !CombatManager.Instance.IsOverOrEnding && ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), _combat) &&
        _combat.CurrentSide == CombatSide.Player;
    private static bool IsCurrent(Session session) => ReferenceEquals(_current, session) && LiveCombat() &&
        ReferenceEquals(session.Owner.Creature.CombatState, session.Combat) && session.Owner.Creature.IsAlive;

    internal static void OnPlayerDeath(Player player)
    {
        if (_current is { } session && ReferenceEquals(player, session.Owner))
        {
            // The same guarded restore abandons a dead holder's detached hand
            // while still restoring phase/input and completing the ticket.
            Restore(session);
        }
        Schedule.Prune(p => p?.Creature.IsAlive == true);
        PrepareAdmission();
        TryRequestNextBegin();
        ReleaseOrdinaryEnd();
    }

    internal static void Clear()
    {
        _current?.Hand?.Abandon();
        _current?.Queue?.Restore();
        _current?.Ready?.RestoreConstraint();
        _admissionGuard?.Restore();
        _admissionGuard = null;
        _admissionOwner = null;
        _current = null;
        PersonalTurnReadyGuard.ClearVotes(_combat);
        _combat = null;
        Schedule.Clear();
        _beginSent = _finishSent = null;
        _ordinaryEndDeferred = _restoringReady = false;
    }
}
