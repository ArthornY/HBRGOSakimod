using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class HangingShotPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    public override Color AmountLabelColor => new("E88954");

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Applier == null ||
            side != Applier.Side ||
            !Owner.IsHittable ||
            Amount <= 0)
        {
            return;
        }

        Flash();
        decimal damage = Amount;
        if (Owner.GetPower<HighDimensionalShellPower>()
                is { Amount: > 0 })
        {
            damage *= 1.25m;
        }

        await CreatureCommandCompatibility.Damage(
            choiceContext,
            Owner,
            damage,
            ValueProp.Move,
            Applier);
        await PowerCmd.Remove(this);
    }
}
