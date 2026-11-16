using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Helpers;

// 0.113 remembers forced ordinary-turn votes separately from the ready set.
// A personal inserted turn borrows only its owner's vote and constraint; the
// native Undo method still performs its normal notifications and UI update.
internal sealed class PersonalTurnReadyGuard
{
    private static readonly FieldInfo TurnState = AccessTools.Field(typeof(CombatManager), "_turnState");
    private static readonly PropertyInfo CannotUndo = TurnState == null ? null :
        AccessTools.Property(TurnState.FieldType, "PlayersThatCannotUnEndTurn");
    private static readonly PropertyInfo ReadyLock = TurnState == null ? null :
        AccessTools.Property(TurnState.FieldType, "ReadyLock");
    // Stable exposes canBackOut only on its accepted-vote event. Keep that
    // event value per combat, without polling or retaining a finished combat.
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, bool>> VoteRules = new();

    internal static void RecordNativeVote(Player player, bool canBackOut)
    {
        if (player?.Creature.CombatState is { } combat)
            VoteRules.GetOrCreateValue(combat)[player] = canBackOut;
    }

    internal static void ForgetNativeVote(Player player)
    {
        if (player?.Creature.CombatState is { } combat && VoteRules.TryGetValue(combat, out var votes))
            votes.Remove(player);
    }

    internal static void ClearVotes(ICombatState combat)
    {
        if (combat != null) VoteRules.Remove(combat);
    }

    private static bool RecordedCanBackOut(Player player) =>
        player?.Creature.CombatState is not { } combat || !VoteRules.TryGetValue(combat, out var votes) ||
        !votes.TryGetValue(player, out bool canBackOut) || canBackOut;

    private readonly CombatManager _manager;
    private readonly Player _owner;
    private readonly int _turnNumber;
    private readonly object _turn;
    private readonly HashSet<Player> _cannotUndo;
    private readonly Lock _readyLock;

    internal bool WasForced { get; }

    internal PersonalTurnReadyGuard(CombatManager manager, Player owner)
    {
        _manager = manager;
        _owner = owner;
        _turnNumber = owner.PlayerCombatState?.TurnNumber ?? -1;
        _turn = TurnState?.GetValue(manager);
        _cannotUndo = _turn == null ? null : CannotUndo?.GetValue(_turn) as HashSet<Player>;
        _readyLock = _turn == null ? null : ReadyLock?.GetValue(_turn) as Lock;
        WasForced = !RecordedCanBackOut(owner);
        if (_cannotUndo != null)
        {
            using (_readyLock.EnterScope()) WasForced |= _cannotUndo.Contains(owner);
        }
    }

    private bool IsSameTurn => _turn != null && ReferenceEquals(TurnState?.GetValue(_manager), _turn);

    internal void UndoReady()
    {
        if (_cannotUndo != null && IsSameTurn)
        {
            using (_readyLock.EnterScope()) _cannotUndo.Remove(_owner);
        }
        _manager.UndoReadyToEndTurnCompat(_owner);
    }

    internal void RestoreConstraint()
    {
        if ((_turn != null && !IsSameTurn) || _owner.PlayerCombatState?.TurnNumber != _turnNumber) return;
        // The beta signal may resume its ordinary-turn continuation inline.
        // Do not revive a vote/constraint native code has already cleared.
        if (!_manager.IsPlayerReadyToEndTurn(_owner))
        {
            ForgetNativeVote(_owner);
            return;
        }
        if (_cannotUndo != null)
        {
            using (_readyLock.EnterScope())
            {
                if (WasForced) _cannotUndo.Add(_owner);
                else _cannotUndo.Remove(_owner);
            }
        }
        RecordNativeVote(_owner, !WasForced);
    }
}
