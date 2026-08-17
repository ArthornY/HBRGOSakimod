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
    private const int _luckySevenSoulEnergyCost = 1;
    private const int _fullSwordheartRound = 7;
    private const decimal _firstTierValueBonusPerEnergy = 0.20m;
    private const decimal _secondTierValueBonusPerEnergy = 0.20m;
    private const decimal _thirdTierValueBonusPerEnergy = 0.25m;
    private const decimal _criticalValueBonusPerEnergy = 0.40m;

    private CardModel _triggeringCard;
    private int _currentRoll;
    private int _currentBonus;
    private decimal _currentValueMultiplier = 1m;
    private int _currentSwordheartAmount;
    private int _currentCardEnergyCost;
    private int _currentCardEnergySpent;
    private AttackCommand _activeAttackCommand;
    private int _currentAttackHitIndex = -1;
    private int _pendingSwordheartAmount;
    private int _pendingFragileTriggers;
    private int _attackOrSkillRollCount;
    private bool _reusePreparedRollForNextDamage;
    private bool _forceRollsToOneHundred;
    private bool _hasCheckedFullSwordheartRound;
    private bool _hasEnteredMagicSwordModeThisCombat;
    private int _criticalSuccessCountThisCombat;
    private int _lastRoll;
    private List<SoulRollHistoryEntry> _rollHistory = new();
    private Stack<SuspendedRollState> _suspendedRollStates = new();

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => IsMutable;

    public override int DisplayAmount => LastRoll;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/soul_of_anchor.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    public IReadOnlyList<SoulRollHistoryEntry> RollHistory => _rollHistory;

    protected virtual int FirstTierUpperBound => 75;

    protected virtual int SecondTierLowerBound => 76;

    protected virtual int SecondTierUpperBound => 90;

    protected virtual int ThirdTierLowerBound => 91;

    protected virtual bool GrantsSwordheart => true;

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

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasEnteredMagicSwordModeThisCombat
    {
        get => _hasEnteredMagicSwordModeThisCombat;
        private set
        {
            AssertMutable();
            _hasEnteredMagicSwordModeThisCombat = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRoll
    {
        get => _lastRoll;
        private set
        {
            AssertMutable();
            int nextValue = Math.Max(0, value);
            if (_lastRoll == nextValue)
            {
                return;
            }

            _lastRoll = nextValue;
            InvokeDisplayAmountChanged();
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

    private int CurrentCardEnergySpent
    {
        get => _currentCardEnergySpent;
        set
        {
            AssertMutable();
            _currentCardEnergySpent = value;
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
        CurrentCardEnergySpent = Math.Max(1, cardPlay.Resources.EnergySpent);
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
        CurrentCardEnergySpent = _luckySevenSoulEnergyCost;
        PrepareAttackOrSkillRoll(cardSource, _luckySevenSoulEnergyCost, includeMagicSwordBonus: false);
    }

    public async Task AfterLuckySevenDamageRoll(PlayerChoiceContext choiceContext)
    {
        await ApplyPendingSwordheart(choiceContext);
        await PlayPendingFragileCards(choiceContext);
        await ConsumeReservedRollModifierPowers(
            choiceContext,
            TriggeringCard);
        FinishCardRollState();
    }

    public async Task<int> RollExtraD100(
        PlayerChoiceContext choiceContext,
        CardModel cardSource,
        bool allowSwordheart)
    {
        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus: true,
            cardSource: cardSource,
            allowMaidBonus: false,
            advantageOnly: true);
        int roll = rollResult.Roll;
        RecordRoll(rollResult, $"「还没有结束」额外掷骰，由「{GetCardTitle(cardSource)}」触发");
        QueueFragileTrigger(roll);
        int swordheartAmount = CanGainSwordheartFromCard(cardSource)
            ? GetNonCardSwordheartAmount(roll, allowSwordheart)
            : 0;
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
        await ConsumeReservedRollModifierPowers(
            choiceContext,
            cardSource);
        return roll;
    }

    public bool IsRegularSwordheartTriggerRoll(int roll)
    {
        return GrantsSwordheart &&
               !IsCriticalRoll(roll) &&
               GetRegularSwordheartCount(roll) > 0;
    }

    public void SetRollsFixedToOneHundred(bool enabled)
    {
        RollsAreFixedToOneHundred = enabled;
    }

    public void RecordMagicSwordModeEntered()
    {
        HasEnteredMagicSwordModeThisCombat = true;
    }

    public void RecordAbilityRoll(int roll, int? discardedRoll, string source)
    {
        SoulRollResult result = RegisterRollResult(new SoulRollResult(roll, discardedRoll));
        RecordRoll(result, source);
    }

    public void RecordAbilityRoll(
        int roll,
        IReadOnlyList<int> discardedRolls,
        string source)
    {
        SoulRollResult result = RegisterRollResult(
            new SoulRollResult(roll, discardedRolls));
        RecordRoll(result, source);
    }

    public void RecordAbilityRoll(
        SoulRollBreakdown winningRoll,
        IReadOnlyList<SoulRollBreakdown> discardedRolls,
        string source)
    {
        SoulRollResult result = RegisterRollResult(
            new SoulRollResult(winningRoll, discardedRolls));
        RecordRoll(result, source);
    }

    public int GetCurrentRollFor(CardModel cardSource)
    {
        return cardSource != null && cardSource == TriggeringCard
            ? CurrentRoll
            : 0;
    }

    public int EnsureCurrentRollFor(CardModel cardSource)
    {
        if (cardSource == null ||
            cardSource != TriggeringCard ||
            cardSource.Owner != Owner ||
            cardSource.Type is not (CardType.Attack or CardType.Skill))
        {
            return 0;
        }

        if (AttackOrSkillRollCount == 0)
        {
            PrepareAttackOrSkillRoll(
                cardSource,
                CurrentCardEnergyCost,
                includeMagicSwordBonus: true);
            _reusePreparedRollForNextDamage = true;
        }

        return CurrentRoll;
    }

    public override Task BeforeCombatStart()
    {
        ClearCardRollStates();
        _hasCheckedFullSwordheartRound = false;
        HasEnteredMagicSwordModeThisCombat = false;
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
            !GrantsSwordheart ||
            _hasCheckedFullSwordheartRound ||
            combatState.RoundNumber != _fullSwordheartRound ||
            side != Owner.Creature.Side ||
            !participants.Contains(Owner.Creature))
        {
            return;
        }

        SoulOfAnchor roundSevenSoul = Owner.Relics
            .OfType<SoulOfAnchor>()
            .FirstOrDefault();
        if (roundSevenSoul != null && !ReferenceEquals(roundSevenSoul, this))
        {
            return;
        }

        _hasCheckedFullSwordheartRound = true;
        if (IsMagicSwordModeActive())
        {
            return;
        }

        int swordheartAmount = HasEnteredMagicSwordModeThisCombat
            ? 2
            : SwordheartPower.GetMaxAmount(Owner.Creature);
        int amountToApply = GetSwordheartAmountToApply(swordheartAmount);
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

        return HbrgoSakiDamageMultiplierHelper.Add(
            CurrentValueMultiplier,
            this);
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
                int effectMultiplier = GetEffectMultiplier(CurrentRoll);
                (int drawAmount, int energyAmount) =
                    GetPowerCardEffect(CurrentRoll);
                drawAmount *= effectMultiplier;
                energyAmount *= effectMultiplier;

                if (drawAmount > 0 || energyAmount > 0)
                {
                    Flash();
                }

                if (drawAmount > 0)
                {
                    await CardPileCmd.Draw(
                        choiceContext,
                        drawAmount,
                        Owner);
                }

                if (energyAmount > 0)
                {
                    await PlayerCmd.GainEnergy(energyAmount, Owner);
                }

                if (IsCriticalRoll(CurrentRoll))
                {
                    Owner.GetRelic<ElectronicSoldierManual>()
                        ?.QueueSixSenseReward(effectMultiplier);
                }
            }

            await ApplyPendingSwordheart(choiceContext);
            await PlayPendingFragileCards(choiceContext);
        }
        finally
        {
            await ConsumeReservedRollModifierPowers(
                choiceContext,
                cardPlay.Card);
            FinishCardRollState();
        }
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        ClearCardRollStates();
        _hasCheckedFullSwordheartRound = false;
        HasEnteredMagicSwordModeThisCombat = false;
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
        _currentCardEnergySpent = 0;
        _activeAttackCommand = null;
        _currentAttackHitIndex = -1;
        _pendingSwordheartAmount = 0;
        _pendingFragileTriggers = 0;
        _attackOrSkillRollCount = 0;
        _reusePreparedRollForNextDamage = false;
        _hasCheckedFullSwordheartRound = false;
        _hasEnteredMagicSwordModeThisCombat = false;
        _criticalSuccessCountThisCombat = 0;
        _rollHistory = new List<SoulRollHistoryEntry>();
        _suspendedRollStates = new Stack<SuspendedRollState>();
    }

    private SoulRollResult RollD100(
        bool includeMagicSwordBonus,
        CardModel cardSource = null,
        bool allowMaidBonus = false,
        bool advantageOnly = false)
    {
        StrengthenPower strengthen =
            Owner.Creature.GetPower<StrengthenPower>();
        bool useStrengthen = strengthen?.TryReserveRoll() == true;
        ExtremeDodgeRollPenaltyPower rollPenalty = advantageOnly
            ? null
            : Owner.Creature.GetPower<ExtremeDodgeRollPenaltyPower>();
        bool useRollPenalty =
            rollPenalty?.TryReserveRoll() == true;
        if (RollsAreFixedToOneHundred)
        {
            return RegisterRollResult(new SoulRollResult(
                SoulRollBreakdown.FromFinalRoll(
                    _rollMax,
                    "控制台指令：固定出目"),
                Array.Empty<SoulRollBreakdown>()));
        }

        List<SoulRollBreakdown> rolls = new()
        {
            RollD100Die("基础骰", advantageOnly)
        };
        GuideLittleHand guideLittleHand = Owner.GetRelic<GuideLittleHand>();
        if (guideLittleHand != null)
        {
            rolls.Add(RollD100Die("优势骰：导游的小手", advantageOnly));
            guideLittleHand.TakeHigherRoll(
                rolls[0].FinalRoll,
                rolls[1].FinalRoll);
        }

        if (useStrengthen)
        {
            rolls.Add(RollD100Die("优势骰：强化", advantageOnly));
        }

        ProtagonistPrivilegePower protagonistPrivilege =
            Owner.Creature.GetPower<ProtagonistPrivilegePower>();
        if (protagonistPrivilege != null)
        {
            rolls.Add(RollD100Die(
                "优势骰：主人公特典",
                advantageOnly));
        }

        bool isAttackCardRoll =
            !advantageOnly &&
            cardSource?.Type == CardType.Attack;
        List<SoulRollBonusDetail> sharedBonuses = new();
        if (useStrengthen && !advantageOnly)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                strengthen.RollBonus,
                "强化"));
        }

        if (useRollPenalty)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                ExtremeDodgeRollPenaltyPower.RollPenalty,
                "极限闪避"));
        }

        if (!advantageOnly &&
            protagonistPrivilege != null)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                protagonistPrivilege.RollPenalty,
                "主人公特典"));
        }

        if (!advantageOnly)
        {
            int accelerationBonus = Owner.Creature
                .GetPower<AccelerationPower>()
                ?.Amount ?? 0;
            if (accelerationBonus != 0)
            {
                sharedBonuses.Add(new SoulRollBonusDetail(
                    accelerationBonus,
                    "加速"));
            }
        }

        if (isAttackCardRoll &&
            includeMagicSwordBonus &&
            IsMagicSwordModeActive())
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                MagicSwordModePower.RollBonus,
                "魔剑模式"));
        }

        if (isAttackCardRoll)
        {
            int sharingBonus = Owner.Creature
                .GetPower<MagicSwordSharingPower>()
                ?.Amount ?? 0;
            if (sharingBonus != 0)
            {
                sharedBonuses.Add(new SoulRollBonusDetail(
                    sharingBonus,
                    "魔剑共享"));
            }
        }

        if (isAttackCardRoll &&
            Owner.Creature.HasPower<NotOverYetPower>())
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                NotOverYetPower.RollBonus,
                "还没有结束"));
        }

        if (allowMaidBonus)
        {
            int maidBonus = Owner.Creature
                .GetPower<MaidCultivationPower>()
                ?.GetRollBonus(cardSource) ?? 0;
            if (maidBonus != 0)
            {
                sharedBonuses.Add(new SoulRollBonusDetail(
                    maidBonus,
                    "女仆的素养"));
            }
        }

        if (!advantageOnly)
        {
            int nextRollBonus = Owner.Creature
                .GetPower<NextRollBonusPower>()
                ?.GetRollBonus(cardSource) ?? 0;
            if (nextRollBonus != 0)
            {
                sharedBonuses.Add(new SoulRollBonusDetail(
                    nextRollBonus,
                    "渐入佳境"));
            }
        }

        List<SoulRollBreakdown> finalRolls = rolls
            .Select(roll => ApplyRollBonuses(roll, sharedBonuses))
            .ToList();
        int winningIndex = GetWinningRollIndex(
            finalRolls.Select(roll => roll.FinalRoll).ToArray());
        SoulRollBreakdown winningRoll = finalRolls[winningIndex];
        List<SoulRollBreakdown> discardedRolls = finalRolls
            .Where((_, index) => index != winningIndex)
            .ToList();

        return RegisterRollResult(new SoulRollResult(
            winningRoll,
            discardedRolls));
    }

    private int GetWinningRollIndex(IReadOnlyList<int> rolls)
    {
        int winningIndex = 0;
        bool hasCritical = IsCriticalRoll(rolls[0]);
        for (int index = 1; index < rolls.Count; index++)
        {
            bool isCritical = IsCriticalRoll(rolls[index]);
            if (isCritical && !hasCritical ||
                isCritical == hasCritical &&
                rolls[index] > rolls[winningIndex])
            {
                winningIndex = index;
                hasCritical = isCritical;
            }
        }

        return winningIndex;
    }

    private SoulRollResult RegisterRollResult(SoulRollResult result)
    {
        LastRoll = result.Roll;
        return result;
    }

    private SoulRollBreakdown RollD100Die(
        string diceSource,
        bool advantageOnly)
    {
        int baseRoll = RollRawD100();
        int finalRoll = baseRoll;
        List<SoulRollBonusDetail> bonuses = new();
        if (!advantageOnly &&
            Owner.Creature.HasPower<ReptureDualSwordsPower>() &&
            baseRoll is 7 or 77)
        {
            int adjustment = _rollMax - baseRoll;
            bonuses.Add(new SoulRollBonusDetail(
                adjustment,
                "Repture-双剑（7或77视为100）"));
            finalRoll = _rollMax;
        }

        return new SoulRollBreakdown(
            baseRoll,
            finalRoll,
            diceSource,
            bonuses);
    }

    private static SoulRollBreakdown ApplyRollBonuses(
        SoulRollBreakdown roll,
        IReadOnlyList<SoulRollBonusDetail> sharedBonuses)
    {
        if (sharedBonuses == null || sharedBonuses.Count == 0)
        {
            return roll;
        }

        SoulRollBonusDetail[] bonuses = roll.Bonuses
            .Concat(sharedBonuses)
            .ToArray();
        return new SoulRollBreakdown(
            roll.BaseRoll,
            roll.FinalRoll + sharedBonuses.Sum(bonus => bonus.Amount),
            roll.DiceSource,
            bonuses);
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

        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus,
            card,
            allowMaidBonus: card.Type == CardType.Attack);
        int roll = rollResult.Roll;
        RecordRoll(rollResult, $"打出「{GetCardTitle(card)}」");
        QueueFragileTrigger(roll);
        int effectMultiplier = GetEffectMultiplier(roll);
        TriggeringCard = card;
        CurrentRoll = roll;
        CurrentBonus = 0;
        CurrentValueMultiplier = GetAttackOrSkillValueMultiplier(
            roll,
            energyCost,
            effectMultiplier);
        QueueCardSwordheart(roll, card, effectMultiplier);
        AttackOrSkillRollCount++;
        ActivateIfCriticalRoll(roll);
    }

    private void PreparePowerRoll(CardModel card, bool includeMagicSwordBonus)
    {
        if (card.Owner != Owner || card.Type != CardType.Power)
        {
            return;
        }

        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus,
            card,
            allowMaidBonus: false);
        int roll = rollResult.Roll;
        RecordRoll(rollResult, $"打出「{GetCardTitle(card)}」");
        QueueFragileTrigger(roll);
        int effectMultiplier = GetEffectMultiplier(roll);
        TriggeringCard = card;
        CurrentRoll = roll;
        QueueCardSwordheart(roll, card, effectMultiplier);
        ActivateIfCriticalRoll(roll);
    }

    private async Task ApplyPendingSwordheart(PlayerChoiceContext choiceContext)
    {
        if (!GrantsSwordheart)
        {
            CurrentSwordheartAmount = 0;
            PendingSwordheartAmount = 0;
            return;
        }

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

    private decimal GetAttackOrSkillValueMultiplier(
        int roll,
        int energyCost,
        int effectMultiplier)
    {
        decimal bonusPerEnergy = GetRollTier(roll) switch
        {
            SoulRollTier.First => _firstTierValueBonusPerEnergy,
            SoulRollTier.Second => _secondTierValueBonusPerEnergy,
            SoulRollTier.Third => _thirdTierValueBonusPerEnergy,
            SoulRollTier.Critical => _criticalValueBonusPerEnergy,
            _ => 0m
        };
        decimal bonusMultiplier = Math.Max(0, energyCost) *
            bonusPerEnergy *
            Math.Max(1, effectMultiplier);
        return 1m + bonusMultiplier;
    }

    private (int DrawAmount, int EnergyAmount) GetPowerCardEffect(int roll)
    {
        return GetRollTier(roll) switch
        {
            SoulRollTier.First => (1, 0),
            SoulRollTier.Second => (2, 1),
            SoulRollTier.Third => (4, 1),
            SoulRollTier.Critical => (4, 1),
            _ => (0, 0)
        };
    }

    private void QueueCardSwordheart(int roll, CardModel card, int effectMultiplier)
    {
        CurrentSwordheartAmount = 0;
        if (!CanGainSwordheartFromCard(card))
        {
            return;
        }

        if (IsCriticalRoll(roll))
        {
            CurrentSwordheartAmount = SwordheartPower.GetMaxAmount(Owner.Creature);
            PendingSwordheartAmount = Math.Max(
                PendingSwordheartAmount,
                CurrentSwordheartAmount);
            return;
        }

        int triggerCount = GetRegularSwordheartCount(roll);
        if (triggerCount <= 0)
        {
            return;
        }

        int multiplier = Math.Max(1, effectMultiplier);
        int perCardCap = Math.Max(1, CurrentCardEnergySpent) * multiplier;
        int remainingCapacity = Math.Max(
            0,
            perCardCap - PendingSwordheartAmount);
        CurrentSwordheartAmount = Math.Min(
            remainingCapacity,
            triggerCount * multiplier);
        PendingSwordheartAmount += CurrentSwordheartAmount;
    }

    private int GetNonCardSwordheartAmount(int roll, bool allowRegularSwordheart)
    {
        if (!GrantsSwordheart)
        {
            return 0;
        }

        if (IsCriticalRoll(roll))
        {
            return SwordheartPower.GetMaxAmount(Owner.Creature);
        }

        return allowRegularSwordheart &&
               GetRegularSwordheartCount(roll) > 0
            ? 1
            : 0;
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

    private int GetRegularSwordheartCount(int roll)
    {
        int sevenCount = CountSevens(roll);
        if (sevenCount > 0)
        {
            return sevenCount;
        }

        return roll > 0 &&
               roll % 7 == 0 &&
               Owner.Creature.HasPower<SwordheartClarityPower>()
            ? 1
            : 0;
    }

    private void RecordRoll(SoulRollResult result, string source)
    {
        AssertMutable();
        _rollHistory.Add(new SoulRollHistoryEntry(
            result.WinningRoll,
            result.DiscardedRollDetails,
            source));
    }

    private static string GetCardTitle(CardModel card)
    {
        return string.IsNullOrWhiteSpace(card?.Title) ? "未知卡牌" : card.Title;
    }

    private readonly struct SoulRollResult
    {
        public SoulRollBreakdown WinningRoll { get; }

        public IReadOnlyList<SoulRollBreakdown> DiscardedRollDetails { get; }

        public int Roll => WinningRoll.FinalRoll;

        public IReadOnlyList<int> DiscardedRolls =>
            DiscardedRollDetails
                .Select(roll => roll.FinalRoll)
                .ToArray();

        public SoulRollResult(int roll, int? discardedRoll)
            : this(
                SoulRollBreakdown.FromFinalRoll(roll),
                discardedRoll.HasValue
                    ? new[]
                    {
                        SoulRollBreakdown.FromFinalRoll(
                            discardedRoll.Value,
                            "优势骰")
                    }
                    : Array.Empty<SoulRollBreakdown>())
        {
        }

        public SoulRollResult(
            int roll,
            IReadOnlyList<int> discardedRolls)
            : this(
                SoulRollBreakdown.FromFinalRoll(roll),
                discardedRolls?
                    .Select(discardedRoll =>
                        SoulRollBreakdown.FromFinalRoll(
                            discardedRoll,
                            "优势骰"))
                    .ToArray())
        {
        }

        public SoulRollResult(
            SoulRollBreakdown winningRoll,
            IReadOnlyList<SoulRollBreakdown> discardedRolls)
        {
            WinningRoll =
                winningRoll ?? SoulRollBreakdown.FromFinalRoll(0);
            DiscardedRollDetails =
                discardedRolls ?? Array.Empty<SoulRollBreakdown>();
        }
    }

    private async Task ConsumeReservedRollModifierPowers(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        StrengthenPower strengthen =
            Owner.Creature.GetPower<StrengthenPower>();
        if (strengthen != null)
        {
            await strengthen.ConsumeReservedRolls(
                choiceContext,
                cardSource);
        }

        ExtremeDodgeRollPenaltyPower rollPenalty =
            Owner.Creature.GetPower<ExtremeDodgeRollPenaltyPower>();
        if (rollPenalty != null)
        {
            await rollPenalty.ConsumeReservedRolls(
                choiceContext,
                cardSource);
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
        int criticalUpperBound = _rollMax + GetChargeRangeBonus();
        return roll >= _rollMax && roll <= criticalUpperBound;
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

    private bool CanGainSwordheartFromCard(CardModel card)
    {
        return GrantsSwordheart &&
               !IsMagicSwordModeActive() &&
               !(Owner.Creature.HasPower<ReptureDualSwordsPower>() &&
                 (card?.IsDupe == true ||
                  DualSwordsCopyHelper.IsDualSwordsCopy(card)));
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

        if (_reusePreparedRollForNextDamage)
        {
            _reusePreparedRollForNextDamage = false;
            return;
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
                CurrentCardEnergySpent,
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
        CurrentCardEnergySpent = state.CurrentCardEnergySpent;
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
        CurrentCardEnergySpent = 0;
        ActiveAttackCommand = null;
        CurrentAttackHitIndex = -1;
        PendingSwordheartAmount = 0;
        AttackOrSkillRollCount = 0;
        _reusePreparedRollForNextDamage = false;
    }

    private readonly struct SuspendedRollState
    {
        public CardModel TriggeringCard { get; }

        public int CurrentRoll { get; }

        public int CurrentBonus { get; }

        public decimal CurrentValueMultiplier { get; }

        public int CurrentSwordheartAmount { get; }

        public int CurrentCardEnergyCost { get; }

        public int CurrentCardEnergySpent { get; }

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
            int currentCardEnergySpent,
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
            CurrentCardEnergySpent = currentCardEnergySpent;
            ActiveAttackCommand = activeAttackCommand;
            CurrentAttackHitIndex = currentAttackHitIndex;
            PendingSwordheartAmount = pendingSwordheartAmount;
            AttackOrSkillRollCount = attackOrSkillRollCount;
        }
    }
}
