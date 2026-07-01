using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SwordheartPower : PowerModel
{
    public const int MaxAmount = 7;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;

        if (target != Owner || canonicalPower is not SwordheartPower || amount <= 0m)
        {
            return false;
        }

        if (target.HasPower<MagicSwordModePower>())
        {
            modifiedAmount = 0m;
            return true;
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

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature applier, CardModel cardSource)
    {
        if (power is not SwordheartPower || power.Owner != Owner || power.Amount < MaxAmount || Owner.HasPower<MagicSwordModePower>())
        {
            return;
        }

        MagicSwordModePower magicSwordMode = await PowerCmd.Apply<MagicSwordModePower>(choiceContext, Owner, 1m, applier, null);
        if (magicSwordMode != null)
        {
            await magicSwordMode.AddFeintCardToCombat();
        }
    }
}
