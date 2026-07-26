using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FragilePower : PowerModel
{
    private const decimal _damageMultiplier = 1.5m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("DDA0DD");

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return target == Owner && amount > 0m && props.IsPoweredAttack()
            ? _damageMultiplier
            : 1m;
    }
}
