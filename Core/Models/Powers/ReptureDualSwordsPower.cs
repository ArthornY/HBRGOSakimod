using System;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ReptureDualSwordsPower : PowerModel
{
    private const decimal _dodgeMultiplier = 1.5m;

    private bool _isPlayingCopy;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
        await PowerCmd.Remove(Owner.GetPower<ReptureBowPower>());
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!cardPlay.IsFirstInSeries || (cardPlay.IsAutoPlay && !FeintMarkPower.IsDualSwordsEligibleStrike(cardPlay.Card)))
        {
            return;
        }

        CardModel card = cardPlay.Card;
        if (!ShouldCopy(card))
        {
            return;
        }

        Creature target = GetCopyTarget(cardPlay);
        if (RequiresOriginalTarget(card) && target == null)
        {
            return;
        }

        if (card.EnergyCost.CostsX && cardPlay.Resources.EnergySpent > 0)
        {
            Flash();
            await PlayerCmd.GainEnergy(cardPlay.Resources.EnergySpent, Owner.Player);
        }

        CardModel copy = card.CreateDupe();
        copy.BaseReplayCount = 0;

        _isPlayingCopy = true;
        try
        {
            Flash();
            await CardCmd.AutoPlay(choiceContext, copy, target, AutoPlayType.Default, skipXCapture: card.EnergyCost.CostsX);
            await CombatFlowHelper.FinishAutoPlayedCard(copy, checkWinCondition: false);
        }
        finally
        {
            _isPlayingCopy = false;
        }
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not DodgePower || target != Owner || amount <= 0m)
        {
            return false;
        }

        modifiedAmount = Math.Ceiling(amount * _dodgeMultiplier);
        return modifiedAmount != amount;
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (dealer != Owner || target == null || target.Side == Owner.Side || !props.IsPoweredAttack())
        {
            return 1m;
        }

        return 0.75m;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _isPlayingCopy = false;
    }

    private bool ShouldCopy(CardModel card)
    {
        return !_isPlayingCopy &&
               !CombatManager.Instance.IsOverOrEnding &&
               !card.IsDupe &&
               card.Owner?.Creature == Owner &&
               card.Type == CardType.Attack;
    }

    private static bool RequiresOriginalTarget(CardModel card)
    {
        return card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly;
    }

    private static Creature GetCopyTarget(CardPlay cardPlay)
    {
        return cardPlay.Card.TargetType switch
        {
            TargetType.AnyEnemy or TargetType.AnyAlly when cardPlay.Target?.IsAlive == true => cardPlay.Target,
            _ => null
        };
    }
}
