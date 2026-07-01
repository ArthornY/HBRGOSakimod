using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DisabledPower : PowerModel
{
    public const int MaxAmount = 2;

    private const decimal RecoveryPenaltyPerStack = 0.5m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("B8B8B8");

    public static bool IsAtMax(Creature creature)
    {
        return creature.GetPower<DisabledPower>()?.Amount >= MaxAmount;
    }

    public static bool Clear(Creature creature)
    {
        DisabledPower disabled = creature.GetPower<DisabledPower>();
        if (disabled == null || disabled.Amount <= 0)
        {
            return false;
        }

        disabled.Flash();
        disabled.RemoveInternal();
        return true;
    }

    public static decimal GetDpRecoveryMultiplier(Creature creature)
    {
        int amount = Math.Min(MaxAmount, Math.Max(0, creature.GetPower<DisabledPower>()?.Amount ?? 0));
        return Math.Max(0m, 1m - amount * RecoveryPenaltyPerStack);
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;

        if (target != Owner || canonicalPower is not DisabledPower || amount <= 0m)
        {
            return false;
        }

        int remaining = MaxAmount - Amount;
        decimal cappedAmount = Math.Min(amount, Math.Max(0, remaining));
        if (cappedAmount == amount)
        {
            return false;
        }

        modifiedAmount = cappedAmount;
        return true;
    }
}
