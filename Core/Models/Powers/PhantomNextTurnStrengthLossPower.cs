using System;
using System.Collections.Generic;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhantomNextTurnStrengthLossPower : PowerModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsActive { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActiveAmount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingAmount { get; private set; }

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F36E75");

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (IsActive ||
            side != Owner.Side ||
            !participants.Contains(Owner) ||
            PendingAmount <= 0)
        {
            return;
        }

        AssertMutable();
        IsActive = true;
        ActiveAmount = PendingAmount;
        PendingAmount = 0;
        Flash();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            -ActiveAmount,
            Applier,
            null,
            silent: true);
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        if (power != this || amount <= 0m)
        {
            return Task.CompletedTask;
        }

        AssertMutable();
        PendingAmount = (int)Math.Min(
            int.MaxValue,
            (long)PendingAmount + (int)amount);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!IsActive ||
            side != Owner.Side ||
            !participants.Contains(Owner))
        {
            return;
        }

        int restoreAmount = ActiveAmount;
        AssertMutable();
        IsActive = false;
        ActiveAmount = 0;
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            restoreAmount,
            Owner,
            null,
            silent: true);
        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -restoreAmount,
            Owner,
            null);
    }
}
