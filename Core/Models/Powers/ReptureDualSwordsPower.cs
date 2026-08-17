using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ReptureDualSwordsPower : PowerModel
{
    private const decimal _blockMultiplier = 1.5m;

    private bool _isPlayingCopy;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    internal static decimal GetProjectedDamageMultiplier(
        Creature owner)
    {
        return owner?
            .GetPower<DualSwordsEnhancementPower>()?
            .DamageMultiplier ?? 0.75m;
    }

    public override async Task AfterApplied(Creature applier, CardModel cardSource)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            await PowerCmd.Remove(Owner.GetPower<NormalStatePower>());
            await PowerCmd.Remove(Owner.GetPower<ReptureBowPower>());
            await PowerCmd.Remove(Owner.GetPower<RaptureScythePower>());
            MagicSwordModePower magicSwordMode =
                Owner.GetPower<MagicSwordModePower>();
            if (magicSwordMode != null)
            {
                await magicSwordMode.RemoveForModeTransition();
            }

            await PowerCmd.Remove(Owner.GetPower<CrimsonBladeFlowPower>());
        }

        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(1, player);
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

        CardModel copy = card.CreateDupe();
        copy.BaseReplayCount = 0;
        DualSwordsCopyHelper.RecordCopy(copy);

        if (card.EnergyCost.CostsX)
        {
            DualSwordsCopyHelper.RecordXEnergySpent(copy, cardPlay.Resources.EnergySpent);
        }

        _isPlayingCopy = true;
        try
        {
            Flash();
            await CardCmd.AutoPlay(choiceContext, copy, target, AutoPlayType.Default, skipXCapture: card.EnergyCost.CostsX);
            await CombatFlowHelper.FinishAutoPlayedCard(copy);
        }
        finally
        {
            DualSwordsCopyHelper.Clear(copy);
            _isPlayingCopy = false;
        }
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (target != Owner || block <= 0m)
        {
            return 1m;
        }

        return _blockMultiplier;
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (amount <= 0m ||
            dealer != Owner ||
            target?.Side == Owner.Side ||
            !props.IsPoweredAttack())
        {
            return 1m;
        }

        return GetProjectedDamageMultiplier(Owner);
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

    private Creature GetCopyTarget(CardPlay cardPlay)
    {
        return cardPlay.Card.TargetType switch
        {
            TargetType.AnyEnemy => GetEnemyCopyTarget(cardPlay.Target),
            TargetType.AnyAlly when cardPlay.Target?.IsAlive == true => cardPlay.Target,
            _ => null
        };
    }

    private Creature GetEnemyCopyTarget(Creature originalTarget)
    {
        if (originalTarget?.IsHittable == true && originalTarget.Side != Owner.Side)
        {
            return originalTarget;
        }

        Creature[] candidates = Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature => creature.IsHittable)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        int selectedIndex = Owner.CombatState.RunState.Rng.CombatTargets.NextInt(0, candidates.Length);
        return candidates[selectedIndex];
    }
}
