using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;

namespace HBRGOSakiMod.Core.Helpers;

// Local presentation only. The common upgrade transaction never awaits this
// queue and no claim is written to a model, synchronized action or run save.
internal static class HbrMemoryGardenUpgradeStories
{
    private sealed class Pending
    {
        internal string Actor, Key, RunKey;
        internal int Level;
        internal HbrMemoryUpgradeStoryPerspective Perspective;
        internal AbstractRoom OriginRoom;
        internal CombatRoom WaitingForCombat;
    }
    private sealed class State
    {
        internal IRunState Run;
        internal readonly Queue<Pending> Queue = new();
        internal readonly HashSet<string> QueuedKeys = new(StringComparer.Ordinal);
        internal bool Running;
    }
    private static ConditionalWeakTable<Player, State> States = new();
    private static WeakReference<Player> LocalOwner;
    private static IDisposable StartedSubscription, LoadedSubscription, EndedSubscription;
    private static bool Initialized;

    internal static void Initialize()
    {
        if (Initialized) return;
        Initialized = true;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        HbrMemoryUpgradeStoryPresentation.AvailabilityChanged += OnAvailabilityChanged;
        StartedSubscription = RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(_ => ClearPending(), replayCurrentState: false);
        LoadedSubscription = RitsuLibFramework.SubscribeLifecycle<RunLoadedEvent>(_ => ClearPending(), replayCurrentState: false);
        EndedSubscription = RitsuLibFramework.SubscribeLifecycle<RunEndedEvent>(_ => ClearPending(), replayCurrentState: false);
    }

    // Root calls this once after the real choice commit. In particular it is
    // not called for acquisition, preview rebuilding or shared-copy refresh.
    internal static void OnUpgradeCommitted(CardModel card, int previousLevel)
    {
        try
        {
            if (card?.IsMutable != true || card.Owner is not { } owner || card.HasBeenRemovedFromState ||
                card.CurrentUpgradeLevel <= previousLevel || card.CurrentUpgradeLevel is < 1 or > 7 ||
                card.Pile?.Type != PileType.Deck || !owner.Deck.Cards.Any(owned => ReferenceEquals(owned, card)) ||
                !IsCurrentLocalOwner(owner) || !HbrMemoryGarden.TryGetUpgradeStoryActor(card, out string actor) ||
                !HbrMemoryGarden.TryGetUpgradeStoryPerspective(owner, out var perspective) ||
                !ShouldOffer(perspective, actor, card.CurrentUpgradeLevel) ||
                !HbrMemoryGardenUpgradeStoryLocalStore.TryGetRunKey(owner, out string runKey) ||
                HbrMemoryGardenUpgradeStoryLocalStore.HasStarted(runKey, PerspectiveKey(perspective), actor, card.CurrentUpgradeLevel)) return;

            Initialize();
            if (States.TryGetValue(owner, out State prior) && !ReferenceEquals(prior.Run, owner.RunState)) States.Remove(owner);
            State state = States.GetValue(owner, player => new State { Run = player.RunState });
            string key = PerspectiveKey(perspective) + "/" + actor + "/" + card.CurrentUpgradeLevel;
            if (!state.QueuedKeys.Add(key)) return;
            state.Queue.Enqueue(new Pending
            {
                Actor = actor, Key = key, RunKey = runKey, Level = card.CurrentUpgradeLevel, Perspective = perspective,
                OriginRoom = owner.RunState.CurrentRoom,
                WaitingForCombat = CombatManager.Instance.IsInProgress ? owner.RunState.CurrentRoom as CombatRoom : null
            });
            LocalOwner = new WeakReference<Player>(owner);
            TryPump(owner, state);
        }
        catch (Exception error) { Warn("queue", error); }
    }

    private static string PerspectiveKey(HbrMemoryUpgradeStoryPerspective perspective) => HbrMemoryUpgradeStoryPerspectives.Key(perspective);

    private static bool ShouldOffer(HbrMemoryUpgradeStoryPerspective perspective, string actor, int level)
    {
        HbrMemoryGardenStoryPlayback mode = HBRGOSakiConfig.MemoryGardenUpgradeStoryPlayback;
        return mode != HbrMemoryGardenStoryPlayback.Off &&
            HbrMemoryUpgradeStoryCatalog.TryFind(actor, perspective, level, out _) &&
            (mode != HbrMemoryGardenStoryPlayback.UnseenOnly ||
                !HbrMemoryGardenUpgradeStoryLocalStore.HasSeen(PerspectiveKey(perspective), actor, level));
    }

