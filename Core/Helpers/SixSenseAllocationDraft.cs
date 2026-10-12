using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed class SixSenseAllocationDraft
{
    private readonly ElectronicSoldierManual _manual;
    private readonly int[] _increases = new int[6];
    private int _revision;

    internal SixSenseAllocationDraft(ElectronicSoldierManual manual)
    {
        _manual = manual;
        _revision = manual.SixSenseAllocationRevision;
    }
    internal int Total => _increases.Sum();
    internal int Remaining => Math.Max(0, _manual.SixSensePoints - Total);
    internal int Added(SixSenseAttribute attribute) => _increases[(int)attribute];
    internal int Level(SixSenseAttribute attribute) => _manual.GetSenseAmount(attribute) + Added(attribute);
    internal int[] Snapshot() => (int[])_increases.Clone();
    internal void Clear() => Array.Clear(_increases);

    internal bool Adjust(SixSenseAttribute attribute, int direction)
    {
        Reconcile();
        if (!Enum.IsDefined(attribute) || direction is not (1 or -1)) return false;
        if (direction > 0 && (Remaining == 0 || Level(attribute) >= ElectronicSoldierManual.MaxSixSense)) return false;
        if (direction < 0 && Added(attribute) == 0) return false;
        _increases[(int)attribute] += direction;
        return true;
    }

    internal void Reconcile()
    {
        // Queuing a confirmation is not an acknowledgement. Keep showing the
        // draft until the synchronized allocation has updated the real levels.
        if (_revision != _manual.SixSenseAllocationRevision)
        {
            Clear();
            _revision = _manual.SixSenseAllocationRevision;
        }
        int budget = _manual.SixSensePoints;
        foreach (SixSenseAttribute attribute in ElectronicSoldierManual.AllSixSenseAttributes)
        {
            int i = (int)attribute;
            _increases[i] = Math.Min(_increases[i], Math.Min(budget,
                Math.Max(0, ElectronicSoldierManual.MaxSixSense - _manual.GetSenseAmount(attribute))));
            budget -= _increases[i];
        }
    }
}

internal sealed class SixSensePointRepeat
{
    private double _elapsed;
    private double _next = 0.4;

    internal void Reset() { _elapsed = 0; _next = 0.4; }
    internal static double Interval(double heldSeconds) => Math.Max(0.025, 0.18 * Math.Exp(-Math.Max(0, heldSeconds - 0.4) * 0.8));

    internal int Advance(double delta)
    {
        _elapsed += Math.Clamp(delta, 0, 0.25);
        int count = 0;
        while (_elapsed >= _next && count < 10)
        {
            _next += Interval(_next);
            count++;
        }
        return count;
    }
}
