using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Acts;
using HBRGOSakiMod.Core.Models.Encounters;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterNextAct))]
internal static class NararaShibaBossTransitionPatch
{
    private static readonly MethodInfo ClearScreensMethod =
        AccessTools.Method(typeof(RunManager), "ClearScreens");
    private static readonly MethodInfo ExitCurrentRoomsMethod =
        AccessTools.Method(typeof(RunManager), "ExitCurrentRooms");
    private static bool _isTransitioning;
    private static Task _transitionTask;

    private static bool Prefix(
        RunManager __instance,
        ref Task __result)
    {
        // A duplicate Continue must not fall through to vanilla EnterNextAct
        // while the checkpoint is being written and skip the fixed event.
        if (_isTransitioning)
        {
            __result = _transitionTask ?? Task.CompletedTask;
            return false;
        }

        RunState state = __instance.DebugOnlyGetState();
        if (!ShouldEnterFixedEvent(__instance, state))
        {
            return true;
        }

        EventModel nextEvent = ShouldEnterDoorAjarEvent(state)
            ? ModelDb.Event<Story_INSIDETHEDOOR_HBRGO_SAKI>()
            : ModelDb.Event<TheArchitect>();
        _isTransitioning = true;
        __result = _transitionTask = EnterFixedEvent(__instance, state, nextEvent);
        return false;
    }

    private static bool ShouldEnterFixedEvent(RunManager manager, RunState state)
    {
        if (state?.Act is not HbrgoActFour || state.CurrentRoomCount != 1)
            return false;

        return state.CurrentRoom is CombatRoom { IsPreFinished: true, RoomType: RoomType.Boss }
            || (state.CurrentRoom is EventRoom { CanonicalEvent: Story_INSIDETHEDOOR_HBRGO_SAKI }
                && manager.EventSynchronizer.Events.Count > 0
                && manager.EventSynchronizer.Events.All(story => story.IsFinished));
    }

    private static bool ShouldEnterDoorAjarEvent(RunState state)
    {
        if (state?.Act is not HbrgoActFour ||
            state.CurrentRoom is not CombatRoom
            {
                IsPreFinished: true,
                RoomType: RoomType.Boss
            } room ||
            room.Encounter is NararaShibaBoss ||
            room.Encounter.MonstersWithSlots.Any(monster =>
                monster.Item1 is FlatHand
                {
                    UnlockedNararaShibaThisRun: true
                }) ||
            !HbrgoSakiVariantPreference.IsNararaShibaUnlocked())
        {
            return false;
        }

        return true;
    }

    private static async Task EnterFixedEvent(
        RunManager runManager,
        RunState state,
        EventModel nextEvent)
    {
        try
        {
            await runManager.EventSynchronizer.AwaitPendingOptionTasks();

            var room = new EventRoom(nextEvent);

            if (TestMode.IsOff)
            {
                await NGame.Instance.Transition.RoomFadeOut();
            }

            ClearScreensMethod?.Invoke(runManager, null);
            await InvokeRunManagerTask(
                ExitCurrentRoomsMethod,
                runManager);

            RecordFixedEvent(state, room);
            // Ordinary events are not pre-finished. Passing the room explicitly
            // makes it the load override instead of rerolling the last boss node.
            // Save before enter hooks or choices grant anything: an in-event or
            // mid-Shiba reload must restore the whole pre-event snapshot.
            await SaveManager.Instance.SaveRun(room);
            if (runManager.CombatReplayWriter?.IsEnabled == true)
            {
                runManager.CombatReplayWriter.RecordInitialState(
                    runManager.ToSave(room));
            }

            await runManager.EnterRoom(room);

            if (TestMode.IsOff)
            {
                await NGame.Instance.Transition.RoomFadeIn();
            }
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private static void RecordFixedEvent(RunState state, EventRoom room)
    {
        // This is a continuation of the boss node, not another map floor.
        // Keep coordinate-based history and total-floor indices unchanged.
        MapPointHistoryEntry history = state.CurrentMapPointHistoryEntry;
        if (history == null || history.Rooms.Any(entry =>
                entry.RoomType == room.RoomType && entry.ModelId == room.ModelId))
            return;

        history.Rooms.Add(new MapPointRoomHistoryEntry
        {
            RoomType = room.RoomType,
            ModelId = room.ModelId
        });
    }

    private static Task InvokeRunManagerTask(
        MethodInfo method,
        RunManager runManager,
        params object[] arguments)
    {
        return method?.Invoke(runManager, arguments) as Task ??
            Task.FromException(new MissingMethodException(
                $"Unable to invoke RunManager.{method?.Name ?? "unknown"}."));
    }
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.SetEvent))]
internal static class DoorAjarEventPortraitPatch
{
    private static void Postfix(
        NEventLayout __instance,
        EventModel eventModel)
    {
        if (eventModel is not DoorAjarHbrgoSaki)
        {
            return;
        }

        TextureRect portrait =
            __instance.GetNodeOrNull<TextureRect>("%Portrait");
        if (portrait != null)
        {
            portrait.StretchMode =
                TextureRect.StretchModeEnum.KeepAspectCovered;
        }
    }
}

