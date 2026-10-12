using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PhaseWarpPower : PowerModel
{
    private const int DeathSpearDelayActions = 3;
    private const decimal NonRollDamageThreshold = 40m;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CompletedOwnerActions { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool SkipNextOwnerTurnEnd { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredDeathSpear { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("D72545");

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        // Phase Warp is granted during a FlatHand move. Do not count that
        // same move as one of the three actions that follow its activation.
        SkipNextOwnerTurnEnd = true;
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (HasTriggeredDeathSpear ||
            side != Owner.Side ||
            !participants.Contains(Owner) ||
            !Owner.IsAlive)
        {
            return Task.CompletedTask;
        }

        if (SkipNextOwnerTurnEnd)
        {
            SkipNextOwnerTurnEnd = false;
            return Task.CompletedTask;
        }

        CompletedOwnerActions++;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (HasTriggeredDeathSpear ||
            side != Owner.Side ||
            !participants.Contains(Owner) ||
            !Owner.IsAlive)
        {
            return Task.CompletedTask;
        }

        if (CompletedOwnerActions < DeathSpearDelayActions ||
            Owner.Monster is not FlatHand flatHand)
        {
            return Task.CompletedTask;
        }

        HasTriggeredDeathSpear = true;
        Flash();
        flatHand.ForceDeathSpearMove();
        return Task.CompletedTask;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target != Owner || amount <= 0m)
        {
            return 1m;
        }

        bool dealerCanRoll =
            RollKeywordRules.IsRollEnabled(dealer?.Player);
        bool shouldBlock;
        if (!dealerCanRoll)
        {
            shouldBlock = amount < NonRollDamageThreshold;
        }
        else
        {
            shouldBlock = !RollKeywordRules.IsCriticalRoll(
                dealer.Player,
                cardSource?
                    .Capability<RollGlossaryCapability>()?
                    .GetCurrentRollDetailsFor(cardSource));
        }

        if (!shouldBlock)
        {
            return 1m;
        }

        Flash();
        return 0m;
    }
}
