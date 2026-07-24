using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class FeintDamageRedirectPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override bool ShouldPlayVfx => false;

    public override Creature ModifyUnblockedDamageTarget(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer)
    {
        if (amount <= 0m ||
            target == null ||
            target == Owner ||
            target.Side != Owner.Side ||
            Owner.IsDead)
        {
            return target;
        }

        return Owner;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
