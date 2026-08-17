using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class NextRollBonusPower : PowerModel
{
    private CardModel _reservedCard;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;

    public override Color AmountLabelColor => new("F8D36A");

    internal int GetRollBonus(CardModel cardSource)
    {
        if (cardSource?.Owner?.Creature != Owner)
        {
            return 0;
        }

        if (_reservedCard == null)
        {
            AssertMutable();
            _reservedCard = cardSource;
            Flash();
        }

        return ReferenceEquals(_reservedCard, cardSource)
            ? Amount
            : 0;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay?.Card, _reservedCard))
        {
            return;
        }

        _reservedCard = null;
        await PowerCmd.Remove(this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedCard = null;
    }
}
