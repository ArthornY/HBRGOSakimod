using System;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// Native/large HP remains the total remaining life. The power counter records
// which of its four integral partitions is active, without death/revival or
// manufacturing additional health at a phase transition.
internal static class NararaLayeredHpHelper
{
    internal const int MaximumLayers = 4;
    [ThreadStatic] private static int _deathDrainDepth;

    private sealed class DeathDrain : IDisposable
    {
        private bool _disposed;
        internal DeathDrain() => _deathDrainDepth++;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _deathDrainDepth--;
        }
    }

    internal static IDisposable BypassForKill() => new DeathDrain();

    private static NararaLayeredHpPower Power(Creature creature) =>
        creature?.Side == MegaCrit.Sts2.Core.Combat.CombatSide.Enemy &&
        creature.Monster is NararaShibaBossMonster
            ? creature.GetPower<NararaLayeredHpPower>() : null;

    internal static bool IsEnabled(Creature creature) => Power(creature) is { Amount: > 0 } &&
        LargeCreatureHealthHelper.GetMaxHp(creature) > 0m;

    private static decimal ReadBudget(string data, decimal fallback) =>
        BigCombatNumbers.ClampStat(BigCombatNumbers.ReadSigned(data, fallback));

    private static decimal InitialMaximum(NararaLayeredHpPower power, Creature creature) =>
        ReadBudget(power.PartitionMaximumHpData, LargeCreatureHealthHelper.GetMaxHp(creature));

    private static decimal SpentMaximum(NararaLayeredHpPower power, Creature creature) =>
        ReadBudget(power.SpentMaximumHpData, InitialMaximum(power, creature) -
            Boundary(InitialMaximum(power, creature), Math.Clamp(power.Amount, 0, MaximumLayers)));

    internal static void InitializePartitions(Creature creature)
    {
        NararaLayeredHpPower power = Power(creature);
        if (power == null || !string.IsNullOrEmpty(power.PartitionMaximumHpData)) return;
        decimal maximum = BigCombatNumbers.ClampStat(LargeCreatureHealthHelper.GetMaxHp(creature));
        power.PartitionMaximumHpData = BigCombatNumbers.Save(maximum);
        power.SpentMaximumHpData = BigCombatNumbers.Save(maximum - Boundary(maximum,
            Math.Clamp(power.Amount, 0, MaximumLayers)));
    }

    internal static int CalculateRemainingLayers(decimal current, decimal maximum)
    {
        if (current <= 0m || maximum <= 0m) return 0;
        int remaining = MaximumLayers;
        while (remaining > 1 && current <= Boundary(maximum, remaining - 1)) remaining--;
        return remaining;
    }

    internal static int GetRemainingLayers(Creature creature)
    {
        NararaLayeredHpPower power = Power(creature);
        if (power == null) return 0;
        return LargeCreatureHealthHelper.GetCurrentHp(creature) <= 0m ? 0 :
            Math.Clamp(power.Amount, 0, MaximumLayers);
    }

    // Odd totals differ by at most one HP. Even a tiny total never gains HP
    // merely to make four positive integral partitions.
    private static decimal Boundary(decimal maximum, int remaining) =>
        decimal.Floor(BigCombatNumbers.ClampStat(maximum) * Math.Clamp(remaining, 0, MaximumLayers) / MaximumLayers);

    internal static decimal GetActiveCurrentHp(Creature creature)
    {
        int remaining = GetRemainingLayers(creature);
        if (remaining == 0) return 0m;
        return Math.Clamp(LargeCreatureHealthHelper.GetCurrentHp(creature) -
            Boundary(InitialMaximum(Power(creature), creature), remaining - 1), 0m, GetActiveMaximumHp(creature));
    }

    internal static decimal GetActiveMaximumHp(Creature creature)
    {
        int remaining = GetRemainingLayers(creature);
        if (remaining == 0) return 0m;
        NararaLayeredHpPower power = Power(creature);
        return Math.Max(0m, LargeCreatureHealthHelper.GetMaxHp(creature) - SpentMaximum(power, creature) -
            Boundary(InitialMaximum(power, creature), remaining - 1));
    }

    internal static decimal BeforeMaximumWrite(Creature creature, decimal current, decimal maximum, out bool broke)
    {
        broke = false;
        NararaLayeredHpPower power = Power(creature);
        if (power == null || power.Amount <= 0) return current;
        InitializePartitions(creature);
        decimal oldMaximum = LargeCreatureHealthHelper.GetMaxHp(creature);
        decimal spent = SpentMaximum(power, creature);
        // Growth belongs to the current life, not every reserve. A reduction
        // first shrinks already-spent capacity, preserving native surviving HP.
        if (maximum < oldMaximum) spent = Math.Max(0m, spent - (oldMaximum - maximum));
        power.SpentMaximumHpData = BigCombatNumbers.Save(spent);
        decimal ceiling = Math.Max(0m, maximum - spent);
        int remaining = Math.Clamp(power.Amount, 0, MaximumLayers);
        while (remaining > 0 && ceiling <= Boundary(InitialMaximum(power, creature), remaining - 1)) remaining--;
        if (remaining != power.Amount)
        {
            NararaHpTransitionBoundary.RecordBreak(creature);
            power.SetAmount(remaining);
            broke = true;
        }
        return Math.Min(current, ceiling);
    }

    internal static decimal ClampHpLoss(Creature creature, decimal amount)
    {
        if (_deathDrainDepth > 0 || amount <= 0m || !IsEnabled(creature)) return amount;
        if (NararaHpTransitionBoundary.BlocksHpLoss(creature)) return 0m;
        decimal current = LargeCreatureHealthHelper.GetCurrentHp(creature);
        int remaining = GetRemainingLayers(creature);
        decimal floor = Boundary(InitialMaximum(Power(creature), creature), remaining - 1);
        return Math.Min(amount, Math.Max(0m, current - floor));
    }

    internal static decimal BeforeHpWrite(Creature creature, decimal requested, out bool broke)
    {
        broke = false;
        NararaLayeredHpPower power = Power(creature);
        if (power == null || power.Amount <= 0) return requested;
        InitializePartitions(creature);
        if (_deathDrainDepth > 0)
        {
            if (requested <= 0m) power.SetAmount(0);
            return requested;
        }
        decimal current = LargeCreatureHealthHelper.GetCurrentHp(creature);
        int remaining = GetRemainingLayers(creature);
        if (remaining <= 0) return requested;
        if (remaining != power.Amount) power.SetAmount(remaining);
        decimal maximum = InitialMaximum(power, creature);
        decimal ceiling = Math.Max(0m, LargeCreatureHealthHelper.GetMaxHp(creature) - SpentMaximum(power, creature));
        if (requested >= current) return Math.Min(requested, ceiling);
        if (NararaHpTransitionBoundary.BlocksHpLoss(creature)) return current;
        decimal floor = Boundary(maximum, remaining - 1);
        requested = Math.Max(requested, floor);
        if (current <= floor || requested > floor) return requested;
        int next = remaining - 1;
        while (next > 0 && Boundary(maximum, next) <= Boundary(maximum, next - 1)) next--;
        // Commit the barrier and phase before native HP events can reenter.
        NararaHpTransitionBoundary.RecordBreak(creature);
        power.SpentMaximumHpData = BigCombatNumbers.Save(SpentMaximum(power, creature) +
            Math.Max(0m, ceiling - floor));
        power.SetAmount(next);
        broke = true;
        return requested;
    }

    internal static void AfterHpWrite(Creature creature, bool broke)
    {
        if (broke && creature.IsAlive) NararaForcedTurnEndHelper.Request(creature);
    }
}
