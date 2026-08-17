using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class EatPassersbyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel cardSource)
    {
        if (creature != Owner ||
            amount <= 0m ||
            Owner.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Creature[] targets = Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature => creature.IsAlive)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            targets,
            Amount,
            ValueProp.Unpowered,
            Owner,
            null);
    }
}
