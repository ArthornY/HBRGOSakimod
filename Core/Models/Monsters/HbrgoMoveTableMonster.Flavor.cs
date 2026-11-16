using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Monsters;

// Additional authored moves overlay the existing archived table, rather than
// editing its rows/weights. Paused turns retain the one already rolled option.
public abstract partial class HbrgoMoveTableMonster
{
    private sealed record Cursor(int Table, int Step, int DpOnce, int HpOnce);
    private sealed record CursorPair(Cursor Before, Cursor After);
    private Dictionary<string, MoveState> _flavorMoveBodies;
    private string _cachedFlavorCursor;
    private CursorPair _flavorCursor;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string FlavorQueuedTableMoveId { get; protected set; } = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string FlavorQueueCursorData { get; protected set; } = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FlavorTablePaused { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FlavorActionFinished { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FlavorQueueWasOverridden { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FlavorPhaseReselectPending { get; protected set; }

    internal virtual bool HasFlavorMechanics => false;
    internal virtual bool FlavorReselectOnShellBreak => false;
    internal bool FlavorEnabled => HasFlavorMechanics && EnemyStatScalingHelper.IsEnemy(Creature) &&
        HbrBattleStatsHelper.IsEnabled(Creature);
    private protected virtual IEnumerable<MoveState> CreateFlavorMoveStates() => Array.Empty<MoveState>();
    private protected virtual HbrEnemyFlavorChoice ChooseFlavorMove(string tableMoveId) => null;

    private IEnumerable<MoveState> BuildFlavorAwareMoveStates()
    {
        if (!HasFlavorMechanics) return CreateTableMoveStates();
        MoveState[] raw = CreateTableMoveStates().Concat(CreateFlavorMoveStates()).ToArray();
        _flavorMoveBodies = raw.ToDictionary(move => move.Id, StringComparer.Ordinal);
        return raw.Select(move => new MoveState(move.Id,
            targets => ExecuteFlavorMove(move.Id, targets), move.Intents.ToArray())
        { MustPerformOnceBeforeTransitioning = move.MustPerformOnceBeforeTransitioning }).ToArray();
    }
    private Cursor CaptureFlavorCursor() => new(MoveTableIndex, MoveTableStep, DpOnceUsedMask, HpOnceUsedMask);
    private CursorPair ReadFlavorCursor()
    {
        if (_cachedFlavorCursor == FlavorQueueCursorData) return _flavorCursor;
        _cachedFlavorCursor = FlavorQueueCursorData;
        return _flavorCursor = string.IsNullOrEmpty(FlavorQueueCursorData) ? null : JsonSerializer.Deserialize<CursorPair>(FlavorQueueCursorData);
    }
    private void StoreFlavorCursor(Cursor before, Cursor after)
    {
        _flavorCursor = new(before, after);
        _cachedFlavorCursor = FlavorQueueCursorData = JsonSerializer.Serialize(_flavorCursor);
    }
    private void RestoreFlavorCursor(Cursor cursor)
    {
        MoveTableIndex = cursor.Table; MoveTableStep = cursor.Step;
        DpOnceUsedMask = cursor.DpOnce; HpOnceUsedMask = cursor.HpOnce;
    }
    private void CommitFlavorCursor()
    {
        CursorPair cursors = ReadFlavorCursor();
        if (!FlavorTablePaused || cursors == null) return;
        if (MoveTableIndex == cursors.After.Table) RestoreFlavorCursor(cursors.After);
        else
        {
            // A real phase change owns its new table cursor. Do not restore an
            // old DP-table index over an HP-table reset.
            DpOnceUsedMask |= cursors.After.DpOnce; HpOnceUsedMask |= cursors.After.HpOnce;
        }
        FlavorTablePaused = false;
    }
    private void HoldFlavorCursor()
    {
        if (FlavorTablePaused || ReadFlavorCursor() is not { } cursor) return;
        RestoreFlavorCursor(cursor.Before); FlavorTablePaused = true;
    }
    private string ApplyFlavorChoice(string tableId, HbrEnemyFlavorChoice choice)
    {
        if (choice?.PauseTable == true) HoldFlavorCursor();
        else CommitFlavorCursor();
        FlavorQueueWasOverridden |= choice != null;
        return choice?.MoveId ?? tableId;
    }
    private string RollFlavorAwareTableMove(Rng rng)
    {
        if (!FlavorEnabled)
        {
            FlavorTablePaused = false; FlavorQueuedTableMoveId = "";
            FlavorQueueCursorData = ""; FlavorActionFinished = false;
            return RollTableMoveCore(rng);
        }
        if (FlavorPhaseReselectPending)
        {
            FlavorPhaseReselectPending = false; FlavorTablePaused = false;
            FlavorQueuedTableMoveId = ""; FlavorQueueCursorData = "";
        }
        if (FlavorTablePaused && !string.IsNullOrEmpty(FlavorQueuedTableMoveId))
        {
            FlavorActionFinished = false;
            HbrEnemyFlavorChoice held = ChooseFlavorMove(FlavorQueuedTableMoveId);
            if (held?.PauseTable != true) FlavorQueueWasOverridden = false;
            return ApplyFlavorChoice(FlavorQueuedTableMoveId, held);
        }
        Cursor before = CaptureFlavorCursor();
        string chosen = RollTableMoveCore(rng);
        StoreFlavorCursor(before, CaptureFlavorCursor());
        FlavorQueuedTableMoveId = chosen; FlavorActionFinished = false; FlavorQueueWasOverridden = false;
        return ApplyFlavorChoice(chosen, ChooseFlavorMove(chosen));
    }
    internal bool RefreshFlavorIntent()
    {
        if (!HasFlavorMechanics || Creature?.IsAlive != true || IsPerformingMove || MoveStateMachine == null) return false;
        if (FlavorPhaseReselectPending)
        {
            string id = RollFlavorAwareTableMove(Creature.CombatState.RunState.Rng.MonsterAi);
            SetMoveImmediate((MoveState)MoveStateMachine.States[id], true); return true;
        }
        if (FlavorActionFinished) return false;
        string queued = FlavorQueuedTableMoveId;
        if (string.IsNullOrEmpty(queued))
        {
            if (!TableMoves.Any(move => move.MoveId == NextMove?.Id)) return false;
            queued = FlavorQueuedTableMoveId = NextMove.Id;
            StoreFlavorCursor(CaptureFlavorCursor(), CaptureFlavorCursor());
        }
        string desired = ApplyFlavorChoice(queued, FlavorEnabled ? ChooseFlavorMove(queued) : null);
        if (desired == NextMove?.Id) return false;
        if (!MoveStateMachine.States.TryGetValue(desired, out MonsterState state) || state is not MoveState move)
            throw new InvalidOperationException(GetType().Name + " has no authored flavor state " + desired);
        // A canceled advertised move returns to the already selected table
        // option. An incidental extra AI roll cannot skip this unperformed step.
        move.MustPerformOnceBeforeTransitioning = true;
        SetMoveImmediate(move, true); return true;
    }
    private async Task ExecuteFlavorMove(string requested, IReadOnlyList<Creature> targets)
    {
        string chosen = requested;
        if (FlavorEnabled && !string.IsNullOrEmpty(FlavorQueuedTableMoveId))
            chosen = ApplyFlavorChoice(FlavorQueuedTableMoveId, ChooseFlavorMove(FlavorQueuedTableMoveId));
        await _flavorMoveBodies[chosen].PerformMove(targets);
        FlavorActionFinished = true;
    }
    private void OnFlavorTablePhaseChanged()
    {
        if (!FlavorEnabled || !FlavorReselectOnShellBreak || !FlavorQueueWasOverridden) return;
        // A real Break transition may select the new HP table once. Pure
        // intent/OD/DP reads never enter this branch or consume AI RNG.
        FlavorPhaseReselectPending = true;
        if (!IsPerformingMove) HbrEnemyFlavorSchedulingHelper.RefreshOwner(Creature);
    }
}
