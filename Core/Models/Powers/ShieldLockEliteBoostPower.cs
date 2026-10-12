using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ShieldLockPower : PowerModel
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("A9D7F5");

    public override bool ShouldPowerBeRemovedAfterOwnerDeath()
    {
        return false;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || canonicalPower is not ShieldLockPower)
        {
            return false;
        }

        if (amount < 0m && applier == Owner)
        {
            modifiedAmount = -Math.Min(-amount, Amount);
            return modifiedAmount != amount;
        }

        modifiedAmount = 0m;
        return amount != 0m;
    }

    internal async Task LoseLayers(int amount)
    {
        if (amount <= 0 || Amount <= 0)
        {
            return;
        }

        Creature oldOwner = Owner;
        int removed = Math.Min(amount, Amount);
        await PowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            this,
            -removed,
            oldOwner,
            null);
        if (oldOwner.IsAlive)
        {
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                oldOwner,
                removed,
                oldOwner,
                null);
        }

        if (oldOwner.IsAlive &&
            oldOwner.GetPowerAmount<ShieldLockPower>() <= 0 &&
            !oldOwner.HasPower<PhaseWarpPower>())
        {
            await PowerCmd.Apply<PhaseWarpPower>(
                new ThrowingPlayerChoiceContext(),
                oldOwner,
                1m,
                oldOwner,
                null);
        }
    }
}
