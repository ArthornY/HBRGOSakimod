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
                { CurrentDp: > 0m } shell)
        {
            _pendingShellDamage[target] = new PendingShellDamage(
                shell,
                shell.CurrentDp);
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

        decimal absorbed = System.Math.Max(0m,
            pending.AmountBefore - pending.Shell.CurrentDp);
        decimal restoreAmount = decimal.Floor(absorbed * 0.25m);
        if (restoreAmount > 0m)
        {
            if (Owner.Player?.GetRelic<DeflectorProtection>() is { } protection)
            {
                // Player DP remains native-sized; bound the accepted recovery
                // before narrowing so a huge monster pool cannot overflow it.
                int capacity = System.Math.Max(0, protection.MaxDp - protection.Dp);
                protection.RestoreDp((int)System.Math.Min(restoreAmount, capacity));
            }
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
        decimal AmountBefore);
}
