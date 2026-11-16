using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Nodes.Events;
using HBRGOSakiMod.Core.Nodes.Events.Story;
using HBRGOSakiMod.Core.Nodes.Screens;
using HBRGOSakiMod.Core.Story;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrMemoryUpgradeStoryPlaybackResult { Completed, Skipped, Abandoned }

// A local visual session only. No event, room, overlay-stack, game action,
// choice synchronizer, RNG or viewed-story persistence is changed here.
internal static class HbrMemoryUpgradeStoryPresentation
{
    private sealed class Anchor
    {
        internal NRun RunNode;
        internal Control Host;
        internal IScreenContext Underlay;
        internal WeakReference<Control> Focus;
    }
    private sealed class Session
    {
        internal Player Owner;
        internal IRunState Run;
        internal AbstractRoom Room;
        internal Anchor Anchor;
        internal HbrMemoryUpgradeStorySegment Segment;
        internal CanvasLayer Layer;
        internal NLibraryEvent Reader;
        internal bool Attached;
        internal bool Closing;
        internal readonly CancellationTokenSource Cancellation = new();
        internal readonly TaskCompletionSource<HbrMemoryUpgradeStoryPlaybackResult> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private static Session _active;
    private static NRun _exitingRun;
    private static WeakReference<NRun> _exitWatched;
    private static bool _listening;
    private static bool _observing;
    [ThreadStatic] private static bool _nativeContext;
    internal static event Action AvailabilityChanged;

    internal static bool CanPlayLocal(Player owner)
    {
        EnsureAvailability();
        return _active == null && TryCaptureUi(owner, out _);
    }
    private static bool TryCaptureUi(Player owner, out Anchor anchor)
    {
        anchor = null;
        if (owner == null || !LocalContext.IsMe(owner) || !MemoryGardenSelectionScope.IsCurrentRun(owner) ||
            CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding) return false;
        NRun run = NRun.Instance;
        if (!Usable(run) || ReferenceEquals(run, _exitingRun) || !Usable(run.GlobalUi) ||
            NDevConsole.Instance?.Visible == true) return false;
        IScreenContext current = NativeContext();
        // These are stable, completed upgrade underlays. In particular do not
        // interrupt a selection, inspect, pause, combat or capstone transition.
        if (current is not (NMemoryGardenScreen or NRewardsScreen or NLibraryEvent or NRestSiteRoom or NEventRoom or NMapScreen)) return false;
        if (current is not Control control || !Usable(control) || !control.IsVisibleInTree()) return false;
        WatchRunExit(run);
        Control focus = control.GetViewport()?.GuiGetFocusOwner();
        anchor = new Anchor { RunNode = run, Host = run.GlobalUi, Underlay = current,
            Focus = focus == null ? null : new WeakReference<Control>(focus) };
        return true;
    }
    private static void WatchRunExit(NRun run)
    {
        if (_exitWatched?.TryGetTarget(out NRun watched) == true && ReferenceEquals(watched, run)) return;
        _exitWatched = new WeakReference<NRun>(run);
        run.TreeExiting += () => OnRunExiting(run);
    }
    private static void EnsureAvailability()
    {
        if (_listening) return;
        _listening = true;
        ActiveScreenContext.Instance.Updated += OnScreenChanged;
    }
    private static void OnScreenChanged()
    {
        if (_observing) return;
        _observing = true;
        try
        {
            if (_active is { } session) ObserveSession(session);
            NotifyAvailability();
        }
        finally { _observing = false; }
    }
    private static void NotifyAvailability()
    {
        if (AvailabilityChanged == null) return;
        foreach (Action listener in AvailabilityChanged.GetInvocationList())
            try { listener(); }
            catch (Exception error) { Log.Warn("[HBRGOSakiMod] Upgrade memory availability observer failed: " + error.Message); }
    }
    private static IScreenContext NativeContext()
    {
        bool previous = _nativeContext;
        _nativeContext = true;
        try { return ActiveScreenContext.Instance.GetCurrentScreen(); }
        finally { _nativeContext = previous; }
    }
    internal static IScreenContext ResolveScreenContext(IScreenContext native)
    {
        Session session = _active;
        return !_nativeContext && session is { Attached: true, Closing: false } &&
            ReferenceEquals(native, session.Anchor.Underlay) && IsCurrent(session) &&
            ReaderIsVisible(session) ? session.Reader : native;
    }
    private static bool IsCurrent(Session session) => !session.Closing && MemoryGardenSelectionScope.IsCurrentRun(session.Owner) &&
        ReferenceEquals(session.Owner.RunState, session.Run) && ReferenceEquals(session.Run.CurrentRoom, session.Room) &&
        ReferenceEquals(NRun.Instance, session.Anchor.RunNode) && !ReferenceEquals(NRun.Instance, _exitingRun) && AnchorIsUsable(session);
    private static bool AnchorIsUsable(Session session) => session.Anchor.Underlay is Control control &&
        Usable(control) && control.IsInsideTree();

