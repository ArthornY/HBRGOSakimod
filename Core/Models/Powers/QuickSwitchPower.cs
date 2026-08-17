using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class QuickSwitchPower : PowerModel
{
    private CardModel _reservedCard;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    internal bool TryReserve(CardModel card)
    {
        if (card?.Owner?.Creature != Owner ||
            Amount <= 0 ||
            _reservedCard != null)
        {
            return false;
        }

        AssertMutable();
        _reservedCard = card;
        Flash();
        return true;
    }

    internal async Task FinishReservation(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool consume)
    {
        if (!ReferenceEquals(_reservedCard, card))
        {
            return;
        }

        AssertMutable();
        _reservedCard = null;
        if (!consume)
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -1m,
            Owner,
            card);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedCard = null;
    }
}
