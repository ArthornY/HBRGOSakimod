using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Content;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Models.Capabilities;

/// <summary>
/// Supplies the Roll glossary and owns all gameplay initiated by the Roll
/// keyword. SoulOfAnchor is deliberately only a history sink.
/// </summary>
public sealed class RollGlossaryCapability :
    CardCapability,
    ICardDescriptionContributor,
    ICardHoverTipContributor
{
    private const string ModId = "HBRGOSakiMod";
    private const string LocTable = "card_keywords";

    private static readonly string RollKeywordId =
        ModContentRegistry.GetQualifiedKeywordId(ModId, "ROLL");

    private static readonly string CriticalSuccessKeywordId =
        ModContentRegistry.GetQualifiedKeywordId(
            ModId,
            "CRITICAL_SUCCESS");

    private bool _isCardRollActive;
    private bool _hasRolledForCurrentPlay;
    private int _currentRoll;
    private int _currentEnergySpent;
    private int _currentRegularSwordheartAmount;
    private int _lastCompletedEnergySpent;
    private int _lastCompletedRegularSwordheartAmount;
    private decimal _currentValueMultiplier = 1m;
    private int _pendingSwordheartAmount;
    private int _pendingFragileTriggers;
    private Stack<SuspendedRollState> _suspendedRollStates = new();

    /// <summary>
    /// The result shared by every base-value calculation made by the current
    /// play of the owning card. It is zero outside that play.
    /// </summary>
    public int CurrentRoll => _currentRoll;

    public override bool TryModifyKeywordsInCombat(
        CardModel card,
        ISet<CardKeyword> keywords)
    {
        if (!ReferenceEquals(card, Owner) ||
            !RollKeywordRules.IsRollEnabled(card) ||
            !ModKeywordRegistry.TryGetCardKeyword(
                RollKeywordId,
                out CardKeyword rollKeyword))
        {
            return false;
        }

        return keywords.Add(rollKeyword);
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!ShouldShowRollGlossary(card))
        {
            yield break;
        }

        yield return CreateHoverTip(
            RollKeywordId,
            CreateRollDescription(card));
        yield return CreateHoverTip(
            CriticalSuccessKeywordId,
            CreateCriticalSuccessDescription(card));
    }

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(
        CardDescriptionContext context)
    {
        CardModel card = context.Card;
        if (!ReferenceEquals(card, Owner) ||
            !ShouldShowRollGlossary(card))
        {
            yield break;
        }

        if (RollKeywordRules.IsMimicTechniqueCard(card))
        {
            yield break;
        }

        yield return new CardDescriptionFragment(
            CreateRollCardText(),
            CardDescriptionFragmentPlacement.BeforeBase);
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = Owner;
        if (card == null ||
            !ReferenceEquals(cardPlay?.Card, card) ||
            !HasEffectiveRollKeyword(card))
        {
            return Task.CompletedTask;
        }

        BeginCardRollState();
        _currentEnergySpent = Math.Max(
            0,
            cardPlay.Resources.EnergySpent);
        PrepareCardRoll(card, includeMagicSwordBonus: true);
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (!IsCurrentCardValue(cardSource) ||
            !props.IsPoweredAttack() ||
            dealer != Controller?.Creature)
        {
            return 1m;
        }

        return HbrgoSakiDamageMultiplierHelper.Add(
            _currentValueMultiplier,
            this);
    }

    public override decimal ModifyBlockMultiplicative(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        if (!IsCurrentCardValue(cardSource) ||
            !props.IsPoweredCardOrMonsterMoveBlock())
        {
            return 1m;
        }

        return _currentValueMultiplier;
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature target,
        CardModel cardSource)
    {
        if (!IsCurrentCardValue(cardSource) ||
            giver != Controller?.Creature ||
            ShouldIgnorePowerAmountBonus(power, amount))
        {
            return 1m;
        }

        return _currentValueMultiplier;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay?.Card, Owner) ||
            !_isCardRollActive)
        {
            return;
        }

        try
        {
            if (!_hasRolledForCurrentPlay)
            {
                PrepareCardRoll(Owner, includeMagicSwordBonus: true);
            }

            if (Owner.Type == CardType.Power)
            {
                await ApplyPowerCardReward(choiceContext);
            }

            await ApplyPendingSwordheart(choiceContext, Owner);
            await PlayPendingFragileCards(choiceContext);
        }
        finally
        {
            _lastCompletedEnergySpent = _currentEnergySpent;
            _lastCompletedRegularSwordheartAmount =
                _currentRegularSwordheartAmount;
            await ConsumeReservedRollModifierPowers(
                choiceContext,
                Owner);
            FinishCardRollState();
        }
    }

    public override Task BeforeCombatStart()
    {
        ResetAllTransientState();
        RollKeywordCombatState.BeginCombat(Controller);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetAllTransientState();
        RollKeywordCombatState.EndCombat(Controller);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures the current play has resolved its one shared Roll result.
    /// </summary>
    public int EnsureCurrentRoll()
    {
        return EnsureCurrentRollFor(Owner);
    }

    /// <summary>
    /// Compatibility form for card implementations migrating from the old
    /// SoulOfAnchor API.
    /// </summary>
    public int EnsureCurrentRoll(CardModel cardSource)
    {
        return EnsureCurrentRollFor(cardSource);
    }

    public int EnsureCurrentRollFor(CardModel cardSource)
    {
        if (!ReferenceEquals(cardSource, Owner) ||
            !HasEffectiveRollKeyword(cardSource))
        {
            return 0;
        }

        if (!_isCardRollActive)
        {
            BeginCardRollState();
        }

        if (!_hasRolledForCurrentPlay)
        {
            PrepareCardRoll(cardSource, includeMagicSwordBonus: true);
        }

        return _currentRoll;
    }

    public int GetCurrentRollFor(CardModel cardSource)
    {
        return ReferenceEquals(cardSource, Owner) &&
               _isCardRollActive
            ? _currentRoll
            : 0;
    }

    public SoulRollTier GetRollTier(int roll)
    {
        return RollKeywordRules.GetRollTier(Controller, roll);
    }

    public void SetRollsFixedToOneHundred(bool enabled)
    {
        RollKeywordCombatState.SetRollsFixedToOneHundred(
            Controller,
            enabled);
    }

    /// <summary>
    /// Resolves an additional d100 introduced by a Roll-enabled card effect.
    /// It deliberately uses the legacy "advantage only" modifier set used by
    /// NotOverYet.
    /// </summary>
    public async Task<int> RollExtraD100(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        if (!ReferenceEquals(cardSource, Owner) ||
            !HasEffectiveRollKeyword(cardSource))
        {
            return 0;
        }

        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus: true,
            cardSource,
            allowMaidBonus: false,
            advantageOnly: true);
        int roll = rollResult.Roll;
        RecordRoll(
            rollResult,
            $"「还没有结束」额外掷骰，由「{GetCardTitle(cardSource)}」触发");
        RegisterResolvedRoll(roll);

        int swordheartAmount = ReserveExtraRollSwordheartAmount(
            cardSource,
            roll);
        if (swordheartAmount > 0)
        {
            await ApplySwordheart(
                choiceContext,
                cardSource,
                swordheartAmount);
        }

        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }

        await PlayPendingFragileCards(choiceContext);
        await ConsumeReservedRollModifierPowers(
            choiceContext,
            cardSource);
        return roll;
    }

    /// <summary>
    /// Legacy special-roll entry point retained for callers while they move
    /// from the old relic implementation to this card capability.
    /// </summary>
    public void PrepareLuckySevenDamageRoll(CardModel cardSource)
    {
        if (!ReferenceEquals(cardSource, Owner) ||
            !HasEffectiveRollKeyword(cardSource))
        {
            return;
        }

        if (!_isCardRollActive)
        {
            BeginCardRollState();
            _currentEnergySpent = 1;
        }

        if (!_hasRolledForCurrentPlay)
        {
            PrepareCardRoll(
                cardSource,
                includeMagicSwordBonus: false);
        }
    }

    public async Task AfterLuckySevenDamageRoll(
        PlayerChoiceContext choiceContext)
    {
        if (!_isCardRollActive)
        {
            return;
        }

        try
        {
            await ApplyPendingSwordheart(choiceContext, Owner);
            await PlayPendingFragileCards(choiceContext);
        }
        finally
        {
            await ConsumeReservedRollModifierPowers(
                choiceContext,
                Owner);
            FinishCardRollState();
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _suspendedRollStates = new Stack<SuspendedRollState>();
        ResetCurrentRollState();
        _lastCompletedEnergySpent = 0;
        _lastCompletedRegularSwordheartAmount = 0;
    }

    private Player Controller => Owner?.Owner;

    private static bool ShouldShowRollGlossary(CardModel card)
    {
        if (!RollKeywordRules.IsSupportedCardType(card))
        {
            return false;
        }

        return HasEffectiveRollKeyword(card) ||
               RollKeywordRules.IsRollEnabled(card);
    }

    private static bool HasEffectiveRollKeyword(CardModel card)
    {
        return card != null &&
               RollKeywordRules.IsSupportedCardType(card) &&
               ModKeywordRegistry.TryGetCardKeyword(
                   RollKeywordId,
                   out CardKeyword rollKeyword) &&
               card.Keywords.Contains(rollKeyword);
    }

    private static string GetRollDescriptionKey(
        CardType cardType,
        bool isSaki)
    {
        if (cardType == CardType.Power)
        {
            return isSaki
                ? $"{RollKeywordId}.sakiPowerDescription"
                : $"{RollKeywordId}.powerDescription";
        }

        return isSaki
            ? $"{RollKeywordId}.sakiAttackSkillDescription"
            : $"{RollKeywordId}.attackSkillDescription";
    }

    private static LocString CreateRollDescription(CardModel card)
    {
        LocString description = new(
            LocTable,
            GetRollDescriptionKey(
                card.Type,
                RollKeywordRules.UsesSakiRules(card)));
        description.Add(
            "energyPrefix",
            EnergyIconHelper.GetPrefix(card));
        return description;
    }

    private static LocString CreateRollCardText()
    {
        return new LocString(
            LocTable,
            $"{RollKeywordId}.cardText");
    }

    private static LocString CreateCriticalSuccessDescription(
        CardModel card)
    {
        return new LocString(
            LocTable,
            RollKeywordRules.UsesSakiRules(card)
                ? $"{CriticalSuccessKeywordId}.sakiDescription"
                : $"{CriticalSuccessKeywordId}.description");
    }

    private static IHoverTip CreateHoverTip(
        string keywordId,
        LocString description)
    {
        return new HoverTip(
            ModKeywordRegistry.GetTitle(keywordId),
            description)
        {
            Id = keywordId
        };
    }

    private void PrepareCardRoll(
        CardModel card,
        bool includeMagicSwordBonus)
    {
        if (_hasRolledForCurrentPlay ||
            !ReferenceEquals(card, Owner) ||
            !HasEffectiveRollKeyword(card))
        {
            return;
        }

        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus,
            card,
            allowMaidBonus: card.Type == CardType.Attack,
            advantageOnly: false);
        int roll = rollResult.Roll;

        AssertMutable();
        _hasRolledForCurrentPlay = true;
        _currentRoll = roll;
        _currentValueMultiplier = card.Type is
            CardType.Attack or CardType.Skill
                ? RollKeywordRules.GetAttackOrSkillValueMultiplier(
                    Controller,
                    roll)
                : 1m;

        RecordRoll(
            rollResult,
            $"打出「{GetCardTitle(card)}」");
        RegisterResolvedRoll(roll);
        QueueCardSwordheart(roll, card);
        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }
    }

    private SoulRollResult RollD100(
        bool includeMagicSwordBonus,
        CardModel cardSource,
        bool allowMaidBonus,
        bool advantageOnly)
    {
        Player player = Controller;
        if (player?.RunState == null)
        {
            return new SoulRollResult(
                SoulRollBreakdown.FromFinalRoll(0),
                Array.Empty<SoulRollBreakdown>());
        }

        StrengthenPower strengthen =
            player.Creature.GetPower<StrengthenPower>();
        bool useStrengthen =
            strengthen?.TryReserveRoll() == true;
        ExtremeDodgeRollPenaltyPower rollPenalty = advantageOnly
            ? null
            : player.Creature
                .GetPower<ExtremeDodgeRollPenaltyPower>();
        bool useRollPenalty =
            rollPenalty?.TryReserveRoll() == true;

        if (RollKeywordCombatState.RollsAreFixedToOneHundred(player))
        {
            return new SoulRollResult(
                SoulRollBreakdown.FromFinalRoll(
                    RollKeywordRules.RollMaximum,
                    "控制台指令：固定出目"),
                Array.Empty<SoulRollBreakdown>());
        }

        List<SoulRollBreakdown> rolls = new()
        {
            RollD100Die("基础骰", advantageOnly)
        };

        GuideLittleHand guideLittleHand =
            player.GetRelic<GuideLittleHand>();
        if (guideLittleHand != null)
        {
            rolls.Add(RollD100Die(
                "优势骰：导游的小手",
                advantageOnly));
            guideLittleHand.TakeHigherRoll(
                rolls[0].FinalRoll,
                rolls[1].FinalRoll);
        }

        if (useStrengthen)
        {
            rolls.Add(RollD100Die(
                "优势骰：强化",
                advantageOnly));
        }

        ProtagonistPrivilegePower protagonistPrivilege =
            player.Creature.GetPower<ProtagonistPrivilegePower>();
        if (protagonistPrivilege != null)
        {
            rolls.Add(RollD100Die(
                "优势骰：主人公特典",
                advantageOnly));
        }

        bool isAttackCardRoll =
            !advantageOnly && cardSource?.Type == CardType.Attack;
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

        if (!advantageOnly && protagonistPrivilege != null)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                protagonistPrivilege.RollPenalty,
                "主人公特典"));
        }

        if (!advantageOnly)
        {
            int accelerationBonus = player.Creature
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
            player.Creature.HasPower<MagicSwordModePower>())
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                MagicSwordModePower.RollBonus,
                "魔剑模式"));
        }

        if (isAttackCardRoll)
        {
            int sharingBonus = player.Creature
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
            player.Creature.HasPower<NotOverYetPower>())
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                NotOverYetPower.RollBonus,
                "还没有结束"));
        }

        if (allowMaidBonus)
        {
            int maidBonus = player.Creature
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
            int nextRollBonus = player.Creature
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
            .Select(roll => ApplyRollBonuses(
                roll,
                sharedBonuses))
            .ToList();
        int winningIndex = GetWinningRollIndex(
            finalRolls.Select(roll => roll.FinalRoll).ToArray());
        SoulRollBreakdown winningRoll = finalRolls[winningIndex];
        SoulRollBreakdown[] discardedRolls = finalRolls
            .Where((_, index) => index != winningIndex)
            .ToArray();

        return new SoulRollResult(winningRoll, discardedRolls);
    }

    private SoulRollBreakdown RollD100Die(
        string diceSource,
        bool advantageOnly)
    {
        Player player = Controller;
        int baseRoll = player.RunState.Rng.Niche.NextInt(
            RollKeywordRules.RollMinimum,
            RollKeywordRules.RollExclusiveMaximum);
        int finalRoll = baseRoll;
        List<SoulRollBonusDetail> bonuses = new();

        if (!advantageOnly &&
            player.Creature.HasPower<ReptureDualSwordsPower>() &&
            baseRoll is 7 or 77)
        {
            int adjustment = RollKeywordRules.RollMaximum - baseRoll;
            bonuses.Add(new SoulRollBonusDetail(
                adjustment,
                "Repture-双剑（7或77视为100）"));
            finalRoll = RollKeywordRules.RollMaximum;
        }

        return new SoulRollBreakdown(
            baseRoll,
            finalRoll,
            diceSource,
            bonuses);
    }

    private int GetWinningRollIndex(IReadOnlyList<int> rolls)
    {
        int winningIndex = 0;
        bool hasCritical = RollKeywordRules.IsCriticalRoll(
            Controller,
            rolls[0]);
        for (int index = 1; index < rolls.Count; index++)
        {
            bool isCritical = RollKeywordRules.IsCriticalRoll(
                Controller,
                rolls[index]);
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
            roll.FinalRoll + sharedBonuses.Sum(
                bonus => bonus.Amount),
            roll.DiceSource,
            bonuses);
    }

    private void RegisterResolvedRoll(int roll)
    {
        if (!RollKeywordRules.IsCriticalRoll(Controller, roll))
        {
            return;
        }

        RollKeywordCombatState.RecordCriticalSuccess(Controller);
        Controller?.GetRelic<ElectronicSoldierManual>()
            ?.QueueSixSenseReward();
    }

    private void RecordRoll(
        SoulRollResult result,
        string source)
    {
        foreach (SoulOfAnchor history in
                 Controller?.Relics.OfType<SoulOfAnchor>() ??
                 Enumerable.Empty<SoulOfAnchor>())
        {
            history.RecordRoll(
                result.WinningRoll,
                result.DiscardedRolls,
                source);
        }
    }

    private void QueueCardSwordheart(int roll, CardModel card)
    {
        Player player = Controller;
        if (!RollKeywordRules.UsesSakiRules(player) ||
            player?.Creature == null ||
            player.Creature.HasPower<MagicSwordModePower>())
        {
            return;
        }

        if (RollKeywordRules.IsCriticalRoll(player, roll))
        {
            _pendingSwordheartAmount = Math.Max(
                _pendingSwordheartAmount,
                SwordheartPower.GetMaxAmount(player.Creature));
            return;
        }

        if (RollKeywordRules.IsCopiedCard(card))
        {
            return;
        }

        int requestedAmount =
            RollKeywordRules.GetRegularSwordheartAmount(player, roll);
        int remainingAmount = Math.Max(
            0,
            _currentEnergySpent - _currentRegularSwordheartAmount);
        int amountToQueue = Math.Min(requestedAmount, remainingAmount);
        if (amountToQueue <= 0)
        {
            return;
        }

        _pendingSwordheartAmount += amountToQueue;
        _currentRegularSwordheartAmount += amountToQueue;
    }

    private int ReserveExtraRollSwordheartAmount(
        CardModel cardSource,
        int roll)
    {
        Player player = Controller;
        if (!RollKeywordRules.UsesSakiRules(player) ||
            player?.Creature == null ||
            player.Creature.HasPower<MagicSwordModePower>())
        {
            return 0;
        }

        if (RollKeywordRules.IsCriticalRoll(player, roll))
        {
            return SwordheartPower.GetMaxAmount(player.Creature);
        }

        if (RollKeywordRules.IsCopiedCard(cardSource))
        {
            return 0;
        }

        int requestedAmount =
            RollKeywordRules.GetRegularSwordheartAmount(player, roll);
        if (requestedAmount <= 0)
        {
            return 0;
        }

        int energySpent = _isCardRollActive
            ? _currentEnergySpent
            : _lastCompletedEnergySpent;
        int alreadyReserved = _isCardRollActive
            ? _currentRegularSwordheartAmount
            : _lastCompletedRegularSwordheartAmount;
        int amountToReserve = Math.Min(
            requestedAmount,
            Math.Max(0, energySpent - alreadyReserved));
        if (amountToReserve <= 0)
        {
            return 0;
        }

        if (_isCardRollActive)
        {
            _currentRegularSwordheartAmount += amountToReserve;
        }
        else
        {
            _lastCompletedRegularSwordheartAmount += amountToReserve;
        }

        return amountToReserve;
    }

    private async Task ApplyPowerCardReward(
        PlayerChoiceContext choiceContext)
    {
        Player player = Controller;
        if (player == null)
        {
            return;
        }

        (int drawAmount, int energyAmount) =
            RollKeywordRules.GetPowerCardEffect(
                player,
                _currentRoll);
        if (drawAmount > 0)
        {
            await CardPileCmd.Draw(
                choiceContext,
                drawAmount,
                player);
        }

        if (energyAmount > 0)
        {
            await PlayerCmd.GainEnergy(
                energyAmount,
                player);
        }
    }

    private async Task ApplyPendingSwordheart(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        int amount = _pendingSwordheartAmount;
        _pendingSwordheartAmount = 0;
        if (amount > 0)
        {
            await ApplySwordheart(
                choiceContext,
                cardSource,
                amount);
        }
    }

    private async Task ApplySwordheart(
        PlayerChoiceContext choiceContext,
        CardModel cardSource,
        int requestedAmount)
    {
        Creature creature = Controller?.Creature;
        if (creature == null || requestedAmount <= 0)
        {
            return;
        }

        SwordheartPower swordheart =
            creature.GetPower<SwordheartPower>();
        int currentAmount = swordheart?.Amount ?? 0;
        int remainingAmount = Math.Max(
            0,
            SwordheartPower.GetMaxAmount(creature) - currentAmount);
        int amountToApply = Math.Min(
            requestedAmount,
            remainingAmount);
        if (amountToApply <= 0)
        {
            return;
        }

        await PowerCmd.Apply<SwordheartPower>(
            choiceContext,
            creature,
            amountToApply,
            creature,
            cardSource);
    }

    private async Task PlayPendingFragileCards(
        PlayerChoiceContext choiceContext)
    {
        while (_pendingFragileTriggers > 0)
        {
            _pendingFragileTriggers--;
            Player player = Controller;
            if (player?.Creature?.CombatState == null)
            {
                return;
            }

            FragileHbrgoSaki card = player.PlayerCombatState?.AllCards
                .OfType<FragileHbrgoSaki>()
                .FirstOrDefault(candidate =>
                    !candidate.HasBeenRemovedFromState &&
                    candidate.Pile?.IsCombatPile == true &&
                    candidate.Pile.Type is not
                        (PileType.Play or PileType.Exhaust));
            Creature target = player.Creature.CombatState
                .GetOpponentsOf(player.Creature)
                .Where(creature => creature.IsHittable)
                .OrderByDescending(creature => creature.CurrentHp)
                .ThenBy(creature => creature.CombatId)
                .FirstOrDefault();
            if (card == null || target == null)
            {
                return;
            }

            bool restoreUnplayable =
                card.Keywords.Contains(CardKeyword.Unplayable);
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

    private async Task ConsumeReservedRollModifierPowers(
        PlayerChoiceContext choiceContext,
        CardModel cardSource)
    {
        Creature creature = Controller?.Creature;
        if (creature == null)
        {
            return;
        }

        StrengthenPower strengthen =
            creature.GetPower<StrengthenPower>();
        if (strengthen != null)
        {
            await strengthen.ConsumeReservedRolls(
                choiceContext,
                cardSource);
        }

        ExtremeDodgeRollPenaltyPower rollPenalty =
            creature.GetPower<ExtremeDodgeRollPenaltyPower>();
        if (rollPenalty != null)
        {
            await rollPenalty.ConsumeReservedRolls(
                choiceContext,
                cardSource);
        }
    }

    private bool IsCurrentCardValue(CardModel cardSource)
    {
        return _isCardRollActive &&
               _hasRolledForCurrentPlay &&
               ReferenceEquals(cardSource, Owner) &&
               Owner.Type is CardType.Attack or CardType.Skill;
    }

    private static bool ShouldIgnorePowerAmountBonus(
        PowerModel power,
        decimal amount)
    {
        return power is SwordheartPower ||
               power.GetTypeForAmount(amount) == PowerType.Debuff;
    }

    private void BeginCardRollState()
    {
        AssertMutable();
        if (_isCardRollActive)
        {
            _suspendedRollStates.Push(new SuspendedRollState(
                _isCardRollActive,
                _hasRolledForCurrentPlay,
                _currentRoll,
                _currentEnergySpent,
                _currentRegularSwordheartAmount,
                _currentValueMultiplier,
                _pendingSwordheartAmount,
                _pendingFragileTriggers));
        }

        ResetCurrentRollState();
        _isCardRollActive = true;
    }

    private void FinishCardRollState()
    {
        AssertMutable();
        ResetCurrentRollState();
        if (_suspendedRollStates.Count == 0)
        {
            return;
        }

        SuspendedRollState state = _suspendedRollStates.Pop();
        _isCardRollActive = state.IsCardRollActive;
        _hasRolledForCurrentPlay = state.HasRolledForCurrentPlay;
        _currentRoll = state.CurrentRoll;
        _currentEnergySpent = state.CurrentEnergySpent;
        _currentRegularSwordheartAmount =
            state.CurrentRegularSwordheartAmount;
        _currentValueMultiplier = state.CurrentValueMultiplier;
        _pendingSwordheartAmount = state.PendingSwordheartAmount;
        _pendingFragileTriggers = state.PendingFragileTriggers;
    }

    private void ResetAllTransientState()
    {
        AssertMutable();
        ResetCurrentRollState();
        _lastCompletedEnergySpent = 0;
        _lastCompletedRegularSwordheartAmount = 0;
        _suspendedRollStates.Clear();
    }

    private void ResetCurrentRollState()
    {
        _isCardRollActive = false;
        _hasRolledForCurrentPlay = false;
        _currentRoll = 0;
        _currentEnergySpent = 0;
        _currentRegularSwordheartAmount = 0;
        _currentValueMultiplier = 1m;
        _pendingSwordheartAmount = 0;
        _pendingFragileTriggers = 0;
    }

    private static string GetCardTitle(CardModel card)
    {
        return string.IsNullOrWhiteSpace(card?.Title)
            ? "未知卡牌"
            : card.Title;
    }

    private readonly struct SoulRollResult
    {
        internal SoulRollBreakdown WinningRoll { get; }

        internal IReadOnlyList<SoulRollBreakdown> DiscardedRolls { get; }

        internal int Roll => WinningRoll.FinalRoll;

        internal SoulRollResult(
            SoulRollBreakdown winningRoll,
            IReadOnlyList<SoulRollBreakdown> discardedRolls)
        {
            WinningRoll =
                winningRoll ?? SoulRollBreakdown.FromFinalRoll(0);
            DiscardedRolls =
                discardedRolls ?? Array.Empty<SoulRollBreakdown>();
        }
    }

    private readonly struct SuspendedRollState
    {
        internal bool IsCardRollActive { get; }

        internal bool HasRolledForCurrentPlay { get; }

        internal int CurrentRoll { get; }

        internal int CurrentEnergySpent { get; }

        internal int CurrentRegularSwordheartAmount { get; }

        internal decimal CurrentValueMultiplier { get; }

        internal int PendingSwordheartAmount { get; }

        internal int PendingFragileTriggers { get; }

        internal SuspendedRollState(
            bool isCardRollActive,
            bool hasRolledForCurrentPlay,
            int currentRoll,
            int currentEnergySpent,
            int currentRegularSwordheartAmount,
            decimal currentValueMultiplier,
            int pendingSwordheartAmount,
            int pendingFragileTriggers)
        {
            IsCardRollActive = isCardRollActive;
            HasRolledForCurrentPlay = hasRolledForCurrentPlay;
            CurrentRoll = currentRoll;
            CurrentEnergySpent = currentEnergySpent;
            CurrentRegularSwordheartAmount =
                currentRegularSwordheartAmount;
            CurrentValueMultiplier = currentValueMultiplier;
            PendingSwordheartAmount = pendingSwordheartAmount;
            PendingFragileTriggers = pendingFragileTriggers;
        }
    }
}