    private static bool IsCurrentLocalOwner(Player owner) => owner != null && LocalContext.IsMe(owner) &&
        owner.Creature?.IsAlive == true && MemoryGardenSelectionScope.IsCurrentRun(owner) &&
        RunManager.Instance.NetService?.Type != NetGameType.Replay;

    private static bool IsCurrent(Player owner, State state) =>
        States.TryGetValue(owner, out State current) && ReferenceEquals(current, state) &&
        ReferenceEquals(owner.RunState, state.Run) && IsCurrentLocalOwner(owner);

    private static void OnCombatEnded(CombatRoom room)
    {
        try
        {
            foreach (Player owner in room.CombatState.Players)
            {
                if (!States.TryGetValue(owner, out State state)) continue;
                if (!IsCurrent(owner, state)) { States.Remove(owner); continue; }
                foreach (Pending pending in state.Queue)
                    if (ReferenceEquals(pending.WaitingForCombat, room)) pending.WaitingForCombat = null;
                TryPump(owner, state);
            }
        }
        catch (Exception error) { Warn("combat end", error); }
    }

    private static void OnAvailabilityChanged()
    {
        try
        {
            if (LocalOwner?.TryGetTarget(out Player owner) == true && States.TryGetValue(owner, out State state)) TryPump(owner, state);
        }
        catch (Exception error) { Warn("availability", error); }
    }

    private static void TryPump(Player owner, State state)
    {
        if (state.Running || state.Queue.Count == 0 || !IsCurrent(owner, state)) return;
        state.Running = true;
        _ = Pump(owner, state);
    }

    private static async Task Pump(Player owner, State state)
    {
        try
        {
            // Leave the committing callback first, without queuing a gameplay
            // action or waiting on this task from the synchronized transaction.
            await Task.Yield();
            while (IsCurrent(owner, state) && state.Queue.TryPeek(out Pending pending))
            {
                if (!ReferenceEquals(owner.RunState.CurrentRoom, pending.OriginRoom))
                { RemoveHead(state); continue; }
                if (pending.WaitingForCombat != null)
                {
                    return;
                }
                if (!ShouldOffer(pending.Perspective, pending.Actor, pending.Level) ||
                    !HbrMemoryGardenUpgradeStoryLocalStore.TryGetRunKey(owner, out string runKey) || runKey != pending.RunKey ||
                    HbrMemoryGardenUpgradeStoryLocalStore.HasStarted(runKey, PerspectiveKey(pending.Perspective), pending.Actor, pending.Level))
                { RemoveHead(state); continue; }
                if (!HbrMemoryUpgradeStoryPresentation.CanPlayLocal(owner)) return;
                RemoveHead(state);
                // No await between the atomic persistent claim and entering
                // the presenter, whose own synchronous gate reserves its UI.
                if (!HbrMemoryGardenUpgradeStoryLocalStore.MarkStarted(runKey, PerspectiveKey(pending.Perspective), pending.Actor, pending.Level)) continue;
                HbrMemoryUpgradeStoryPlaybackResult result = await HbrMemoryUpgradeStoryPresentation.PlayLocalAsync(owner,
                    pending.Perspective, pending.Actor, pending.Level);
                if (IsCurrent(owner, state) && result is HbrMemoryUpgradeStoryPlaybackResult.Completed or HbrMemoryUpgradeStoryPlaybackResult.Skipped)
                    HbrMemoryGardenUpgradeStoryLocalStore.MarkSeen(PerspectiveKey(pending.Perspective), pending.Actor, pending.Level);
            }
        }
        catch (Exception error) { Warn("playback", error); }
        finally { state.Running = false; }
    }

    private static void RemoveHead(State state)
    {
        Pending pending = state.Queue.Dequeue();
        state.QueuedKeys.Remove(pending.Key);
    }

    private static void ClearPending()
    {
        States = new();
        LocalOwner = null;
    }

    private static void Warn(string phase, Exception error) =>
        Log.Warn("[HBRGOSakiMod][MemoryUpgradeStory] " + phase + ": " + error.Message);
}
