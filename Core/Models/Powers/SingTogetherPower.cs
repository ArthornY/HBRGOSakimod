using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SingTogetherPower : PowerModel
{
    public const int MaxCount = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || canonicalPower is not SingTogetherPower || amount <= 0m)
        {
            return false;
        }

        modifiedAmount = System.Math.Min(amount, System.Math.Max(0, MaxCount - Amount));
        return modifiedAmount != amount;
    }

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (!Owner.HasPower<PreparationPower>() && !Owner.HasPower<HighSongPower>())
        {
            await PowerCmd.Apply<PreparationPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                1m,
                applier ?? Owner,
                cardSource);
        }
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature applier,
        CardModel cardSource)
    {
        return power is SingTogetherPower &&
               power.Owner == Owner &&
               power.Amount >= MaxCount
            ? TryTransform(choiceContext)
            : Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Owner.HasPower<HighSongPower>())
        {
            return;
        }

        if (Amount < MaxCount)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                this,
                MaxCount > Amount ? 1m : 0m,
                Owner,
                null);
        }

        await TryTransform(choiceContext);
    }

    private async Task TryTransform(PlayerChoiceContext choiceContext)
    {
        if (Amount < MaxCount || Owner.HasPower<HighSongPower>())
        {
            return;
        }

        Flash();
        if (Owner.HasPower<PreparationPower>())
        {
            await PowerCmd.Remove(Owner.GetPower<PreparationPower>());
        }

        await PowerCmd.Apply<HighSongPower>(
            choiceContext,
            Owner,
            HighSongPower.GetInitialAmount(Owner),
            Owner,
            null);
        await HighSongPower.EnsureCardInHand(Owner.Player);
    }
}
