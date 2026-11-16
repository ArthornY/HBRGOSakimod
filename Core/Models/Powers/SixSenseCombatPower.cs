using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>
/// Held the card-play effects of the removed six-sense card attributes (leadership,
/// gentleness, mental strength) and the resistance-break count. Both layers were
/// removed on 2026-11-09; nothing applies this power any more. It stays registered,
/// with its saved values, so an older combat state that lists it loads; it is
/// hidden and changes nothing.
/// </summary>
public sealed class SixSenseCombatPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LeadershipMilli { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int GentlenessMilli { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MentalStrengthMilli { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ResistanceBreaks { get; private set; }

    protected override bool IsVisibleInternal => false;
    public override bool ShouldPlayVfx => false;
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
}