[HarmonyPatch(
    typeof(ActChangeSynchronizer),
    nameof(ActChangeSynchronizer.OnPlayerReady))]
internal static class NararaShibaRepeatedActTransitionPatch
{
    private sealed class DoorVoteStage
    {
        internal EventRoom Room;
    }

    private static readonly ConditionalWeakTable<ActChangeSynchronizer, DoorVoteStage> DoorVoteStages = new();

    private static readonly FieldInfo LastTransitioningActIndexField =
        AccessTools.Field(
            typeof(ActChangeSynchronizer),
            "_lastTransitioningActIndex");

    private static void Prefix(ActChangeSynchronizer __instance, Player __0)
    {
        if (LastTransitioningActIndexField == null) return;
        RunManager manager = RunManager.Instance;
        RunState state = manager?.DebugOnlyGetState();
        if (state?.Act is HbrgoActFour
            && state.CurrentRoom is EventRoom { CanonicalEvent: Story_INSIDETHEDOOR_HBRGO_SAKI } eventRoom
            && manager.EventSynchronizer.GetEventForPlayer(__0) is HbrStoryEvent { IsFinished: true })
        {
            DoorVoteStage stage = DoorVoteStages.GetValue(__instance, static _ => new DoorVoteStage());
            // Unlock this event's vote once. Repeated messages must retain vanilla's
            // duplicate-transition guard while the asynchronous room change runs.
            if (ReferenceEquals(stage.Room, eventRoom)) return;
            stage.Room = eventRoom;
            LastTransitioningActIndexField.SetValue(__instance, state.CurrentActIndex - 1);
            return;
        }

        if (state?.CurrentRoom is CombatRoom { Encounter: NararaShibaBoss })
            LastTransitioningActIndexField.SetValue(__instance, -1);
    }
}

[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
internal static class DoorStoryCombatRewardsProceedPatch
{
    // Let the six-sense allocation screen finish before returning to the story.
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(bool ____isTerminal, IRunState ____runState, NProceedButton ____proceedButton)
    {
        if (!____isTerminal || ____runState?.CurrentRoomCount <= 1
            || ____runState?.CurrentRoom is not CombatRoom
            {
                IsPreFinished: true,
                ShouldResumeParentEventAfterCombat: true,
                Encounter: NararaShibaBoss encounter
            }
            || encounter.StoryState?.EventId != HbrStoryEvent.ActEndingDoorEventId)
            return true;

        ____proceedButton.Disable();
        // This resumes the parent event without an act vote or another ActFloor increment.
        TaskHelper.RunSafely(RunManager.Instance.ProceedFromTerminalRewardsScreenCompat());
        return false;
    }
}
