using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DualSwordsCounterPower : PowerModel
{
    // Compatibility only: current Synergy stores this value inside its own
    // saved state and imports any legacy model when either power is applied.
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    public override Task AfterApplied(Creature applier, CardModel cardSource) =>
        Owner?.GetPower<SynergyPower>()?.AbsorbLegacyCounters() ?? Task.CompletedTask;
}
