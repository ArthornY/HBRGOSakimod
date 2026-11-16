using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BowCounterPower : PowerModel
{
    // Load-only counterpart to SynergyPower.BowCount.
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public override Task AfterApplied(Creature applier, CardModel cardSource) =>
        Owner?.GetPower<SynergyPower>()?.AbsorbLegacyCounters() ?? Task.CompletedTask;
}
