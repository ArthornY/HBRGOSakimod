using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FocusedShotPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (target != Owner ||
            block <= 0m ||
            !Owner.HasPower<ReptureBowPower>())
        {
            return 1m;
        }

        return 0.75m;
    }

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel cardSource)
    {
        if (creature != Owner ||
            amount <= 0m ||
            !Owner.HasPower<ReptureBowPower>())
        {
            return;
        }

        decimal strength = amount / 12m;
        Flash();
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            strength,
            Owner,
            null);
    }
}
