using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>
/// Enemy six-sense resistances were removed (2026-11-09) and are no longer applied.
/// The power stays registered, with its saved values, so an older combat state that
/// still lists it loads; it is hidden and changes nothing.
/// </summary>
public sealed class SixSenseResistancePower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] Resistances { get; private set; } = Array.Empty<int>();

    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
    public override bool ShouldPlayVfx => false;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        Resistances = Resistances?.ToArray() ?? Array.Empty<int>();
    }
}
