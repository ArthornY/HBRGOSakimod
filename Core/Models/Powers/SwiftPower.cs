using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SwiftPower : PowerModel
{
    private CardModel _reservedCard;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CompletedSwitches { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FreeSwitches { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("6ED7FF");

    internal bool CanPreventNextPenalty => FreeSwitches > 0;

    internal bool TryReserve(CardModel card)
    {
        if (!CanPreventNextPenalty ||
            card?.Owner?.Creature != Owner ||
            _reservedCard != null)
        {
            return false;
        }

        AssertMutable();
        _reservedCard = card;
        return true;
    }

    internal Task FinishTransition(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool modeChanged)
    {
        bool wasReserved = ReferenceEquals(
            _reservedCard,
            card);
        if (wasReserved)
        {
            AssertMutable();
            _reservedCard = null;
        }

        if (!modeChanged)
        {
            return Task.CompletedTask;
        }

        AssertMutable();
        if (wasReserved && FreeSwitches > 0)
        {
            FreeSwitches--;
        }

        CompletedSwitches++;
        if (CompletedSwitches >= 2)
        {
            FreeSwitches += CompletedSwitches / 2;
            CompletedSwitches %= 2;
            Flash();
        }

        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedCard = null;
    }
}
