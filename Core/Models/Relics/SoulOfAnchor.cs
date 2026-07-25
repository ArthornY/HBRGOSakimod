using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Relics;

public class SoulOfAnchor : RelicModel
{
    private const int _rollMin = 1;
    private const int _rollMax = 100;
    private const int _rollExclusiveMax = _rollMax + 1;
    private const int _criticalSwordheartAmount = 7;
    private const int _luckySevenSoulEnergyCost = 1;
    private const int _fullSwordheartRound = 7;

    private CardModel _triggeringCard;
    private int _currentRoll;
    private int _currentBonus;
    private decimal _currentValueMultiplier = 1m;
    private int _currentSwordheartAmount;
    private int _currentCardEnergyCost;
    private AttackCommand _activeAttackCommand;
    private int _currentAttackHitIndex = -1;
    private int _pendingSwordheartAmount;
    private int _pendingFragileTriggers;
    private int _attackOrSkillRollCount;
    private bool _forceRollsToOneHundred;
    private bool _hasCheckedFullSwordheartRound;
    private int _criticalSuccessCountThisCombat;
    private List<SoulRollHistoryEntry> _rollHistory = new();
    private Stack<SuspendedRollState> _suspendedRollStates = new();

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => false;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/soul_of_anchor.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    public IReadOnlyList<SoulRollHistoryEntry> RollHistory => _rollHistory;

    protected virtual int FirstTierUpperBound => 75;

    protected virtual int SecondTierLowerBound => 76;

    protected virtual int SecondTierUpperBound => 90;

