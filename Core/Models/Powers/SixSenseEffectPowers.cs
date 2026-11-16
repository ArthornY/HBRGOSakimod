using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>
/// The visible counters of the removed six-sense card effects. Nothing applies
/// them since 2026-11-09; they stay registered so an older combat state that lists
/// them loads, and they are hidden and change nothing.
/// </summary>
public abstract class SixSenseEffectPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
    public override bool ShouldPlayVfx => false;
    protected override bool IsVisibleInternal => false;
    public abstract string CustomPackedIconPath { get; }
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
}

public sealed class SixSenseLeadershipPower : SixSenseEffectPower
{
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/six_sense_leadership_power.tres";
}

public sealed class SixSenseGentlenessPower : SixSenseEffectPower
{
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/six_sense_gentleness_power.tres";
}

public sealed class SixSenseMentalStrengthPower : SixSenseEffectPower
{
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/six_sense_mental_strength_power.tres";
}
