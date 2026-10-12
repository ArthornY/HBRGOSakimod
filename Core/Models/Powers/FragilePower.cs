using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FragilePower : PowerModel
{
    private const decimal _damageMultiplier = 1.5m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("DDA0DD");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side ||
            !participants.Contains(Owner) ||
            Amount <= 0 ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(this))
        {
            return;
        }

        Flash();
        await PowerCmd.TickDownDuration(this);
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        decimal multiplier = target == Owner &&
            amount > 0m &&
            Owner.HasPower<BreakPower>()
            ? _damageMultiplier
            : 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }
}
