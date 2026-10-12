using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed class CardDamageHistoryEntry
{
    internal int Sequence { get; init; }
    internal string CardName { get; init; }
    internal decimal TotalDamage { get; set; }
    internal bool IsComplete { get; set; }
}

// A root wrapper owns the whole causal chain, including nested automatic cards
// and phantom actions with no card source. This is separate from damage limits.
internal static class CardDamageHistoryHelper
{
    private sealed class BattleHistory
    {
        internal readonly Dictionary<Player, List<CardDamageHistoryEntry>> Players = new();
    }

    private sealed record ExactBlockedDamage(decimal Amount);

    internal sealed class PlayScope
    {
        internal PlayScope Previous;
        internal PlayScope Root;
        internal Player Player;
        internal ICombatState Combat;
        internal CombatSide Side;
        internal CardDamageHistoryEntry Entry;
        internal bool Active = true;
    }

    private static readonly AsyncLocal<PlayScope> Current = new();
    private static readonly ConditionalWeakTable<ICombatState, BattleHistory> Battles = new();
    private static readonly ConditionalWeakTable<DamageResult, ExactBlockedDamage> ExactBlocked = new();
    private static readonly object DamageListenerLock = new();
    private static Action<Player, CardDamageHistoryEntry> _damageUpdated;
    private static volatile Delegate[] _damageListeners = Array.Empty<Delegate>();

    internal static event Action<Player> HistoryChanged;
    internal static event Action<Player, CardDamageHistoryEntry> DamageUpdated
    {
        add
        {
            lock (DamageListenerLock)
            {
                _damageUpdated += value;
                _damageListeners = _damageUpdated?.GetInvocationList() ?? Array.Empty<Delegate>();
            }
        }
        remove
        {
            lock (DamageListenerLock)
            {
                _damageUpdated -= value;
                _damageListeners = _damageUpdated?.GetInvocationList() ?? Array.Empty<Delegate>();
            }
        }
    }

    private static void NotifyDamageUpdated(PlayScope root)
    {
        // Cache the subscriber snapshot on subscription changes, not each hit.
        foreach (Action<Player, CardDamageHistoryEntry> listener in _damageListeners)
        {
            try { listener(root.Player, root.Entry); }
            catch (Exception error)
            {
                // A disposed/broken display cannot interrupt damage resolution
                // or prevent the remaining displays from receiving this receipt.
                DamageUpdated -= listener;
                Log.Warn($"[HBRGOSakiMod] Damage display update failed: {error.Message}");
            }
        }
    }

    internal static IReadOnlyList<CardDamageHistoryEntry> GetEntries(Player player)
    {
        ICombatState combat = player?.Creature?.CombatState;
        return combat != null && Battles.TryGetValue(combat, out BattleHistory history) &&
            history.Players.TryGetValue(player, out List<CardDamageHistoryEntry> entries)
                ? entries : Array.Empty<CardDamageHistoryEntry>();
    }

    internal static PlayScope Begin(CardModel card)
    {
        PlayScope previous = Current.Value;
        PlayScope parent = previous;
        while (parent != null && !parent.Active) parent = parent.Previous;
        PlayScope scope = new() { Previous = previous };
        if (parent?.Root?.Active == true)
        {
            scope.Root = parent.Root;
        }
        else if (card?.Owner?.Creature?.CombatState is { } combat)
        {
            scope.Root = scope;
            scope.Player = card.Owner;
            scope.Combat = combat;
            scope.Side = card.Owner.Creature.Side;
            BattleHistory history = Battles.GetValue(combat, static _ => new BattleHistory());
            if (!history.Players.TryGetValue(scope.Player, out List<CardDamageHistoryEntry> entries))
                history.Players.Add(scope.Player, entries = new());
            scope.Entry = new CardDamageHistoryEntry
            {
                Sequence = entries.Count + 1,
                CardName = card.Title
            };
            entries.Add(scope.Entry);
        }
        Current.Value = scope;
        if (scope.Entry != null) HistoryChanged?.Invoke(scope.Player);
        return scope;
    }

    internal static void RestoreCaller(PlayScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope))
            Current.Value = scope.Previous;
    }

    internal static void Close(PlayScope scope)
    {
        if (scope?.Active != true) return;
        scope.Active = false;
        RestoreCaller(scope);
        if (scope.Entry == null) return;
        scope.Entry.IsComplete = true;
        HistoryChanged?.Invoke(scope.Player);
    }

    // Native results project decimal block/DP absorption into Int32. Preserve
    // only values that cannot fit, without changing that native projection.
    internal static void SetBlockedDamage(DamageResult result, decimal amount)
    {
        ExactBlocked.Remove(result);
        if (amount > int.MaxValue || amount < int.MinValue)
            ExactBlocked.Add(result, new ExactBlockedDamage(amount));
        result.BlockedDamage = (int)Math.Clamp(amount, int.MinValue, int.MaxValue);
    }

    // Each list belongs to one original target. Redirected overkill is the input
    // to the final original-target result, so include it only on the last entry.
    internal static decimal GetDamage(IReadOnlyList<DamageResult> hitResults, int index)
    {
        DamageResult result = hitResults[index];
        decimal blocked = ExactBlocked.TryGetValue(result, out ExactBlockedDamage exact)
            ? exact.Amount : (decimal)result.BlockedDamage;
        return Math.Max(0m, blocked) + Math.Max(0m, (decimal)result.UnblockedDamage) +
            (index == hitResults.Count - 1 ? Math.Max(0m, (decimal)result.OverkillDamage) : 0m);
    }

    internal static void RecordHit(IReadOnlyList<DamageResult> hitResults)
    {
        PlayScope root = Current.Value?.Root;
        if (root?.Active != true || root.Entry == null) return;
        for (int index = 0; index < hitResults.Count; index++)
        {
            Creature receiver = hitResults[index].Receiver;
            if (receiver == null || receiver.Side == root.Side ||
                !ReferenceEquals(receiver.CombatState, root.Combat)) continue;
            decimal amount = GetDamage(hitResults, index);
            if (amount <= 0m) continue;
            root.Entry.TotalDamage = AddDamage(root.Entry.TotalDamage, amount);
            RunPeakCardDamageHelper.Update(root.Player, root.Entry.TotalDamage);
            NotifyDamageUpdated(root);
        }
        // Damage receipts update one displayed value immediately. Structural
        // history notifications remain limited to root start and completion.
    }

    // Explicit DP loss has no native DamageResult. Absorption callers report
    // only DP beyond the incoming hit, whose ordinary portion is already counted.
    internal static void RecordDpDamage(Creature receiver, decimal amount)
    {
        if (receiver == null || amount <= 0m) return;
        HighHitDamageVisualHelper.RecordDpDamage(receiver, amount);
        PlayScope root = Current.Value?.Root;
        if (root?.Active != true || root.Entry == null || receiver.Side == root.Side ||
            !ReferenceEquals(receiver.CombatState, root.Combat)) return;
        root.Entry.TotalDamage = AddDamage(root.Entry.TotalDamage, amount);
        RunPeakCardDamageHelper.Update(root.Player, root.Entry.TotalDamage);
        NotifyDamageUpdated(root);
    }

    internal static decimal AddDamage(decimal total, decimal amount) =>
        amount >= decimal.MaxValue - total ? decimal.MaxValue : total + amount;
}
