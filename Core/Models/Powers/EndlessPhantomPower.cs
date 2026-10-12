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

public sealed class EndlessPhantomPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F2C15F");

    internal async Task AfterPhantomNonAttack(
        Creature phantom)
    {
        int amount = Math.Max(0, Amount);
        if (phantom == null || !phantom.IsAlive || amount <= 0)
        {
            return;
        }

        Flash();
        PlayerChoiceContext context =
            new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<PhantomNextAttackBonusSegmentsPower>(
            context,
            phantom,
            amount,
            Owner,
            null);
        await PowerCmd.Apply<StrengthPower>(
            context,
            phantom,
            amount,
            Owner,
            null);
    }
}
