using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NararaShibaEventPreparation
{
    private sealed class Preparation
    {
        internal readonly HashSet<ulong> ReadyOwners = new();
        internal readonly Dictionary<ulong, Lazy<Task>> CopyRewards = new();
        internal readonly TaskCompletionSource<int> Floor = new();
        internal bool Started;
    }

    private static readonly ConditionalWeakTable<EventRoom, Dictionary<string, Preparation>> Preparations = new();

    private static Preparation GetPreparation(EventRoom room, string preparationId)
    {
        Dictionary<string, Preparation> phases = Preparations.GetValue(
            room, _ => new Dictionary<string, Preparation>(StringComparer.Ordinal));
        if (!phases.TryGetValue(preparationId, out Preparation preparation))
            phases[preparationId] = preparation = new Preparation();
        return preparation;
    }

    internal static async Task<int> Ready(Player owner, string preparationId)
    {
        RunManager manager = RunManager.Instance;
        if (owner.RunState.CurrentRoom is not EventRoom room)
            throw new InvalidOperationException("Narara preparation requires the current shared event room.");
        RequireCurrentEvent(manager, owner.RunState, room);
        var events = manager.EventSynchronizer.Events;
        if (!events.Any(model => model.Owner.NetId == owner.NetId))
            throw new InvalidOperationException("Narara preparation owner is not part of the current event.");

        // Shared event choices run once for every player's mutable event on each
        // peer. Do not enter (and pause the action queue) while another owner's
        // rewards, removal or upgrades are still being applied on this peer.
        Preparation preparation = GetPreparation(room, preparationId);
        if (!preparation.CopyRewards.TryGetValue(owner.NetId, out Lazy<Task> copyReward))
        {
            // Store before starting the task, including synchronous/recursive
            // callbacks. Repeated readiness reports must never duplicate cards.
            copyReward = new Lazy<Task>(() => OfferCardCopy(owner, manager, owner.RunState, room));
            preparation.CopyRewards.Add(owner.NetId, copyReward);
        }
        await copyReward.Value;
        RequireCurrentEvent(manager, owner.RunState, room);
        preparation.ReadyOwners.Add(owner.NetId);
        if (!preparation.Started && events.All(
                model => preparation.ReadyOwners.Contains(model.Owner.NetId)))
        {
            preparation.Started = true;
            _ = CompletePreparation(preparation, manager, owner.RunState, room);
        }

        return await preparation.Floor.Task;
    }

    private static async Task OfferCardCopy(Player owner, RunManager manager,
        IRunState run, EventRoom room)
    {
        RequireCurrentEvent(manager, run, room);
        // Combat replay starts with the already prepared deck; the preceding
        // event's selection messages are not in the combat replay recording.
        if (manager.NetService.Type == NetGameType.Replay) return;

        var prefs = new CardSelectorPrefs(
            new LocString("events", "HBRGO_NARARA_PREPARATION.copySelectionPrompt"), 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        var selected = (await CardSelectCmd.FromDeckGeneric(owner, prefs)).FirstOrDefault();
        RequireCurrentEvent(manager, run, room);
        if (selected == null) return;
        if (selected.Owner != owner || !owner.Deck.Cards.Contains(selected))
            throw new InvalidOperationException("Narara card copy requires a card in the selecting player's deck.");

        var copy = run.CloneCard(selected);
        var result = await CardPileCmd.Add(copy, PileType.Deck);
        RequireCurrentEvent(manager, run, room);
        CardCmd.PreviewCardPileAdd(result);
    }

    internal static void Fail(Player owner, string preparationId, Exception exception)
    {
        if (owner.RunState.CurrentRoom is not EventRoom room) return;
        Preparation preparation = GetPreparation(room, preparationId);
        preparation.Started = true;
        preparation.Floor.TrySetException(exception);
    }

    private static void RequireCurrentEvent(RunManager manager, IRunState run, EventRoom room)
    {
        if (!ReferenceEquals(RunManager.Instance, manager) || manager.IsCleaningUp ||
            !ReferenceEquals(manager.DebugOnlyGetState(), run) || !ReferenceEquals(run.CurrentRoom, room))
            throw new OperationCanceledException("Narara event preparation no longer belongs to the current run and room.");
    }

    private static async Task CompletePreparation(
        Preparation preparation, RunManager manager, IRunState run, EventRoom room)
    {
        try
        {
            // Reserve only after all owners' reward choices have finished, and
            // only once per choice phase. Each peer consumes the same first player's
            // next choice ID; every event clone shares this single result.
            RequireCurrentEvent(manager, run, room);
            Player leader = run.Players[0];
            int floor;
            if (manager.NetService.Type == NetGameType.Replay)
            {
                // Pre-combat event choices are not recorded by the native
                // combat replay writer; never wait for a nonexistent message.
                floor = NararaShibaFloorSync.CurrentFloor;
            }
            else if (manager.NetService.Type == NetGameType.Singleplayer)
            {
                floor = await NararaShibaFloorSync.RefreshAsync();
            }
            else
            {
                uint choiceId = manager.PlayerChoiceSynchronizer.ReserveChoiceId(leader);
                if (LocalContext.IsMe(leader))
                {
                    floor = await NararaShibaFloorSync.RefreshAsync();
                    RequireCurrentEvent(manager, run, room);
                    manager.PlayerChoiceSynchronizer.SyncLocalChoice(
                        leader, choiceId, PlayerChoiceResult.FromIndex(floor));
                }
                else
                {
                    floor = (await manager.PlayerChoiceSynchronizer.WaitForRemoteChoice(leader, choiceId)).AsIndex();
                }
            }

            if (floor <= 0)
                throw new InvalidOperationException($"Invalid shared Narara starting floor: {floor}.");
            // Async HTTP/remote waits can outlive leaving or loading a run.
            // Never release an old event's callbacks into the new run manager.
            RequireCurrentEvent(manager, run, room);
            Log.Info($"Narara shared event preparation finished; starting floor {floor}.");
            preparation.Floor.TrySetResult(floor);
        }
        catch (Exception exception)
        {
            preparation.Floor.TrySetException(exception);
        }
    }
}
