using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NormalStatePower : PowerModel
{
    private const decimal _dpRecoveryPercent = 0.05m;
    private const decimal _blockMultiplier = 0.75m;
    private const decimal _outgoingDamageMultiplier = 0.5m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            return;
        }

        DeflectorProtection deflectorProtection = Owner.Player?.GetRelic<DeflectorProtection>();
        if (deflectorProtection == null)
        {
            return;
        }

        int restoredDp = deflectorProtection.RestoreDpByMaxPercent(_dpRecoveryPercent);
        if (restoredDp > 0)
        {
            Flash();
        }

        await Task.CompletedTask;
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (target != Owner || block <= 0m)
        {
            return 1m;
        }

        return _blockMultiplier;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        return dealer == Owner && amount > 0m
            ? _outgoingDamageMultiplier
            : 1m;
    }
}
