using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class BreakPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("B8B8B8");

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.TickDownDuration(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        DestructionRatePower destructionRate = oldOwner.GetPower<DestructionRatePower>();
        if (destructionRate != null)
        {
            await PowerCmd.Remove(destructionRate);
        }

        if (!oldOwner.IsAlive || oldOwner.HasPower<HighDimensionalShellPower>())
        {
            return;
        }

        await PowerCmd.Apply<HighDimensionalShellPower>(
            new ThrowingPlayerChoiceContext(),
            oldOwner,
            HighDimensionalShellPower.CalculateAmount(oldOwner),
            oldOwner,
            null);
    }
}
