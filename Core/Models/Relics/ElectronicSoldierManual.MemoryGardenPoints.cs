using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Relics;

public partial class ElectronicSoldierManual
{
    private int _memoryGardenUpgradePoints;
    private int _memoryGardenSixSenseUpgradeTier;
    private string _memoryGardenUpgradeRewardedRooms = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenUpgradePoints
    {
        get => Volatile.Read(ref _memoryGardenUpgradePoints);
        protected set => Volatile.Write(ref _memoryGardenUpgradePoints, Math.Max(0, value));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenSixSenseUpgradeTier
    {
        get => Volatile.Read(ref _memoryGardenSixSenseUpgradeTier);
        protected set => Volatile.Write(ref _memoryGardenSixSenseUpgradeTier, Math.Max(0, value));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string MemoryGardenUpgradeRewardedRooms
    {
        get => Volatile.Read(ref _memoryGardenUpgradeRewardedRooms) ?? string.Empty;
        protected set => Volatile.Write(ref _memoryGardenUpgradeRewardedRooms, value ?? string.Empty);
    }

    internal void SynchronizeMemoryGardenUpgradePoints() => MemoryGardenUpgradePointsHelper.Synchronize(this);

    internal bool TrySpendMemoryGardenUpgradePoint()
    {
        if (!IsMutable) return false;
        SynchronizeMemoryGardenUpgradePoints();
        while (true)
        {
            int previous = MemoryGardenUpgradePoints;
            if (previous <= 0) return false;
            if (Interlocked.CompareExchange(ref _memoryGardenUpgradePoints, previous - 1, previous) == previous)
            {
                MemoryGardenUpgradePointsHelper.NotifyUpgradePointsChanged(this, previous, previous - 1, "spend");
                return true;
            }
        }
    }

    internal void RefundMemoryGardenUpgradePoint()
    {
        AssertMutable();
        AddMemoryGardenUpgradePointsCore(1, "refund");
    }

    internal void AddMemoryGardenUpgradePoints(int amount)
        => AddMemoryGardenUpgradePointsCore(amount, "grant");

    private void AddMemoryGardenUpgradePointsCore(int amount, string reason)
    {
        AssertMutable();
        if (amount <= 0) return;
        while (true)
        {
            int previous = MemoryGardenUpgradePoints;
            int next = (int)Math.Min(int.MaxValue, (long)previous + amount);
            if (Interlocked.CompareExchange(ref _memoryGardenUpgradePoints, next, previous) != previous) continue;
            if (next != previous)
            {
                MemoryGardenUpgradePointsHelper.NotifyUpgradePointsChanged(this, previous, next, reason);
                Flash();
            }
            return;
        }
    }

    internal void ClaimMemoryGardenSixSenseTier(int tier)
    {
        AssertMutable();
        while (true)
        {
            int previous = MemoryGardenSixSenseUpgradeTier;
            if (tier <= previous) return;
            // Commit the high-water mark before callbacks from the new points.
            if (Interlocked.CompareExchange(ref _memoryGardenSixSenseUpgradeTier, tier, previous) != previous) continue;
            AddMemoryGardenUpgradePointsCore(tier - previous, "six-sense-tier:" + tier);
            return;
        }
    }

    internal bool ClaimMemoryGardenRoomPoints(string key, int amount)
    {
        AssertMutable();
        if (string.IsNullOrEmpty(key) || amount <= 0) return false;
        while (true)
        {
            string previous = Volatile.Read(ref _memoryGardenUpgradeRewardedRooms);
            HashSet<string> claims = new((previous ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            if (!claims.Add(key)) return false;
            string next = string.Join("\n", claims.OrderBy(value => value, StringComparer.Ordinal));
            if (!ReferenceEquals(Interlocked.CompareExchange(ref _memoryGardenUpgradeRewardedRooms, next, previous), previous)) continue;
            AddMemoryGardenUpgradePointsCore(amount, "room:" + key);
            return true;
        }
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        GrantHbrBattleVictory(room);
        MemoryGardenUpgradePointsHelper.OnCombatVictory(this, room);
        return Task.CompletedTask;
    }
}
