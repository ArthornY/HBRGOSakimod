using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class AmnesiaDrawReductionPower : PowerModel
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F36E75");

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player || AmountOnTurnStart <= 0)
        {
            return count;
        }

        return Math.Max(0m, count - AmountOnTurnStart);
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner) || AmountOnTurnStart <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            this,
            -Math.Min(Amount, AmountOnTurnStart),
            null,
            null);
    }
}
