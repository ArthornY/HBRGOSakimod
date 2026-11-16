using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    private int _memoryGardenSixSenseRuleVersion;
    private int _memoryGardenInitialDeckSize;
    private int _memoryGardenDeckHighWater;
    private int _memoryGardenDeckUpgradeTier;
    private int _memoryGardenDeckRuleVersion;
    private string _memoryGardenUpgradeRewardedRooms = string.Empty;
    private static readonly ConditionalWeakTable<ElectronicSoldierManual, object> SixSenseRuleMigrationGates = new();
    private static readonly ConditionalWeakTable<ElectronicSoldierManual, object> DeckRewardGates = new();
    private const int CurrentSixSensePointRuleVersion = 1;

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

    // Version 0 stored the number of claimed 30-point milestones. Keep the
    // independent earned/spent balance while converting that receipt to 60s.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenSixSenseRuleVersion
    {
        get => Volatile.Read(ref _memoryGardenSixSenseRuleVersion);
        protected set => Volatile.Write(ref _memoryGardenSixSenseRuleVersion, Math.Max(0, value));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenInitialDeckSize
    {
        get => Volatile.Read(ref _memoryGardenInitialDeckSize);
        protected set => Volatile.Write(ref _memoryGardenInitialDeckSize, Math.Max(0, value));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenDeckHighWater
    {
        get => Volatile.Read(ref _memoryGardenDeckHighWater);
        protected set => Volatile.Write(ref _memoryGardenDeckHighWater, Math.Max(0, value));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenDeckUpgradeTier
    {
        get => Volatile.Read(ref _memoryGardenDeckUpgradeTier);
        protected set => Volatile.Write(ref _memoryGardenDeckUpgradeTier, Math.Max(0, value));
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MemoryGardenDeckRuleVersion
    {
        get => Volatile.Read(ref _memoryGardenDeckRuleVersion);
        protected set => Volatile.Write(ref _memoryGardenDeckRuleVersion, Math.Max(0, value));
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
        EnsureMemoryGardenSixSenseRule();
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

    private void EnsureMemoryGardenSixSenseRule()
    {
        if (MemoryGardenSixSenseRuleVersion >= CurrentSixSensePointRuleVersion) return;
        // The claim CAS below must never observe half of this version/receipt
        // conversion. Migration performs no grants or callbacks under the gate.
        lock (SixSenseRuleMigrationGates.GetOrCreateValue(this))
        {
            if (MemoryGardenSixSenseRuleVersion >= CurrentSixSensePointRuleVersion) return;
            MemoryGardenSixSenseUpgradeTier /= 2;
            MemoryGardenSixSenseRuleVersion = CurrentSixSensePointRuleVersion;
        }
    }

    internal void ClaimMemoryGardenDeckSize(Func<int> getInitialSize, int currentSize)
    {
        AssertMutable();
        int points;
        int tier;
        lock (DeckRewardGates.GetOrCreateValue(this))
        {
            if (MemoryGardenDeckRuleVersion == 0)
            {
                // Read the character's real starting deck only once. A later
                // room, reload or manual replacement uses the saved baseline.
                MemoryGardenInitialDeckSize = Math.Max(0, getInitialSize());
                MemoryGardenDeckRuleVersion = 1;
            }
            int highest = Math.Max(MemoryGardenInitialDeckSize,
                Math.Max(MemoryGardenDeckHighWater, Math.Max(0, currentSize)));
            MemoryGardenDeckHighWater = highest;
            tier = (highest - MemoryGardenInitialDeckSize) / 3;
            int previous = MemoryGardenDeckUpgradeTier;
            if (tier <= previous) return;
            // Claim before callbacks, and never decrease on card removal.
            MemoryGardenDeckUpgradeTier = tier;
            points = tier - previous;
        }
        AddMemoryGardenUpgradePointsCore(points, "deck-tier:" + tier);
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