    protected virtual int ThirdTierLowerBound => 91;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CriticalSuccessCountThisCombat
    {
        get => _criticalSuccessCountThisCombat;
        private set
        {
            AssertMutable();
            _criticalSuccessCountThisCombat = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool RollsAreFixedToOneHundred
    {
        get => _forceRollsToOneHundred;
        private set
        {
            AssertMutable();
            _forceRollsToOneHundred = value;
        }
    }

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
        if (cardPlay.Card.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        BeginCardRollState(cardPlay.Card);
        if (cardPlay.Card.Type is CardType.Attack or CardType.Skill)
        {
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
        BeginCardRollState(cardSource);
        CurrentCardEnergyCost = _luckySevenSoulEnergyCost;
        PrepareAttackOrSkillRoll(cardSource, _luckySevenSoulEnergyCost, includeMagicSwordBonus: false);
    }

    public async Task AfterLuckySevenDamageRoll(PlayerChoiceContext choiceContext)
    {
        await ApplyPendingSwordheart(choiceContext);
        await PlayPendingFragileCards(choiceContext);
        FinishCardRollState();
    }

    public async Task<int> RollExtraD100(PlayerChoiceContext choiceContext, CardModel cardSource)
    {
        SoulRollResult rollResult = RollD100(includeMagicSwordBonus: true);
        int roll = rollResult.Roll;
        RecordRoll(rollResult, $"「还没有结束」额外掷骰，由「{GetCardTitle(cardSource)}」触发");
        QueueFragileTrigger(roll);
        int effectMultiplier = GetEffectMultiplier(roll);
        int swordheartAmount = IsMagicSwordModeActive()
            ? 0
            : GetSwordheartAmount(roll) * effectMultiplier;
        int amountToApply = GetSwordheartAmountToApply(swordheartAmount);

        Flash();
        ActivateIfCriticalRoll(roll);
        if (amountToApply > 0)
        {
            await PowerCmd.Apply<SwordheartPower>(
                choiceContext,
                Owner.Creature,
                amountToApply,
                Owner.Creature,
                cardSource);
        }

        await PlayPendingFragileCards(choiceContext);
        return roll;
    }

    public void SetRollsFixedToOneHundred(bool enabled)
    {
        RollsAreFixedToOneHundred = enabled;
    }

    public override Task BeforeCombatStart()
    {
        ClearCardRollStates();
        _hasCheckedFullSwordheartRound = false;
        CriticalSuccessCountThisCombat = 0;
        _rollHistory.Clear();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (
            _hasCheckedFullSwordheartRound ||
            combatState.RoundNumber != _fullSwordheartRound ||
            side != Owner.Creature.Side ||
            !participants.Contains(Owner.Creature))
        {
            return;
        }

        _hasCheckedFullSwordheartRound = true;
        if (IsMagicSwordModeActive())
        {
            return;
        }

        int amountToApply = GetSwordheartAmountToApply(
            SwordheartPower.GetMaxAmount(Owner.Creature));
        if (amountToApply <= 0)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<SwordheartPower>(
            choiceContext,
            Owner.Creature,
            amountToApply,
            Owner.Creature,
            null);
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

        try
        {
            if (cardPlay.Card.Type is CardType.Attack or CardType.Skill && AttackOrSkillRollCount == 0)
            {
                PrepareAttackOrSkillRoll(cardPlay.Card, CurrentCardEnergyCost, includeMagicSwordBonus: true);
            }

            if (cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Power)
            {
                int energyRefund = GetPowerEnergyRefund(CurrentRoll, cardPlay.Resources.EnergySpent)
                    * GetEffectMultiplier(CurrentRoll);
                if (energyRefund > 0)
                {
                    Flash();
                    await PlayerCmd.GainEnergy(energyRefund, Owner);
                }
            }

            await ApplyPendingSwordheart(choiceContext);
            await PlayPendingFragileCards(choiceContext);
        }
        finally
        {
            FinishCardRollState();
        }
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        ClearCardRollStates();
        _hasCheckedFullSwordheartRound = false;
        CriticalSuccessCountThisCombat = 0;
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
        _pendingFragileTriggers = 0;
        _attackOrSkillRollCount = 0;
        _hasCheckedFullSwordheartRound = false;
        _criticalSuccessCountThisCombat = 0;
        _rollHistory = new List<SoulRollHistoryEntry>();
        _suspendedRollStates = new Stack<SuspendedRollState>();
    }

    private SoulRollResult RollD100(bool includeMagicSwordBonus)
    {
        if (RollsAreFixedToOneHundred)
        {
            return new SoulRollResult(_rollMax, null);
        }

        int roll = RollReptureAdjustedD100();
        int? discardedRoll = null;
        GuideLittleHand guideLittleHand = Owner.GetRelic<GuideLittleHand>();
        if (guideLittleHand != null)
        {
            int secondRoll = RollReptureAdjustedD100();
            int higherRoll = guideLittleHand.TakeHigherRoll(roll, secondRoll);
            discardedRoll = higherRoll == roll ? secondRoll : roll;
            roll = higherRoll;
        }

        int bonus = 0;
        if (includeMagicSwordBonus && IsMagicSwordModeActive())
        {
            bonus += MagicSwordModePower.RollBonus;
        }

        if (Owner.Creature.HasPower<NotOverYetPower>())
        {
            bonus += NotOverYetPower.RollBonus;
        }

        return new SoulRollResult(
            roll + bonus,
            discardedRoll.HasValue ? discardedRoll.Value + bonus : null);
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

        SoulRollResult rollResult = RollD100(includeMagicSwordBonus);
        int roll = rollResult.Roll;
        RecordRoll(rollResult, $"打出「{GetCardTitle(card)}」");
        QueueFragileTrigger(roll);
        int effectMultiplier = GetEffectMultiplier(roll);
        TriggeringCard = card;
        CurrentRoll = roll;
        CurrentBonus = GetAttackOrSkillBonus(roll, energyCost) * effectMultiplier;
        CurrentValueMultiplier = IsCriticalRoll(roll) ? 2m * effectMultiplier : 1m;
        CurrentSwordheartAmount = IsMagicSwordModeActive() ? 0 : GetSwordheartAmount(roll) * effectMultiplier;
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

        SoulRollResult rollResult = RollD100(includeMagicSwordBonus);
        int roll = rollResult.Roll;
        RecordRoll(rollResult, $"打出「{GetCardTitle(card)}」");
        QueueFragileTrigger(roll);
        int effectMultiplier = GetEffectMultiplier(roll);
        TriggeringCard = card;
        CurrentRoll = roll;
        CurrentSwordheartAmount = IsMagicSwordModeActive() ? 0 : GetSwordheartAmount(roll) * effectMultiplier;
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

    private void QueueFragileTrigger(int roll)
    {
        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }
    }

    private async Task PlayPendingFragileCards(PlayerChoiceContext choiceContext)
    {
        while (_pendingFragileTriggers > 0)
        {
            _pendingFragileTriggers--;
            if (Owner.Creature.CombatState == null)
            {
                return;
            }

            FragileHbrgoSaki card = Owner.PlayerCombatState?.AllCards
                .OfType<FragileHbrgoSaki>()
                .FirstOrDefault(candidate =>
                    !candidate.HasBeenRemovedFromState &&
                    candidate.Pile?.IsCombatPile == true &&
                    candidate.Pile.Type is not (PileType.Play or PileType.Exhaust));
            Creature target = Owner.Creature.CombatState
                .GetOpponentsOf(Owner.Creature)
                .Where(creature => creature.IsHittable)
                .OrderByDescending(creature => creature.CurrentHp)
                .ThenBy(creature => creature.CombatId)
                .FirstOrDefault();
            if (card == null || target == null)
            {
                return;
            }

            Flash();
            bool restoreUnplayable = card.Keywords.Contains(CardKeyword.Unplayable);
            if (restoreUnplayable)
            {
                card.RemoveKeyword(CardKeyword.Unplayable);
            }

            try
            {
                await CardCmd.AutoPlay(
                    choiceContext,
                    card,
                    target,
                    AutoPlayType.Default,
                    skipCardPileVisuals: true);
                await CombatFlowHelper.FinishAutoPlayedCard(card);
            }
            finally
            {
                if (restoreUnplayable)
                {
                    card.AddKeyword(CardKeyword.Unplayable);
                }
            }
        }
    }

    private static int GetCardEnergyCost(CardPlay cardPlay)
    {
        int energyValue = int.Max(cardPlay.Resources.EnergyValue, cardPlay.Card.EnergyCost.GetResolved());
        return int.Max(0, energyValue);
    }

    private int GetAttackOrSkillBonus(int roll, int energyCost)
    {
        if (GetRollTier(roll) == SoulRollTier.First)
        {
            return energyCost;
        }

        if (GetRollTier(roll) == SoulRollTier.Second)
        {
            return energyCost * 2;
        }

        if (GetRollTier(roll) == SoulRollTier.Third)
        {
            return energyCost * 3;
        }

        return 0;
    }

    private int GetPowerEnergyRefund(int roll, int energySpent)
    {
        if (GetRollTier(roll) == SoulRollTier.First)
        {
            return 1;
        }

        if (GetRollTier(roll) == SoulRollTier.Second)
        {
            return 2;
        }

        if (GetRollTier(roll) == SoulRollTier.Third)
        {
            return 3;
        }

        if (IsCriticalRoll(roll))
        {
            return energySpent + 2;
        }

        return 0;
    }

    private int GetSwordheartAmount(int roll)
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

    private void RecordRoll(SoulRollResult result, string source)
    {
        AssertMutable();
        _rollHistory.Add(new SoulRollHistoryEntry(result.Roll, result.DiscardedRoll, source));
    }

    private static string GetCardTitle(CardModel card)
    {
        return string.IsNullOrWhiteSpace(card?.Title) ? "未知卡牌" : card.Title;
    }

    private readonly struct SoulRollResult
    {
        public int Roll { get; }

        public int? DiscardedRoll { get; }

        public SoulRollResult(int roll, int? discardedRoll)
        {
            Roll = roll;
            DiscardedRoll = discardedRoll;
        }
    }

    private void ActivateIfCriticalRoll(int roll)
    {
        if (!IsCriticalRoll(roll))
        {
            return;
        }

        CriticalSuccessCountThisCombat++;
        Flash();
    }

    public SoulRollTier GetRollTier(int roll)
    {
        if (IsCriticalRoll(roll))
        {
            return SoulRollTier.Critical;
        }

        int firstTierLowerBound = Math.Max(_rollMin, 50 - GetChargeRangeBonus());
        if (roll >= firstTierLowerBound && roll <= FirstTierUpperBound)
        {
            return SoulRollTier.First;
        }

        if (roll >= SecondTierLowerBound && roll <= SecondTierUpperBound)
        {
            return SoulRollTier.Second;
        }

        return IsThirdTierRoll(roll) ? SoulRollTier.Third : SoulRollTier.None;
    }

    private bool IsCriticalRoll(int roll)
    {
        int criticalLowerBound = Math.Max(_rollMin, _rollMax - GetChargeRangeBonus());
        return roll >= criticalLowerBound && roll <= _rollMax;
    }

    private bool IsThirdTierRoll(int roll)
    {
        return roll > _rollMax ||
            roll >= ThirdTierLowerBound && roll < _rollMax && !IsCriticalRoll(roll);
    }

    private int GetChargeRangeBonus()
    {
        int chargeAmount = Owner.Creature.GetPower<ChargePower>()?.Amount ?? 0;
        return Math.Max(0, chargeAmount) * ChargePower.RangeShiftPerStack;
    }

    private int GetEffectMultiplier(int roll)
    {
        int multiplier = CrimsonBladeFlowPower.GetSoulOfAnchorEffectMultiplier(Owner.Creature);
        if (IsCriticalRoll(roll) && Owner.Creature.HasPower<TinyMomentsPower>())
        {
            multiplier *= TinyMomentsPower.CriticalEffectMultiplier;
        }

        return multiplier;
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

    private void BeginCardRollState(CardModel card)
    {
        if (TriggeringCard != null)
        {
            AssertMutable();
            _suspendedRollStates.Push(new SuspendedRollState(
                TriggeringCard,
                CurrentRoll,
                CurrentBonus,
                CurrentValueMultiplier,
                CurrentSwordheartAmount,
                CurrentCardEnergyCost,
                ActiveAttackCommand,
                CurrentAttackHitIndex,
                PendingSwordheartAmount,
                AttackOrSkillRollCount));
        }

        ResetCurrentRollState();
        TriggeringCard = card;
    }

    private void FinishCardRollState()
    {
        ResetCurrentRollState();
        if (_suspendedRollStates.Count == 0)
        {
            return;
        }

        AssertMutable();
        SuspendedRollState state = _suspendedRollStates.Pop();
        TriggeringCard = state.TriggeringCard;
        CurrentRoll = state.CurrentRoll;
        CurrentBonus = state.CurrentBonus;
        CurrentValueMultiplier = state.CurrentValueMultiplier;
        CurrentSwordheartAmount = state.CurrentSwordheartAmount;
        CurrentCardEnergyCost = state.CurrentCardEnergyCost;
        ActiveAttackCommand = state.ActiveAttackCommand;
        CurrentAttackHitIndex = state.CurrentAttackHitIndex;
        PendingSwordheartAmount = state.PendingSwordheartAmount;
        AttackOrSkillRollCount = state.AttackOrSkillRollCount;
    }

    private void ClearCardRollStates()
    {
        ResetCurrentRollState();
        AssertMutable();
        _pendingFragileTriggers = 0;
        _suspendedRollStates.Clear();
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

    private readonly struct SuspendedRollState
    {
        public CardModel TriggeringCard { get; }

        public int CurrentRoll { get; }

        public int CurrentBonus { get; }

        public decimal CurrentValueMultiplier { get; }

        public int CurrentSwordheartAmount { get; }

        public int CurrentCardEnergyCost { get; }

        public AttackCommand ActiveAttackCommand { get; }

        public int CurrentAttackHitIndex { get; }

        public int PendingSwordheartAmount { get; }

        public int AttackOrSkillRollCount { get; }

        public SuspendedRollState(
            CardModel triggeringCard,
            int currentRoll,
            int currentBonus,
            decimal currentValueMultiplier,
            int currentSwordheartAmount,
            int currentCardEnergyCost,
            AttackCommand activeAttackCommand,
            int currentAttackHitIndex,
            int pendingSwordheartAmount,
            int attackOrSkillRollCount)
        {
            TriggeringCard = triggeringCard;
            CurrentRoll = currentRoll;
            CurrentBonus = currentBonus;
            CurrentValueMultiplier = currentValueMultiplier;
            CurrentSwordheartAmount = currentSwordheartAmount;
            CurrentCardEnergyCost = currentCardEnergyCost;
            ActiveAttackCommand = activeAttackCommand;
            CurrentAttackHitIndex = currentAttackHitIndex;
            PendingSwordheartAmount = pendingSwordheartAmount;
            AttackOrSkillRollCount = attackOrSkillRollCount;
        }
    }
}
