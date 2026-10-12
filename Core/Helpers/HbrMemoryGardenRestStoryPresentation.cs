using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Events;
using HBRGOSakiMod.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Presents the night story inside the existing rest site. The logical room and its
/// synchronized options stay intact; a RestSiteRoom cannot be reconstructed as a
/// saved parent room, so this deliberately does not push a nested EventRoom.
/// </summary>
internal static class HbrMemoryGardenRestStoryPresentation
{
    internal enum Phase { Inactive, Preparing, Reading, Returning, Closed }

    internal sealed class Entry
    {
        internal readonly IRunState RunState;
        internal readonly RestSiteRoom Room;
        internal readonly bool Claimed;
        internal Phase _phase;
        internal EventModel _localEvent;
        internal EventRoom _displayRoom;
        internal Task _returnTask;

        internal Entry(IRunState runState, RestSiteRoom room, bool claimed)
        {
            RunState = runState;
            Room = room;
            Claimed = claimed;
            _phase = claimed ? Phase.Preparing : Phase.Inactive;
        }
    }

    private static readonly ConditionalWeakTable<RestSiteRoom, Entry> Entries = new();

    // Called before the original asynchronous EnterInternal starts. Re-evaluating
    // this after a rest-site choice would incorrectly trigger on the fourth card's fire.
    internal static Entry CaptureEntry(IRunState runState, RestSiteRoom room, bool isRestoring)
    {
        if (isRestoring || runState == null || !ReferenceEquals(runState.CurrentRoom, room)) return null;
        return Entries.GetValue(room, key => new Entry(runState, key,
            HbrMemoryGardenRestStory.TryBeginAtRestSite(runState, key)));
    }

    internal static bool IsPreparing(RestSiteRoom room) => room != null
        && Entries.TryGetValue(room, out Entry entry) && entry._phase == Phase.Preparing;

    internal static async Task PresentAfterEntry(Task originalEntry, Entry entry)
    {
        try { await originalEntry; }
        catch
        {
            if (entry?.Claimed == true)
            {
                entry._phase = Phase.Closed;
                HbrMemoryGardenRestStory.CancelBeginAtRestSite(entry.RunState, entry.Room);
            }
            throw;
        }
        if (entry?.Claimed != true || entry._phase != Phase.Preparing) return;

        try
        {
            if (!IsCurrent(entry))
            {
                entry._phase = Phase.Closed;
                HbrMemoryGardenRestStory.CancelBeginAtRestSite(entry.RunState, entry.Room);
                return;
            }
            var canonical = ModelDb.Event<Story_THEBEGINNING_HBRGO_SAKI>();
            if (canonical.Definition.Pages.Any(page => page.Choices.Any(choice => choice.Combat.Length > 0)))
                throw new InvalidOperationException("黑刀之夜的火堆剧情不能包含嵌套战斗。");

            RunManager manager = RunManager.Instance;
            await manager.EventSynchronizer.AwaitPendingOptionTasks();
            await PreloadManager.LoadRoomEventAssets(canonical, entry.RunState);
            if (!IsCurrent(entry))
            {
                entry._phase = Phase.Closed;
                HbrMemoryGardenRestStory.CancelBeginAtRestSite(entry.RunState, entry.Room);
                return;
            }
            manager.EventSynchronizer.BeginEvent(canonical);
            entry._displayRoom = new EventRoom(canonical);
            entry._localEvent = manager.EventSynchronizer.GetLocalEvent();
            entry._phase = Phase.Reading;
            NRun.Instance?.SetCurrentRoom(NEventRoom.Create(entry._localEvent, entry.RunState, false));
            if (TestMode.IsOff && entry._localEvent.Node is not NLibraryEvent { IsInitializedForRestStory: true })
                throw new InvalidOperationException("黑刀之夜剧情界面未能完成初始化。");
            await entry._localEvent.AfterEventStarted();
            HbrMemoryGardenRestStory.ConfirmBeginAtRestSite(entry.RunState, entry.Room);
            if (entry.RunState is RunState state) state.AddVisitedEvent(canonical);
        }
        catch (Exception exception)
        {
            entry._phase = Phase.Closed;
            HbrMemoryGardenRestStory.CancelBeginAtRestSite(entry.RunState, entry.Room);
            entry._localEvent?.EnsureCleanup();
            Log.Error($"黑刀之夜剧情初始化失败，返回原火堆：{exception}");
            if (IsCurrent(entry)) await RestoreRestSite(entry);
        }
    }

    internal static bool TryGetReturnTask(EventModel eventModel, out Task returnTask)
    {
        returnTask = null;
        if (eventModel?.IsFinished != true
            || RunManager.Instance?.DebugOnlyGetState()?.CurrentRoom is not RestSiteRoom room
            || !Entries.TryGetValue(room, out Entry entry)
            || !ReferenceEquals(entry._localEvent, eventModel)
            || entry._phase is not (Phase.Reading or Phase.Returning)) return false;

        if (entry._phase == Phase.Reading)
        {
            entry._phase = Phase.Returning;
            entry._returnTask = ReturnToRestSite(entry);
        }
        returnTask = entry._returnTask;
        return true;
    }

    private static async Task ReturnToRestSite(Entry entry)
    {
        try
        {
            await RunManager.Instance.EventSynchronizer.AwaitPendingOptionTasks();
            if (!IsCurrent(entry)) return;
            foreach (EventModel story in RunManager.Instance.EventSynchronizer.Events) story.EnsureCleanup();
            // LoadRoomEventAssets may have evicted the fire's specific assets.
            // Resume rebuilds its UI, without BeginRestSite or another room-enter hook.
            await RestoreRestSite(entry);
            entry._phase = Phase.Closed;
        }
        catch
        {
            // Keep the completed event available for another return attempt. Never
            // cancel its consumed once flag or replay already granted rewards here.
            entry._phase = Phase.Reading;
            throw;
        }
    }

    private static async Task RestoreRestSite(Entry entry)
    {
        await PreloadManager.LoadRoomRestSite(entry.RunState.Act, entry.Room.Options);
        if (IsCurrent(entry)) await entry.Room.Resume((AbstractRoom)entry._displayRoom ?? entry.Room, entry.RunState);
    }

    private static bool IsCurrent(Entry entry) =>
        ReferenceEquals(RunManager.Instance?.DebugOnlyGetState(), entry.RunState)
        && ReferenceEquals(entry.RunState.CurrentRoom, entry.Room);
}
