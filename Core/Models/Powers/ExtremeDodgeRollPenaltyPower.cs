using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ExtremeDodgeRollPenaltyPower : PowerModel
{
    public const int RollPenalty = -10;

    private int _reservedRolls;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F36E75");

    internal bool TryReserveRoll()
    {
        if (Amount - _reservedRolls <= 0)
        {
            return false;
        }

        AssertMutable();
        _reservedRolls++;
        Flash();
        return true;
    }

    internal async Task ConsumeReservedRolls(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        int amount = Math.Min(Amount, _reservedRolls);
        AssertMutable();
        _reservedRolls = 0;
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -amount,
            Owner,
            cardSource);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedRolls = 0;
    }
}
