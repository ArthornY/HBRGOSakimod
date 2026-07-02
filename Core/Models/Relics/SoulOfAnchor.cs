using System;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class SoulOfAnchor : RelicModel
{
    private const int _rollMin = 1;
    private const int _rollMax = 100;
    private const int _rollExclusiveMax = _rollMax + 1;
    private const int _criticalSwordheartAmount = 7;
    private const int _luckySevenSoulEnergyCost = 1;

    private CardModel _triggeringCard;
    private int _currentRoll;
    private int _currentBonus;
    private decimal _currentValueMultiplier = 1m;
    private int _currentSwordheartAmount;
    private int _currentCardEnergyCost;
    private AttackCommand _activeAttackCommand;
    private int _currentAttackHitIndex = -1;
    private int _pendingSwordheartAmount;
    private int _attackOrSkillRollCount;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => false;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/soul_of_anchor.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    private CardModel TriggeringCard
    {
        get => _triggeringCard;
        set
        {
            AssertMutable();
            _triggeringCard = value;
        }
    }

    private int CurrentBonus
    {
        get => _currentBonus;
        set
        {
            AssertMutable();
            _currentBonus = value;
        }
    }

    private int CurrentRoll
    {
        get => _currentRoll;
        set
        {
            AssertMutable();
            _currentRoll = value;
        }
    }

    private decimal CurrentValueMultiplier
    {
        get => _currentValueMultiplier;
        set
        {
            AssertMutable();
            _currentValueMultiplier = value;
        }
    }

    private int CurrentSwordheartAmount
    {
        get => _currentSwordheartAmount;
        set
        {
            AssertMutable();
            _currentSwordheartAmount = value;
        }
    }

    private int CurrentCardEnergyCost
    {
        get => _currentCardEnergyCost;
        set
        {
            AssertMutable();
            _currentCardEnergyCost = value;
        }
    }

    private AttackCommand ActiveAttackCommand
    {
        get => _activeAttackCommand;
        set
        {
            AssertMutable();
            _activeAttackCommand = value;
        }
    }

    private int CurrentAttackHitIndex
    {
        get => _currentAttackHitIndex;
        set
        {
            AssertMutable();
            _currentAttackHitIndex = value;
        }
    }

    private int PendingSwordheartAmount
    {
        get => _pendingSwordheartAmount;
        set
        {
            AssertMutable();
            _pendingSwordheartAmount = value;
        }
    }

    private int AttackOrSkillRollCount
    {
        get => _attackOrSkillRollCount;
        set
        {
            AssertMutable();
            _attackOrSkillRollCount = value;
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        ResetCurrentRollState();

        if (cardPlay.Card.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        if (cardPlay.Card.Type is CardType.Attack or CardType.Skill)
        {
            TriggeringCard = cardPlay.Card;
            CurrentCardEnergyCost = GetCardEnergyCost(cardPlay);
        }
        else if (cardPlay.Card.Type == CardType.Power)
        {
            PreparePowerRoll(cardPlay.Card, includeMagicSwordBonus: true);
        }

        return Task.CompletedTask;
    }

    public override Task BeforeAttack(AttackCommand command)
    {
        if (TriggeringCard != null && command.ModelSource == TriggeringCard && command.Attacker == Owner.Creature)
        {
            ActiveAttackCommand = command;
            CurrentAttackHitIndex = -1;
        }

        return Task.CompletedTask;
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command == ActiveAttackCommand)
        {
            ActiveAttackCommand = null;
            CurrentAttackHitIndex = -1;
        }

        return Task.CompletedTask;
    }

    public void PrepareLuckySevenDamageRoll(CardModel cardSource)
    {
        TriggeringCard = cardSource;
        CurrentCardEnergyCost = _luckySevenSoulEnergyCost;
        PrepareAttackOrSkillRoll(cardSource, _luckySevenSoulEnergyCost, includeMagicSwordBonus: false);
    }

    public async Task AfterLuckySevenDamageRoll(PlayerChoiceContext choiceContext)
    {
        await ApplyPendingSwordheart(choiceContext);
        ResetCurrentRollState();
    }

    public override decimal ModifyDamageAdditive(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (!IsTriggeredCardValue(cardSource))
        {
            return 0m;
        }

        if (!props.IsPoweredAttack())
        {
            return 0m;
        }

        if (cardSource.Owner != Owner && dealer != Owner.Creature)
        {
            return 0m;
        }

        PrepareRollForDamage(cardSource);

        return CurrentBonus;
    }

    public override decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (!IsTriggeredCardValue(cardSource))
        {
            return 1m;
        }

        if (!props.IsPoweredAttack())
        {
            return 1m;
        }

        if (cardSource.Owner != Owner && dealer != Owner.Creature)
        {
            return 1m;
        }

        return CurrentValueMultiplier;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel cardSource, CardPlay cardPlay)
    {
        if (!IsTriggeredCardValue(cardSource))
        {
            return 0m;
        }

        if (!props.IsPoweredCardOrMonsterMoveBlock())
        {
            return 0m;
        }

        if (cardSource.Owner != Owner)
        {
            return 0m;
        }

        PrepareRollForCardValue(cardSource);

        return CurrentBonus;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel cardSource, CardPlay cardPlay)
    {
        if (!IsTriggeredCardValue(cardSource))
        {
            return 1m;
        }

        if (!props.IsPoweredCardOrMonsterMoveBlock())
        {
            return 1m;
        }

        if (cardSource.Owner != Owner)
        {
            return 1m;
        }

        return CurrentValueMultiplier;
    }

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature target, CardModel cardSource)
    {
        if (ShouldIgnorePowerAmountBonus(power, amount))
        {
            return 0m;
        }

        if (!IsTriggeredCardValue(cardSource))
        {
            return 0m;
        }

        if (giver != Owner.Creature)
        {
            return 0m;
        }

        PrepareRollForCardValue(cardSource);

        return CurrentBonus;
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(PowerModel power, Creature giver, decimal amount, Creature target, CardModel cardSource)
    {
        if (ShouldIgnorePowerAmountBonus(power, amount))
        {
            return 1m;
        }

        if (!IsTriggeredCardValue(cardSource))
        {
            return 1m;
        }

        if (giver != Owner.Creature)
        {
            return 1m;
        }

        return CurrentValueMultiplier;
    }

    private static bool ShouldIgnorePowerAmountBonus(PowerModel power, decimal amount)
    {
        return power is SwordheartPower || power.GetTypeForAmount(amount) == PowerType.Debuff;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != TriggeringCard)
        {
            return;
        }

        if (cardPlay.Card.Type is CardType.Attack or CardType.Skill && AttackOrSkillRollCount == 0)
        {
            PrepareAttackOrSkillRoll(cardPlay.Card, CurrentCardEnergyCost, includeMagicSwordBonus: true);
        }

        if (cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Power)
        {
            int energyRefund = GetPowerEnergyRefund(CurrentRoll, cardPlay.Resources.EnergySpent);
            if (energyRefund > 0)
            {
                Flash();
                await PlayerCmd.GainEnergy(energyRefund, Owner);
            }
        }

        await ApplyPendingSwordheart(choiceContext);

        ResetCurrentRollState();
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        ResetCurrentRollState();
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeringCard = null;
        _currentRoll = 0;
        _currentBonus = 0;
        _currentValueMultiplier = 1m;
        _currentSwordheartAmount = 0;
        _currentCardEnergyCost = 0;
        _activeAttackCommand = null;
        _currentAttackHitIndex = -1;
        _pendingSwordheartAmount = 0;
        _attackOrSkillRollCount = 0;
    }

    private int RollD100(bool includeMagicSwordBonus)
    {
        int roll = RollReptureAdjustedD100();
        GuideLittleHand guideLittleHand = Owner.GetRelic<GuideLittleHand>();
        if (guideLittleHand != null)
        {
            roll = guideLittleHand.TakeHigherRoll(roll, RollReptureAdjustedD100());
        }

        if (includeMagicSwordBonus && IsMagicSwordModeActive())
        {
            roll += MagicSwordModePower.RollBonus;
        }

        return roll;
    }

    private int RollReptureAdjustedD100()
    {
        int roll = RollRawD100();
        if (Owner.Creature.HasPower<ReptureDualSwordsPower>() && roll is 7 or 77)
        {
            return _rollMax;
        }

        return roll;
    }

    private int RollRawD100()
    {
        return Owner.RunState.Rng.Niche.NextInt(_rollMin, _rollExclusiveMax);
    }

    private void PrepareAttackOrSkillRoll(CardModel card, int energyCost, bool includeMagicSwordBonus)
    {
        if (card.Owner != Owner || card.Type is not (CardType.Attack or CardType.Skill))
        {
            return;
        }

        int roll = RollD100(includeMagicSwordBonus);
        TriggeringCard = card;
        CurrentRoll = roll;
        CurrentBonus = GetAttackOrSkillBonus(roll, energyCost);
        CurrentValueMultiplier = IsCriticalRoll(roll) ? 2m : 1m;
        CurrentSwordheartAmount = IsMagicSwordModeActive() ? 0 : GetSwordheartAmount(roll);
        PendingSwordheartAmount += CurrentSwordheartAmount;
        AttackOrSkillRollCount++;
        ActivateIfCriticalRoll(roll);
    }

    private void PreparePowerRoll(CardModel card, bool includeMagicSwordBonus)
    {
        if (card.Owner != Owner || card.Type != CardType.Power)
        {
            return;
        }

        int roll = RollD100(includeMagicSwordBonus);
        TriggeringCard = card;
        CurrentRoll = roll;
        CurrentSwordheartAmount = IsMagicSwordModeActive() ? 0 : GetSwordheartAmount(roll);
        PendingSwordheartAmount += CurrentSwordheartAmount;
        ActivateIfCriticalRoll(roll);
    }

    private async Task ApplyPendingSwordheart(PlayerChoiceContext choiceContext)
    {
        if (PendingSwordheartAmount <= 0)
        {
            return;
        }

        int swordheartAmount = GetSwordheartAmountToApply(PendingSwordheartAmount);
        if (swordheartAmount <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<SwordheartPower>(choiceContext, Owner.Creature, swordheartAmount, Owner.Creature, TriggeringCard);
    }

    private static int GetCardEnergyCost(CardPlay cardPlay)
    {
        int energyValue = int.Max(cardPlay.Resources.EnergyValue, cardPlay.Card.EnergyCost.GetResolved());
        return int.Max(0, energyValue);
    }

    private static int GetAttackOrSkillBonus(int roll, int energyCost)
    {
        if (roll >= 50 && roll <= 75)
        {
            return energyCost;
        }

        if (roll >= 76 && roll <= 90)
        {
            return energyCost * 2;
        }

        if (IsThirdTierRoll(roll))
        {
            return energyCost * 3;
        }

        return 0;
    }

    private static int GetPowerEnergyRefund(int roll, int energySpent)
    {
        if (roll >= 50 && roll <= 75)
        {
            return 1;
        }

        if (roll >= 76 && roll <= 90)
        {
            return 2;
        }

        if (IsThirdTierRoll(roll))
        {
            return 3;
        }

        if (IsCriticalRoll(roll))
        {
            return energySpent + 2;
        }

        return 0;
    }

    private static int GetSwordheartAmount(int roll)
    {
        int amount = IsCriticalRoll(roll) ? _criticalSwordheartAmount : 0;
        amount += CountSevens(roll);
        return Math.Min(SwordheartPower.BaseMaxAmount, amount);
    }

    private int GetSwordheartAmountToApply(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        SwordheartPower swordheart = Owner.Creature.GetPower<SwordheartPower>();
        int currentAmount = swordheart?.Amount ?? 0;
        int remainingAmount = Math.Max(0, SwordheartPower.GetMaxAmount(Owner.Creature) - currentAmount);
        return Math.Min(amount, remainingAmount);
    }

    private static int CountSevens(int value)
    {
        int count = 0;

        while (value > 0)
        {
            if (value % 10 == 7)
            {
                count++;
            }

            value /= 10;
        }

        return count;
    }

    private void ActivateIfCriticalRoll(int roll)
    {
        if (!IsCriticalRoll(roll))
        {
            return;
        }

        Flash();
    }

    private static bool IsCriticalRoll(int roll)
    {
        return roll == _rollMax;
    }

    private static bool IsThirdTierRoll(int roll)
    {
        return roll >= 91 && roll <= 99 || roll > _rollMax;
    }

    private bool IsMagicSwordModeActive()
    {
        return Owner.Creature.HasPower<MagicSwordModePower>();
    }

    private void PrepareRollForDamage(CardModel cardSource)
    {
        if (!IsTriggeredCardValue(cardSource))
        {
            return;
        }

        if (ActiveAttackCommand != null && ActiveAttackCommand.ModelSource == cardSource)
        {
            int hitIndex = ActiveAttackCommand.Results.Count();
            if (CurrentAttackHitIndex == hitIndex)
            {
                return;
            }

            CurrentAttackHitIndex = hitIndex;
        }
        else
        {
            CurrentAttackHitIndex = -1;
        }

        PrepareAttackOrSkillRoll(cardSource, CurrentCardEnergyCost, includeMagicSwordBonus: true);
    }

    private void PrepareRollForCardValue(CardModel cardSource)
    {
        if (!IsTriggeredCardValue(cardSource))
        {
            return;
        }

        CurrentAttackHitIndex = -1;
        PrepareAttackOrSkillRoll(cardSource, CurrentCardEnergyCost, includeMagicSwordBonus: true);
    }

    private bool IsTriggeredCardValue(CardModel cardSource)
    {
        if (TriggeringCard == null || cardSource != TriggeringCard)
        {
            return false;
        }

        return TriggeringCard.Type is CardType.Attack or CardType.Skill;
    }

    private void ResetCurrentRollState()
    {
        TriggeringCard = null;
        CurrentRoll = 0;
        CurrentBonus = 0;
        CurrentValueMultiplier = 1m;
        CurrentSwordheartAmount = 0;
        CurrentCardEnergyCost = 0;
        ActiveAttackCommand = null;
        CurrentAttackHitIndex = -1;
        PendingSwordheartAmount = 0;
        AttackOrSkillRollCount = 0;
    }
}
