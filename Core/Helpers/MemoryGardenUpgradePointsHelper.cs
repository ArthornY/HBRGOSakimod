using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MemoryGardenUpgradePointsHelper
{
    private sealed class BalanceState { internal int Depth; }
    private sealed class PurchaseState { internal int Busy; }
    private sealed record RoomStamp(string Key);
    private static readonly ConditionalWeakTable<ElectronicSoldierManual, BalanceState> Balances = new();
    private static readonly ConditionalWeakTable<ElectronicSoldierManual, PurchaseState> Purchases = new();
    private static readonly ConditionalWeakTable<AbstractRoom, RoomStamp> Rooms = new();

    internal static event Action<ElectronicSoldierManual> UpgradePointsChanged;

    internal static bool TryBeginPurchase(ElectronicSoldierManual manual, out IDisposable purchase)
    {
        purchase = null;
        if (manual?.IsMutable != true) return false;
        PurchaseState state = Purchases.GetOrCreateValue(manual);
        if (Interlocked.CompareExchange(ref state.Busy, 1, 0) != 0) return false;
        purchase = new PurchaseScope(state);
        return true;
    }

    private sealed class PurchaseScope : IDisposable
    {
        private PurchaseState _state;
        internal PurchaseScope(PurchaseState state) => _state = state;
        public void Dispose()
        {
            PurchaseState state = Interlocked.Exchange(ref _state, null);
            if (state != null) Volatile.Write(ref state.Busy, 0);
        }
    }

    internal static void NotifyUpgradePointsChanged(ElectronicSoldierManual manual,
        int previous, int current, string reason)
    {
        Log.Info($"[HBRGOSakiMod][MemoryGarden] Upgrade points: manual={manual.Id}, " +
            $"{previous}->{current}, reason={reason}");
        if (UpgradePointsChanged == null) return;
        // A UI observer must not interrupt an already committed grant/spend.
        foreach (Action<ElectronicSoldierManual> observer in UpgradePointsChanged.GetInvocationList())
        {
            try { observer(manual); }
            catch (Exception error) { Log.Warn($"[HBRGOSakiMod][MemoryGarden] Point display refresh failed: {error.Message}"); }
        }
    }

    internal static long GetTotalSixSensePoints(ElectronicSoldierManual manual)
    {
        if (manual == null) return 0;
        long total = Math.Max(0, manual.SixSensePoints);
        foreach (var attribute in ElectronicSoldierManual.AllSixSenseAttributes)
            total += Math.Max(0, manual.GetSenseAmount(attribute));
        return total;
    }

    internal static void Synchronize(ElectronicSoldierManual manual)
    {
        if (manual?.IsMutable != true || Balances.GetOrCreateValue(manual).Depth > 0) return;
        int tier = (int)Math.Min(int.MaxValue, GetTotalSixSensePoints(manual) / 60L);
        manual.ClaimMemoryGardenSixSenseTier(tier);
        if (manual.Owner?.Deck is { } deck && manual.Owner.Character is { } character)
            manual.ClaimMemoryGardenDeckSize(manual.MemoryGardenDeckRuleVersion == 0
                ? () => character.StartingDeck.Count() : null, deck.Cards.Count);
    }

    internal static IDisposable BeginBalanceUpdate(ElectronicSoldierManual manual)
    {
        Synchronize(manual);
        Balances.GetOrCreateValue(manual).Depth++;
        return new BalanceScope(manual);
    }
    private sealed class BalanceScope : IDisposable
    {
        private ElectronicSoldierManual _manual;
        internal BalanceScope(ElectronicSoldierManual manual) => _manual = manual;
        public void Dispose()
        {
            ElectronicSoldierManual manual = _manual;
            if (manual == null) return;
            _manual = null;
            BalanceState balance = Balances.GetOrCreateValue(manual);
            balance.Depth = Math.Max(0, balance.Depth - 1);
            Synchronize(manual);
        }
    }

    internal static string GetRoomKey(IRunState run, AbstractRoom room)
    {
        if (run == null || room == null) return string.Empty;
        return Rooms.GetValue(room, physicalRoom =>
        {
            // Id is regenerated on load and TotalFloor can change on same-node
            // re-entry. Map coordinates and room-history position are saved.
            // Cache the first physical-room stamp so a later nested room does
            // not make another callback for this object appear to be new.
            string location = run.CurrentMapCoord is { } coord
                ? FormattableString.Invariant($"{run.CurrentActIndex}:{coord.col}:{coord.row}")
                : FormattableString.Invariant($"{run.CurrentActIndex}:none");
            // A fire remains the same map fire when a story appends history or
            // reconstructs its RestSiteRoom. Battles inside events need slots.
            int slot = physicalRoom is RestSiteRoom ? 0 : Math.Max(1, run.CurrentMapPointHistoryEntry?.Rooms.Count ?? 1);
            return new RoomStamp(FormattableString.Invariant($"{location}:{slot}:{physicalRoom.RoomType}:{physicalRoom.ModelId}"));
        }).Key;
    }

    internal static void OnRoomEntered(ElectronicSoldierManual manual, AbstractRoom room)
    {
        if (manual?.IsMutable != true) return;
        Synchronize(manual);
        string key = GetRoomKey(manual.Owner?.RunState, room);
        if (room is RestSiteRoom) manual.ClaimMemoryGardenRoomPoints(key, 1);
    }

    internal static void OnCombatVictory(ElectronicSoldierManual manual, CombatRoom room)
    {
        if (manual?.IsMutable != true || room == null || !room.CombatState.Players.Contains(manual.Owner)) return;
        Synchronize(manual);
        int points = room.RoomType switch { RoomType.Elite => 1, RoomType.Boss => 2, _ => 0 };
        if (points > 0) manual.ClaimMemoryGardenRoomPoints(GetRoomKey(manual.Owner.RunState, room), points);
    }
}
