using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Models.Characters;
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

    private static readonly string AbsoluteCriticalSuccessKeywordId =
        ModContentRegistry.GetQualifiedKeywordId(ModId, "ABSOLUTE_CRITICAL_SUCCESS");

    private bool _isCardRollActive;
    private bool _hasRolledForCurrentPlay;
    private int _damageSegmentsStarted;
    private int _currentRoll;
    private SoulRollBreakdown _currentRollDetails;
    private int _currentEnergySpent;
    private int _currentRegularSwordheartAmount;
    private int _lastCompletedEnergySpent;
    private int _lastCompletedRegularSwordheartAmount;
    private decimal _currentValueMultiplier = 1m;
    private int _pendingSwordheartAmount;
    private int _pendingFragileTriggers;
    private Stack<SuspendedRollState> _suspendedRollStates = new();
    // Standalone cannon dice reuse the owner pipeline without creating a card
    // or claiming any card-play resources, attributes or one-card bonuses.
    private Player _standalonePlayer;
    private CardType? _standaloneType;

    /// <summary>
    /// The current result used by the owning card. Multi-hit damage advances
    /// this value once per actual damage segment.
    /// </summary>
    public int CurrentRoll => _currentRoll;

    internal bool LastExtraRollWasCritical { get; private set; }

    internal static (int FlatBonus, int PercentBonus) GetProjectedAttackRollModifiers(Player player) =>
        GetProjectedAttackRollModifiers(player, null);

    internal static (int FlatBonus, int PercentBonus) GetProjectedAttackRollModifiers(
        Player player, CardModel card)
    {
        Creature creature = player?.Creature;
        if (creature == null)
        {
            return (0, 0);
        }

        long bonus = 0;
        long percent = HalfBodyHandHelper.GetRollPenalty(player) +
            TourGuidePower.GetRollPenalty(creature);
        percent += NararaNullificationPenaltyHelper.GetRollPenalty(card, player);

        StrengthenPower strengthen =
            creature.GetPower<StrengthenPower>();
        if (strengthen?.Amount > 0)
        {
            percent += strengthen.RollBonus;
        }

        if (creature.GetPower<ExtremeDodgeRollPenaltyPower>()?.Amount > 0)
        {
            percent += ExtremeDodgeRollPenaltyPower.RollPenalty;
        }

        percent += creature
            .GetPower<ProtagonistPrivilegePower>()?
            .RollPenalty ?? 0;
        percent += creature.GetPower<AccelerationPower>()?.Amount ?? 0;
        percent += LostStarSongSupportPower.GetRollBonus(creature);
        percent += HotaruCannonSupportUpgradeHelper.GetRollPercentBonus(player, CardType.Attack);
        bonus += PhantomHelper.GetSummonerRollBonus(player);

        if (creature.HasPower<MagicSwordModePower>())
        {
            bonus += MagicSwordModePower.RollBonus;
        }

        bonus += TeamCombatHelper
            .GetLivingPlayerCreatures(creature)
            .Where(teammate => teammate != creature)
            .Where(teammate =>
                teammate.HasPower<MagicSwordModePower>())
            .Sum(teammate => teammate
                .GetPower<MagicSwordSharingPower>()?
                .Amount ?? 0);

        if (creature.GetPower<NotOverYetPower>() is { } notOverYet)
        {
            percent += notOverYet.AttackRollBonus;
        }

        if (creature.GetPower<MaidCultivationPower>()?.Amount > 0)
        {
            percent += MaidCultivationPower.RollBonus;
        }

        percent += creature.GetPower<NextRollBonusPower>()?.Amount ?? 0;
        bonus += (long)decimal.Floor(creature.GetPower<SixSenseCombatPower>()?.CombatRollBonus ?? 0m);
        return (
            (int)Math.Clamp(bonus, int.MinValue, int.MaxValue),
            (int)Math.Clamp(percent, int.MinValue, int.MaxValue));
    }

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
        yield return CreateHoverTip(
            AbsoluteCriticalSuccessKeywordId,
            CreateAbsoluteCriticalSuccessDescription(card));
        if (NararaNullificationPenaltyHelper.IsPenalized(card))
        {
            yield return new HoverTip(
                new LocString("powers", "NARARA_NULLIFICATION_POWER.title"),
                HbrgoSakiEnergyIconHelper.Add31OPrefix(
                    new LocString("powers", "NARARA_NULLIFICATION_POWER.cardPenaltyDescription")))
            {
                Id = "NARARA_NULLIFICATION_CARD_PENALTY",
                IsDebuff = true
            };
        }
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = Owner;
        if (card == null ||
            !ReferenceEquals(cardPlay?.Card, card) ||
            !HasEffectiveRollKeyword(card))
        {
            return;
        }

        // Reserve next-card bonuses before rolling regardless of capability dispatch order.
        if (card.Capability<SixSenseCardCapability>() is { } sixSense)
            await sixSense.PreparePlay(cardPlay);
        BeginCardRollState();
        _currentEnergySpent = Math.Max(
            0,
            cardPlay.Resources.EnergySpent);
        PrepareCardRoll(card, includeMagicSwordBonus: true);
        await ResolveHotaruCriticalRewards(new BlockingPlayerChoiceContext());
    }

    public decimal ModifyDamageMultiplicative(
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
            cardSource is DoublePowersHbrgoSaki ||
            TemporaryStatRollScope.IsInternalApplication(power, giver, amount, target, cardSource) ||
            (cardSource is HerSongIsMyScreamHbrgoSaki &&
                power is MegaCrit.Sts2.Core.Models.Powers.DexterityPower or ScreamBlockBoostPower) ||
            (cardSource is MatchaParfaitHbrgoSaki && power is
                MegaCrit.Sts2.Core.Models.Powers.StrengthPower or
                MegaCrit.Sts2.Core.Models.Powers.DexterityPower or MatchaDeferredDpRecoveryPower) ||
            power is ItsMygoProtectionPower ||
            power is LostStarSongSupportPower ||
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
            await ResolveHotaruCriticalRewards(choiceContext);
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
    /// Ensures the current play has resolved its initial Roll result.
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

    public SoulRollBreakdown GetCurrentRollDetailsFor(CardModel cardSource)
    {
        return ReferenceEquals(cardSource, Owner) && _isCardRollActive
            ? _currentRollDetails
            : null;
    }

    internal void PrepareDamageSegmentRoll(
        CardModel cardSource,
        ValueProp props,
        Creature dealer)
    {
        if (!ReferenceEquals(cardSource, Owner) ||
            !_isCardRollActive ||
            !HasEffectiveRollKeyword(cardSource) ||
            !props.IsPoweredAttack() ||
            dealer != Controller?.Creature)
        {
            return;
        }

        if (!_hasRolledForCurrentPlay)
        {
            PrepareCardRoll(cardSource, includeMagicSwordBonus: true);
        }
        else if (_damageSegmentsStarted > 0)
        {
            PrepareAdditionalDamageSegmentRoll(cardSource);
        }

        _damageSegmentsStarted++;
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
        CardModel cardSource,
        bool forceAdvantage = false)
    {
        LastExtraRollWasCritical = false;
        if (!ReferenceEquals(cardSource, Owner) ||
            !HasEffectiveRollKeyword(cardSource))
        {
            return 0;
        }

        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus: true,
            cardSource,
            allowMaidBonus: false,
            advantageOnly: true,
            forceAdvantage);
        int roll = rollResult.Roll;
        bool wasCritical = RollKeywordRules.IsCriticalRoll(Controller, rollResult.WinningRoll);
        RecordRoll(
            rollResult,
            $"「还没有结束」额外掷骰，由「{GetCardTitle(cardSource)}」触发");
        RegisterResolvedRoll(rollResult.WinningRoll);
        await ResolveHotaruCriticalRewards(choiceContext);

        int swordheartAmount = ReserveExtraRollSwordheartAmount(
            cardSource,
            rollResult.WinningRoll);
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
        // Reentrant card effects may roll again or change Charge. Publish the
        // completed outer roll's original result only after those effects finish.
        LastExtraRollWasCritical = wasCritical;
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
            await ResolveHotaruCriticalRewards(choiceContext);
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

    private Player Controller => _standalonePlayer ?? Owner?.Owner;

    internal static async Task<(SoulRollBreakdown Winning, IReadOnlyList<SoulRollBreakdown> Discarded)>
        RollForCannon(PlayerChoiceContext context, Player player, CardType type)
    {
        StrengthenPower strengthen = player.Creature.GetPower<StrengthenPower>();
        ExtremeDodgeRollPenaltyPower penalty = player.Creature.GetPower<ExtremeDodgeRollPenaltyPower>();
        NextCriticalSuccessPower critical = player.Creature.GetPower<NextCriticalSuccessPower>();
        int oldStrengthen = strengthen?.ReservedRollCount ?? 0;
        int oldPenalty = penalty?.ReservedRollCount ?? 0;
        int oldCritical = critical?.ReservedRollCount ?? 0;
        RollGlossaryCapability pipeline = CreateCannonPipeline(player, type);
        SoulRollResult result = pipeline.RollD100(true, null, allowMaidBonus: false, advantageOnly: false);
        int newStrengthen = (strengthen?.ReservedRollCount ?? 0) - oldStrengthen;
        int newPenalty = (penalty?.ReservedRollCount ?? 0) - oldPenalty;
        int newCritical = (critical?.ReservedRollCount ?? 0) - oldCritical;
        if (newStrengthen > 0) await strengthen.ConsumeReservedRolls(context, null, newStrengthen);
        if (newPenalty > 0) await penalty.ConsumeReservedRolls(context, null, newPenalty);
        if (newCritical > 0) await critical.ConsumeReservedRolls(context, null, newCritical);
        return (result.WinningRoll, result.DiscardedRolls);
    }

    internal static void CommitCannonRoll(Player player, SoulRollBreakdown winning,
        IReadOnlyList<SoulRollBreakdown> discarded)
    {
        RollGlossaryCapability pipeline = CreateCannonPipeline(player);
        pipeline.RecordRoll(new SoulRollResult(winning, discarded), "浮游炮");
        pipeline.RegisterResolvedRoll(winning);
    }

    private static RollGlossaryCapability CreateCannonPipeline(
        Player player, CardType? type = null)
    {
        // Capabilities are canonical models registered with Ritsu. Creating a
        // second instance through their constructor trips ModelDb's duplicate
        // guard; the registry supplies a fresh mutable clone with isolated state.
        RollGlossaryCapability pipeline =
            ModelCapabilityRegistry.Create<RollGlossaryCapability>();
        pipeline._standalonePlayer = player;
        pipeline._standaloneType = type;
        return pipeline;
    }
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
        description.Add("absoluteThreshold", RollKeywordRules.GetAbsoluteCriticalMinimum(card.IsMutable ? card.Owner : null));
        description.Add("criticalThreshold", RollKeywordRules.GetCriticalMinimum(card.IsMutable ? card.Owner : null));
        return description;
    }

    private static LocString CreateRollCardText()
    {
        return new LocString(
            LocTable,
            $"{RollKeywordId}.cardText");
    }

    internal static string GetRollCardText()
    {
        return CreateRollCardText().GetFormattedText();
    }

    private static LocString CreateCriticalSuccessDescription(
        CardModel card)
    {
        LocString text = new(
            LocTable,
            RollKeywordRules.UsesHotaruRules(card)
                ? $"{CriticalSuccessKeywordId}.hotaruDescription"
                : RollKeywordRules.UsesSakiRules(card)
                ? $"{CriticalSuccessKeywordId}.sakiDescription"
                : $"{CriticalSuccessKeywordId}.description");
        text.Add("criticalThreshold", RollKeywordRules.GetCriticalMinimum(card?.IsMutable == true ? card.Owner : null));
        return text;
    }

    internal static LocString CreateAbsoluteCriticalSuccessDescription(CardModel card)
    {
        LocString text = new(LocTable, RollKeywordRules.UsesHotaruRules(card)
            ? $"{AbsoluteCriticalSuccessKeywordId}.hotaruDescription"
            : $"{AbsoluteCriticalSuccessKeywordId}.description");
        text.Add("threshold", RollKeywordRules.GetAbsoluteCriticalMinimum(card?.IsMutable == true ? card.Owner : null));
        return text;
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
        _currentRollDetails = rollResult.WinningRoll;
        _currentValueMultiplier = card.Type is
            CardType.Attack or CardType.Skill
                ? RollKeywordRules.GetAttackOrSkillValueMultiplier(
                    Controller,
                    _currentRollDetails,
                    card.Type)
                : 1m;

        RecordRoll(
            rollResult,
            $"打出「{GetCardTitle(card)}」");
        RegisterResolvedRoll(_currentRollDetails);
        QueueCardSwordheart(_currentRollDetails, card);
        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }
    }

    private void PrepareAdditionalDamageSegmentRoll(CardModel card)
    {
        SoulRollResult rollResult = RollD100(
            includeMagicSwordBonus: true,
            card,
            allowMaidBonus: card.Type == CardType.Attack,
            advantageOnly: false);
        int roll = rollResult.Roll;

        AssertMutable();
        _currentRoll = roll;
        _currentRollDetails = rollResult.WinningRoll;
        _currentValueMultiplier = card.Type is
            CardType.Attack or CardType.Skill
                ? RollKeywordRules.GetAttackOrSkillValueMultiplier(
                    Controller,
                    _currentRollDetails,
                    card.Type)
                : 1m;

        RecordRoll(
            rollResult,
            $"「{GetCardTitle(card)}」第{_damageSegmentsStarted + 1}段伤害");
        RegisterResolvedRoll(_currentRollDetails);
        QueueCardSwordheart(_currentRollDetails, card);
        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }
    }

    private SoulRollResult RollD100(
        bool includeMagicSwordBonus,
        CardModel cardSource,
        bool allowMaidBonus,
        bool advantageOnly,
        bool forceAdvantage = false)
    {
        Player player = Controller;
        if (player?.RunState == null)
        {
            return new SoulRollResult(
                RollKeywordRules.ResolveRollTier(player, SoulRollBreakdown.FromFinalRoll(0)),
                Array.Empty<SoulRollBreakdown>());
        }

        NextCriticalSuccessPower nextCriticalSuccess =
            player.Creature.GetPower<NextCriticalSuccessPower>();
        if (nextCriticalSuccess?.TryReserveRoll() == true)
        {
            return new SoulRollResult(
                RollKeywordRules.ResolveRollTier(player, SoulRollBreakdown.FromFinalRoll(
                    RollKeywordRules.RollMaximum,
                    "不应如此：必定大成功")),
                Array.Empty<SoulRollBreakdown>());
        }

        StrengthenPower strengthen =
            player.Creature.GetPower<StrengthenPower>();
        bool useStrengthen = strengthen?.TryReserveRoll() == true;
        ExtremeDodgeRollPenaltyPower rollPenalty = advantageOnly
            ? null
            : player.Creature
                .GetPower<ExtremeDodgeRollPenaltyPower>();
        bool useRollPenalty = rollPenalty?.TryReserveRoll() == true;

        if (RollKeywordCombatState.RollsAreFixedToOneHundred(player))
        {
            return new SoulRollResult(
                RollKeywordRules.ResolveRollTier(player, SoulRollBreakdown.FromFinalRoll(
                    RollKeywordRules.RollMaximum,
                    "控制台指令：固定出目")),
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

        if (forceAdvantage)
        {
            rolls.Add(RollD100Die(
                "优势骰：还没有结束",
                advantageOnly));
        }

        CardType? rollType = _standaloneType ?? cardSource?.Type;
        bool isAttackCardRoll = !advantageOnly && rollType == CardType.Attack;
        List<SoulRollBonusDetail> sharedBonuses = new();

        int cannonUpgradeBonus = HotaruCannonSupportUpgradeHelper.GetRollPercentBonus(player, rollType);
        if (!advantageOnly && cannonUpgradeBonus != 0)
            sharedBonuses.Add(new SoulRollBonusDetail(cannonUpgradeBonus, "浮游炮升级", isPercentage: true));

        int highSongBonus = LostStarSongSupportPower.GetRollBonus(player.Creature);
        if (highSongBonus != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                highSongBonus, "迷途之星的歌", isPercentage: true));
        }

        int halfBodyPenalty = HalfBodyHandHelper.GetRollPenalty(player);
        if (halfBodyPenalty != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                halfBodyPenalty, "半身", isPercentage: true));
        }

        int sixSenseBonus = SixSenseCardCapability.GetRollBonus(cardSource);
        if (sixSenseBonus > 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(sixSenseBonus, "六感属性"));
        }

        int sixSenseEffectBonus = _standalonePlayer != null
            ? (int)Math.Min(int.MaxValue, decimal.Floor(player.Creature.GetPower<SixSenseCombatPower>()?.CombatRollBonus ?? 0m))
            : SixSenseCardCapability.GetTriggeredRollBonus(cardSource);
        if (!advantageOnly && sixSenseEffectBonus > 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(sixSenseEffectBonus, "六感效果"));
        }

        int tourGuidePenalty = TourGuidePower.GetRollPenalty(player.Creature);
        if (tourGuidePenalty != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                tourGuidePenalty,
                "导游之力", isPercentage: true));
        }

        int nullificationPenalty = NararaNullificationPenaltyHelper.GetRollPenalty(cardSource, player);
        if (nullificationPenalty != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                nullificationPenalty, "无效化", isPercentage: true));
        }

        if (useStrengthen && !advantageOnly)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                strengthen.RollBonus,
                "强化", isPercentage: true));
        }

        if (useRollPenalty)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                ExtremeDodgeRollPenaltyPower.RollPenalty,
                "极限闪避", isPercentage: true));
        }

        if (!advantageOnly && protagonistPrivilege != null)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                protagonistPrivilege.RollPenalty,
                "主人公特典", isPercentage: true));
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
                    "加速", isPercentage: true));
            }

            int phantomBonus = PhantomHelper.GetSummonerRollBonus(player);
            if (phantomBonus > 0)
            {
                sharedBonuses.Add(new SoulRollBonusDetail(phantomBonus, "幻影升级"));
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

        if (!advantageOnly)
        {
            int sharingBonus = TeamCombatHelper
                .GetLivingPlayerCreatures(player.Creature)
                .Where(creature => creature != player.Creature)
                .Where(creature =>
                    creature.HasPower<MagicSwordModePower>())
                .Sum(creature => creature
                    .GetPower<MagicSwordSharingPower>()
                    ?.Amount ?? 0);
            if (sharingBonus != 0)
            {
                sharedBonuses.Add(new SoulRollBonusDetail(
                    sharingBonus,
                    "魔剑共享"));
            }
        }

        if (isAttackCardRoll &&
            player.Creature.GetPower<NotOverYetPower>() is { } notOverYet)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                notOverYet.AttackRollBonus,
                "还没有结束", isPercentage: true));
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
                    "女仆的素养", isPercentage: true));
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
                    "渐入佳境", isPercentage: true));
            }
        }

        List<SoulRollBreakdown> finalRolls = rolls
            .Select(roll => RollModifierHelper.Apply(
                roll,
                sharedBonuses))
            .Select(roll => RollKeywordRules.ResolveRollTier(player, roll))
            .ToList();
        int winningIndex = GetWinningRollIndex(finalRolls);
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
        bool isForcedCritical = false;
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
            isForcedCritical = true;
        }

        return new SoulRollBreakdown(
            baseRoll,
            finalRoll,
            diceSource,
            bonuses)
        {
            IsForcedCritical = isForcedCritical
        };
    }

    private int GetWinningRollIndex(IReadOnlyList<SoulRollBreakdown> rolls)
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
                rolls[index].FinalRoll > rolls[winningIndex].FinalRoll)
            {
                winningIndex = index;
                hasCritical = isCritical;
            }
        }

        return winningIndex;
    }

    private void RegisterResolvedRoll(SoulRollBreakdown roll)
    {
        HbrgoSakiPresentationBridge.NotifyRollResolved(Controller, roll.FinalRoll);

        if (!RollKeywordRules.IsCriticalRoll(Controller, roll))
        {
            return;
        }

        RollKeywordCombatState.RecordCriticalSuccess(Controller);
        Controller?.GetRelic<ElectronicSoldierManual>()
            ?.QueueSixSenseReward();
        Controller?.GetRelic<Millsage>()?.QueueCriticalCannonReward();
    }

    private Task ResolveHotaruCriticalRewards(PlayerChoiceContext context) =>
        Controller?.GetRelic<Millsage>()?.ResolveCriticalCannonRewards(context) ?? Task.CompletedTask;

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

    private void QueueCardSwordheart(SoulRollBreakdown roll, CardModel card)
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
        SoulRollBreakdown roll)
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
                _currentRollDetails);
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
        if (Controller?.Character is not HbrgoSaki31O ||
            creature == null ||
            requestedAmount <= 0)
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
                // Include this card's after-play rewards and modifier cleanup,
                // while source-specific waits keep other triggered cards intact.
                using IDisposable animationScope =
                    FragileAutoPlayAnimationHelper.Suppress(card);
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

        NextCriticalSuccessPower nextCriticalSuccess =
            creature.GetPower<NextCriticalSuccessPower>();
        if (nextCriticalSuccess != null)
        {
            await nextCriticalSuccess.ConsumeReservedRolls(
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
               power is BeyondProtectionPower ||
               power is PhantomNextAttackDoublePower ||
               power is PhantomNextAttackBonusSegmentsPower ||
               power is MyLegendNextAttackBasePower ||
               power is PhantomAttackToBlockPower ||
               power is PhantomDeferredBlockPower ||
               power is LingeringLifePower ||
               power is PhantomTemporaryLevelPower ||
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
                _damageSegmentsStarted,
                _currentRoll,
                _currentRollDetails,
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
        _damageSegmentsStarted = state.DamageSegmentsStarted;
        _currentRoll = state.CurrentRoll;
        _currentRollDetails = state.CurrentRollDetails;
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
        LastExtraRollWasCritical = false;
        _isCardRollActive = false;
        _hasRolledForCurrentPlay = false;
        _damageSegmentsStarted = 0;
        _currentRoll = 0;
        _currentRollDetails = null;
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

        internal int DamageSegmentsStarted { get; }

        internal int CurrentRoll { get; }

        internal SoulRollBreakdown CurrentRollDetails { get; }

        internal int CurrentEnergySpent { get; }

        internal int CurrentRegularSwordheartAmount { get; }

        internal decimal CurrentValueMultiplier { get; }

        internal int PendingSwordheartAmount { get; }

        internal int PendingFragileTriggers { get; }

        internal SuspendedRollState(
            bool isCardRollActive,
            bool hasRolledForCurrentPlay,
            int damageSegmentsStarted,
            int currentRoll,
            SoulRollBreakdown currentRollDetails,
            int currentEnergySpent,
            int currentRegularSwordheartAmount,
            decimal currentValueMultiplier,
            int pendingSwordheartAmount,
            int pendingFragileTriggers)
        {
            IsCardRollActive = isCardRollActive;
            HasRolledForCurrentPlay = hasRolledForCurrentPlay;
            DamageSegmentsStarted = damageSegmentsStarted;
            CurrentRoll = currentRoll;
            CurrentRollDetails = currentRollDetails;
            CurrentEnergySpent = currentEnergySpent;
            CurrentRegularSwordheartAmount =
                currentRegularSwordheartAmount;
            CurrentValueMultiplier = currentValueMultiplier;
            PendingSwordheartAmount = pendingSwordheartAmount;
            PendingFragileTriggers = pendingFragileTriggers;
        }
    }
}
