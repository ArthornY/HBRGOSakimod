using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Relics;
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

    public override MegaCrit.Sts2.Core.Localization.LocString Description =>
        HbrgoSakiEnergyIconHelper.Add31OPrefix(base.Description);

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
        }

        await ModeTransitionHelper.RemoveModeSelectionCardsFromCombat(Owner);
        SwiftPower swift = Owner.GetPower<SwiftPower>();
        if (swift != null)
        {
            await swift.RecordModeSwitch();
        }

    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player ||
            ModeTransitionHelper.IsWeaponModeWeakened(Owner))
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

        _isPlayingCopy = true;
        try
        {
            const int copyCount = 1;
            for (int copyIndex = 0;
                 copyIndex < copyCount &&
                 !CombatManager.Instance.IsOverOrEnding;
                 copyIndex++)
            {
                Creature target = GetCopyTarget(cardPlay);
                if (RequiresOriginalTarget(card) && target == null)
                {
                    break;
                }

                CardModel copy = card.CreateDupeCompat(card.Owner);
                copy.BaseReplayCount = 0;
                try
                {
                    DualSwordsCopyHelper.RecordPowerCopy(copy);
                    if (card.EnergyCost.CostsX)
                    {
                        DualSwordsCopyHelper.RecordXEnergySpent(
                            copy,
                            cardPlay.Resources.EnergySpent);
                    }

                    Flash();
                    if (card is SwitchingSlashHbrgoSaki)
                    {
                        await PowerCmd.Apply<ReptureBowPower>(
                            choiceContext,
                            Owner,
                            1m,
                            Owner,
                            card);
                    }

                    await CardCmd.AutoPlay(
                        choiceContext,
                        copy,
                        target,
                        AutoPlayType.Default,
                        skipXCapture: card.EnergyCost.CostsX);
                    await CombatFlowHelper.FinishAutoPlayedCard(copy);
                }
                finally
                {
                    DualSwordsCopyHelper.Clear(copy);
                }
            }
        }
        finally
        {
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

        if (HasVirtualCrimsonBladeFlow())
        {
            return 2m;
        }

        if (ModeTransitionHelper.IsWeaponModeWeakened(Owner))
        {
            return 1m;
        }

        return _blockMultiplier;
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (amount > 0m &&
            HasVirtualCrimsonBladeFlow() &&
            (dealer == Owner || target == Owner))
        {
            HbrgoSakiDamageMultiplierHelper.Add(2m, this);
        }

        if (amount <= 0m ||
            dealer != Owner ||
            target?.Side == Owner.Side ||
            !props.IsPoweredAttack())
        {
            return 1m;
        }

        return GetProjectedDamageMultiplier(Owner);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!HasVirtualCrimsonBladeFlow() ||
            side != Owner.Side ||
            !participants.Contains(Owner))
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!HasVirtualCrimsonBladeFlow() ||
            side != Owner.Side ||
            !participants.Contains(Owner) ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(Owner))
        {
            return;
        }

        DeflectorProtection protection =
            Owner.Player?.GetRelic<DeflectorProtection>();
        int lostDp = protection?.LoseDpByCurrentPercent(0.1m) ?? 0;
        if (lostDp > 0)
        {
            Flash();
        }

        if (protection != null)
        {
            await protection.ResolveMentalStrengthDpLossRewards(
                choiceContext);
        }
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

    private bool HasVirtualCrimsonBladeFlow()
    {
        return !Owner.HasPower<CrimsonBladeFlowPower>() &&
               HbrgoSakiHextechCompatLoader
                   .HasDoubleSwordsDoubleRune(Owner);
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
