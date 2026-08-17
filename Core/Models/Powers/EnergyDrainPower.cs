using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class EnergyDrainPower : PowerModel
{
    private Dictionary<Creature, PendingShellDamage>
        _pendingShellDamage = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (dealer == Owner &&
            target != null &&
            target.Side != Owner.Side &&
            amount > 0m &&
            target.GetPower<HighDimensionalShellPower>() is
                { Amount: > 0 } shell)
        {
            _pendingShellDamage[target] = new PendingShellDamage(
                shell,
                shell.Amount);
        }

        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel cardSource)
    {
        if (dealer != Owner ||
            target == null ||
            !_pendingShellDamage.Remove(
                target,
                out PendingShellDamage pending))
        {
            return Task.CompletedTask;
        }

        int absorbed = System.Math.Max(
            0,
            pending.AmountBefore - pending.Shell.Amount);
        int restoreAmount = (int)(absorbed * 0.25m);
        if (restoreAmount > 0)
        {
            Owner.Player?
                .GetRelic<DeflectorProtection>()?
                .RestoreDp(restoreAmount);
            Flash();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingShellDamage =
            new Dictionary<Creature, PendingShellDamage>();
    }

    private readonly record struct PendingShellDamage(
        HighDimensionalShellPower Shell,
        int AmountBefore);
}
