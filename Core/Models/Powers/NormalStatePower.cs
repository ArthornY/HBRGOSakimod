using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NormalStatePower : PowerModel
{
    private const decimal _dpRecoveryPercent = 0.05m;
    private const decimal _cardDodgeMultiplier = 0.75m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
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

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature target, CardModel cardSource)
    {
        if (power is not DodgePower || giver != Owner || target != Owner || amount <= 0m)
        {
            return 0m;
        }

        if (cardSource == null || cardSource.Owner != Owner.Player)
        {
            return 0m;
        }

        return Math.Ceiling(amount * _cardDodgeMultiplier) - amount;
    }
}
