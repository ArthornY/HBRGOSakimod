using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MaidCultivationPower : PowerModel
{
    public const int RollBonus = 20;

    private const decimal _damageMultiplier = 1.5m;
    private const decimal _baseOverclockMultiplier = 1.5m;
    private const decimal _upgradedOverclockMultiplier = 2m;

    private HashSet<CardModel> _enhancedCards = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasEnhancedOverclock { get; private set; }

    public override LocString Description => HasEnhancedOverclock
        ? new LocString("powers", "MAID_CULTIVATION_POWER.upgradedDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("F7A9D5");

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (cardSource is MimicMaidCultivationHbrgoSaki { HasEnhancedOverclock: true })
        {
            AssertMutable();
            HasEnhancedOverclock = true;
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay?.Card;
        if (!IsEligibleAttack(card) ||
            _enhancedCards.Contains(card) ||
            Amount - _enhancedCards.Count <= 0)
        {
            return Task.CompletedTask;
        }

        _enhancedCards.Add(card);
        Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CardModel card = cardPlay?.Card;
        if (card == null || !_enhancedCards.Remove(card))
        {
            return;
        }

        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -1m,
            null,
            card);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        decimal multiplier = dealer == Owner &&
            props.IsPoweredAttack() &&
            IsEnhancing(cardSource)
                ? _damageMultiplier
                : 1m;
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }

    internal int GetRollBonus(CardModel cardSource)
    {
        return IsEnhancing(cardSource) ? RollBonus : 0;
    }

    internal decimal GetOverclockMultiplier(CardModel cardSource)
    {
        if (!IsEnhancing(cardSource))
        {
            return 1m;
        }

        return HasEnhancedOverclock
            ? _upgradedOverclockMultiplier
            : _baseOverclockMultiplier;
    }

    internal static decimal GetOverclockMultiplier(
        Creature dealer,
        CardModel cardSource)
    {
        return dealer?.GetPower<MaidCultivationPower>()
            ?.GetOverclockMultiplier(cardSource) ?? 1m;
    }

    private bool IsEnhancing(CardModel card)
    {
        if (!IsEligibleAttack(card))
        {
            return false;
        }

        return _enhancedCards.Contains(card) ||
            Amount - _enhancedCards.Count > 0;
    }

    private bool IsEligibleAttack(CardModel card)
    {
        return card?.Owner?.Creature == Owner &&
            card.Type == CardType.Attack;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _enhancedCards = new HashSet<CardModel>();
    }
}
