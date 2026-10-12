using System;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

// A selection can outlive its room while SL tears down the run. Never resume
// its synchronization, rewards or rest-site completion against a new run.
internal sealed class MemoryGardenSelectionScope
{
    private readonly Player _player;
    private readonly RunManager _manager;
    private readonly IRunState _run;
    private readonly AbstractRoom _room;

    internal PlayerChoiceSynchronizer Synchronizer { get; }

    private MemoryGardenSelectionScope(Player player)
    {
        _player = player;
        _manager = RunManager.Instance;
        _run = player.RunState;
        _room = _run.CurrentRoom;
        Synchronizer = _manager.PlayerChoiceSynchronizer;
    }

    internal static bool IsCurrentRun(Player player) => player != null &&
        RunManager.Instance is { IsCleaningUp: false } manager &&
        ReferenceEquals(manager.DebugOnlyGetState(), player.RunState) &&
        player.RunState.CurrentRoom != null;

    internal static MemoryGardenSelectionScope Capture(Player player)
    {
        if (!IsCurrentRun(player)) throw new OperationCanceledException("The memory garden run has closed.");
        MemoryGardenSelectionScope scope = new(player);
        scope.ThrowIfInvalid();
        return scope;
    }

    internal void ThrowIfInvalid()
    {
        if (!IsCurrentRun(_player) || !ReferenceEquals(RunManager.Instance, _manager) ||
            !ReferenceEquals(_player.RunState, _run) || !ReferenceEquals(_run.CurrentRoom, _room) ||
            Synchronizer == null || !ReferenceEquals(_manager.PlayerChoiceSynchronizer, Synchronizer))
            throw new OperationCanceledException("The memory garden selection belongs to a closed run or room.");
    }
}
