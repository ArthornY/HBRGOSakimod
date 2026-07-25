using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class LostMelodyPower : PowerModel
{
    private HashSet<CardModel> _cardsPaidWithLostMelody = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("FFD45C");

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        bool replacesEnergy =
            Amount > 0 &&
            card?.Owner?.Creature == Owner &&
            !card.EnergyCost.CostsX &&
            originalCost > 1m;

        if (replacesEnergy)
        {
            _cardsPaidWithLostMelody.Add(card);
            modifiedCost = 0m;
            return true;
        }

        if (card != null)
        {
            _cardsPaidWithLostMelody.Remove(card);
        }

        modifiedCost = originalCost;
        return false;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay?.Card == null)
        {
            return;
        }

        bool replacedEnergy = _cardsPaidWithLostMelody.Remove(cardPlay.Card);
        if (Amount <= 0 ||
            cardPlay.Card.Owner?.Creature != Owner ||
            cardPlay.IsAutoPlay ||
            !cardPlay.IsFirstInSeries ||
            !replacedEnergy)
        {
            return;
        }

        Flash();
        await PowerCmd.ModifyAmount(
            new ThrowingPlayerChoiceContext(),
            this,
            -1m,
            Owner,
            cardPlay.Card);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay?.Card != null)
        {
            _cardsPaidWithLostMelody.Remove(cardPlay.Card);
        }

        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _cardsPaidWithLostMelody = new HashSet<CardModel>();
    }
}
