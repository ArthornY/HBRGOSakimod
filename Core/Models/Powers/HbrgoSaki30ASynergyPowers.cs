using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ProofThroughPainPower : PowerModel
{
    private int _triggersThisTurn;
    private int _pendingTriggers;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");

    internal void QueuePhantomDamage()
    {
        if (_triggersThisTurn >= Amount || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        AssertMutable();
        _triggersThisTurn++;
        _pendingTriggers++;
    }

    // DP is resolved synchronously as block. Defer its rewards to this awaited
    // hook, which also runs for lethal HP damage and redirected damage.
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext,
        Creature dealer, DamageResult result, ValueProp props, Creature target, CardModel cardSource)
    {
        if (Owner.Player == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (PhantomHelper.IsOwnedMainPhantom(target, Owner.Player) && result.UnblockedDamage > 0)
        {
            QueuePhantomDamage();
        }

        while (_pendingTriggers > 0 && !CombatManager.Instance.IsOverOrEnding)
        {
            AssertMutable();
            _pendingTriggers--;
            Flash();
            await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
            await PowerCmd.Apply<ChargePower>(choiceContext, Owner, 2m, Owner, null);
        }
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            AssertMutable();
            _triggersThisTurn = 0;
            _pendingTriggers = 0;
        }
        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggersThisTurn = 0;
        _pendingTriggers = 0;
    }
}

public sealed class MemoryRefluxPower : PowerModel
{
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");

    internal async Task AfterRosterConsumed(PlayerChoiceContext choiceContext)
    {
        if (_triggeredThisTurn || Amount <= 0 || Owner?.Player == null ||
            !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        AssertMutable();
        _triggeredThisTurn = true;
        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
        await PowerCmd.Apply<ChargePower>(choiceContext, Owner, 2m, Owner, null);
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            AssertMutable();
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeredThisTurn = false;
    }
}
