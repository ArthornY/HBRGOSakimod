using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MidsummerHellCursePower : PowerModel
{
    private const int _cardsPerTrigger = 3;
    private const decimal _maxHpDamagePercent = 0.1m;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public CardType LastCardType { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ConsecutiveCardCount { get; private set; }

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F85C5C");

    public override async Task AfterCardPlayedLate(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature cardOwner = cardPlay.Card.Owner?.Creature;
        if (cardPlay.Card is YoHbrgoSaki ||
            cardOwner == null ||
            cardOwner.Side == Owner.Side ||
            cardPlay.Card.Type == CardType.None ||
            Owner.IsDead ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int nextCount = cardPlay.Card.Type == LastCardType
            ? ConsecutiveCardCount + 1
            : 1;
        SetSequence(cardPlay.Card.Type, nextCount);
        if (nextCount < _cardsPerTrigger)
        {
            return;
        }

        SetSequence(cardPlay.Card.Type, 0);
        decimal damage = Math.Max(
            1m,
            Owner.MaxHp * _maxHpDamagePercent * Amount);
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            cardOwner,
            cardPlay.Card);
    }

    private void SetSequence(CardType cardType, int count)
    {
        AssertMutable();
        LastCardType = cardType;
        ConsecutiveCardCount = Math.Max(0, count);
    }
}
