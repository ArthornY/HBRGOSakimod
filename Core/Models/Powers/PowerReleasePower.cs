using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PowerReleasePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("E64F5E");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        RedCrimson redCrimson = Owner.CombatState?.Allies
            .Where(creature =>
                creature.IsAlive &&
                creature.PetOwner == Owner.Player)
            .Select(creature => creature.Monster)
            .OfType<RedCrimson>()
            .FirstOrDefault();
        redCrimson?.SetIntentCycleBonus(1);
        return Task.CompletedTask;
    }
}
