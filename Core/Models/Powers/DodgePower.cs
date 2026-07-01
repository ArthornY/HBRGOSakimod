using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class DodgePower : PowerModel
{
    private int _pendingSpend;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (!CanSpendForDamage(target, amount, props))
        {
            return amount;
        }

        _pendingSpend = GetSpendAmount(amount);
        return 0m;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        if (_pendingSpend <= 0)
        {
            return Task.CompletedTask;
        }

        Spend(_pendingSpend);
        _pendingSpend = 0;
        return Task.CompletedTask;
    }

    public static bool TrySpend(Creature creature, decimal amount)
    {
        if (amount <= 0m)
        {
            return false;
        }

        DodgePower dodge = creature.GetPower<DodgePower>();
        int spendAmount = GetSpendAmount(amount);
        if (dodge == null || dodge.Amount < spendAmount)
        {
            return false;
        }

        dodge.Spend(spendAmount);
        return true;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingSpend = 0;
    }

    private bool CanSpendForDamage(Creature target, decimal amount, ValueProp props)
    {
        if (target != Owner || amount <= 0m || props.HasFlag(ValueProp.Unblockable))
        {
            return false;
        }

        return Amount >= GetSpendAmount(amount);
    }

    private static int GetSpendAmount(decimal amount)
    {
        return Math.Max(1, (int)Math.Ceiling(amount));
    }

    private void Spend(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetAmount(Amount - Math.Min(Amount, amount));
        Flash();

        if (ShouldRemoveDueToAmount())
        {
            RemoveInternal();
        }
    }
}