    internal static async Task<HbrMemoryUpgradeStoryPlaybackResult> PlayLocalAsync(Player owner,
        HbrMemoryUpgradeStoryPerspective perspective, string actorId, int level)
    {
        EnsureAvailability();
        if (_active != null || !HbrMemoryUpgradeStoryCatalog.TryFind(actorId, perspective, level, out var segment) ||
            !TryCaptureUi(owner, out Anchor anchor)) return HbrMemoryUpgradeStoryPlaybackResult.Abandoned;
        Session session = new() { Owner = owner, Run = owner.RunState, Room = owner.RunState.CurrentRoom, Anchor = anchor, Segment = segment };
        // Claim this local screen before the first await; previews and another
        // queued upgrade cannot both claim the same UI-ready instant.
        _active = session;
        try
        {
            if (!await LoadAssets(segment).WaitAsync(session.Cancellation.Token) || !IsCurrent(session))
            { Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned); return await session.Completion.Task; }
            AttachReader(session);
            return await session.Completion.Task;
        }
        catch (OperationCanceledException)
        {
            Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
            return await session.Completion.Task;
        }
        catch (Exception error)
        {
            Log.Warn("[HBRGOSakiMod] Upgrade memory reader closed safely: " + error);
            Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
            return await session.Completion.Task;
        }
        finally
        {
            if (ReferenceEquals(_active, session)) Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
            session.Cancellation.Dispose();
        }
    }
    private static Task<bool> LoadAssets(HbrMemoryUpgradeStorySegment segment)
    {
        var paths = segment.AssetPaths.Concat(NLibraryStoryRuntimeResources.Collect(segment.EffectivePresentation,
            segment.Lines, HbrStoryAudioSession.ResolveCueResource)).Concat(new[] { NLibraryEvent.ScenePath,
            ImageHelper.GetImagePath("ui/library/hbr_original_ui_atlas.png") }).Distinct(StringComparer.Ordinal);
        AssetLoadingSession loading = PreloadManager.Cache.CreateSession("memory-upgrade", paths);
        return NAssetLoader.Instance.LoadInTheBackground(loading);
    }
    private static void AttachReader(Session session)
    {
        NLibraryEvent reader = new() { Name = "LocalUpgradeMemoryReader" };
        session.Reader = reader;
        reader.InitializeUpgradeMemory(session.Owner, session.Segment, result => Finish(session, result));
        CanvasLayer layer = new() { Name = "LocalUpgradeMemoryLayer", Layer = 100 };
        session.Layer = layer;
        session.Anchor.Host.AddChild(layer);
        session.Attached = true;
        layer.AddChild(reader);
        ActiveScreenContext.Instance.Update();
    }
    internal static bool ObserveReader(NLibraryEvent reader)
    {
        Session session = _active;
        if (session == null || !ReferenceEquals(session.Reader, reader)) return false;
        return ObserveSession(session);
    }
    private static bool ObserveSession(Session session)
    {
        if (!IsCurrent(session)) { Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned); return false; }
        if (session.Attached) UpdateReaderCoverage(session, ReferenceEquals(NativeContext(), session.Anchor.Underlay));
        return !session.Closing;
    }
    private static bool ReaderIsVisible(Session session) => Usable(session.Reader) && session.Reader.Visible;
    private static void UpdateReaderCoverage(Session session, bool visible)
    {
        if (Usable(session.Reader) && session.Reader.Visible != visible) session.Reader.Visible = visible;
    }
    private static void Finish(Session session, HbrMemoryUpgradeStoryPlaybackResult result)
    {
        if (session.Closing) return;
        session.Closing = true;
        session.Cancellation.Cancel();
        try { DetachReader(session); }
        catch (Exception error) { Log.Warn("[HBRGOSakiMod] Upgrade memory visual cleanup failed: " + error.Message); }
        finally
        {
            if (ReferenceEquals(_active, session)) _active = null;
            session.Completion.TrySetResult(result);
        }
        try { ActiveScreenContext.Instance.Update(); if (_active == null) RestoreFocus(session); }
        catch (Exception error) { Log.Warn("[HBRGOSakiMod] Upgrade memory focus restore failed: " + error.Message); }
        NotifyAvailability();
    }
    private static void DetachReader(Session session)
    {
        if (Usable(session.Reader)) { session.Reader.Visible = false; session.Reader.QueueFree(); }
        if (Usable(session.Layer)) session.Layer.QueueFree();
    }
    private static void RestoreFocus(Session session)
    {
        // If another player moved the room or the user opened another screen,
        // abandon our visual only; never resurrect or focus that stale UI.
        if (!AnchorIsUsable(session) || !ReferenceEquals(NRun.Instance, session.Anchor.RunNode) ||
            !ReferenceEquals(session.Run.CurrentRoom, session.Room) || !ReferenceEquals(NativeContext(), session.Anchor.Underlay)) return;
        if (session.Anchor.Focus?.TryGetTarget(out Control focus) == true && Usable(focus) && focus.IsVisibleInTree()) focus.GrabFocus();
        else ActiveScreenContext.Instance.FocusOnDefaultControl();
    }
    internal static void CancelAll()
    {
        if (_active is { } session) Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
    }
    internal static void OnRunExiting(NRun run)
    {
        _exitingRun = run;
        if (_active is { } session && ReferenceEquals(session.Anchor.RunNode, run))
            Finish(session, HbrMemoryUpgradeStoryPlaybackResult.Abandoned);
    }
    private static bool Usable(Node node) => node != null && GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion();
}
