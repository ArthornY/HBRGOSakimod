using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.FateDice;
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
    // 镜面 doubles only the card's damage and block, not the power amounts it
    // gives (design v3: 「这张牌的伤害和格挡翻倍」), so it is kept apart.
    private decimal _currentMirrorMultiplier = 1m;
    private int _pendingSwordheartAmount;
    private int _pendingFragileTriggers;
    private HbrFateDicePendingEffects _pendingFateEffects;
    private List<RollPowerReservation> _pendingRollPowerReservations = new();
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
        percent += HbrRollPowerHelper.GetAvailableBonus(creature);

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
        percent += HotaruRareRollHelper.GetRollPercent(player);
        percent += player.GetRelic<HotaruIridescence>()?.RollBonus ?? 0;
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

        percent += creature.GetPower<NextRollBonusPower>()?.Amount ?? 0;
        bonus += HotaruEchoLocationPower.GetProjectedBonus(player);
        bonus += HotaruLingeringEchoPower.GetProjectedBonus(player);
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
            _currentValueMultiplier * _currentMirrorMultiplier,
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

        return _currentValueMultiplier * _currentMirrorMultiplier;
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
            await PlayPendingFateEffects(choiceContext);
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
    public Task<int> RollExtraD100(
        PlayerChoiceContext choiceContext,
        CardModel cardSource,
        bool forceAdvantage = false) =>
        RollExtraD100Core(choiceContext, cardSource, forceAdvantage, advantageOnly: true, "额外掷骰");

    // New card effects request a fresh ordinary die, including the normal
    // additive modifiers. The original attack's published roll is untouched.
    public Task<int> RollAdditionalD100(PlayerChoiceContext choiceContext, CardModel cardSource) =>
        RollExtraD100Core(choiceContext, cardSource, forceAdvantage: false, advantageOnly: false, "追加掷骰");

    private async Task<int> RollExtraD100Core(PlayerChoiceContext choiceContext, CardModel cardSource,
        bool forceAdvantage, bool advantageOnly, string label)
    {
        LastExtraRollWasCritical = false;
        if (!ReferenceEquals(cardSource, Owner) ||
            !HasEffectiveRollKeyword(cardSource))
        {
            return 0;
        }

        List<RollPowerReservation> reservations = _pendingRollPowerReservations ??= new();
        int reservationStart = reservations.Count;
        Creature holder = Controller?.Creature;
        StrengthenPower strengthen = !advantageOnly ? holder?.GetPower<StrengthenPower>() : null;
        ExtremeDodgeRollPenaltyPower penalty = !advantageOnly ? holder?.GetPower<ExtremeDodgeRollPenaltyPower>() : null;
        NextCriticalSuccessPower critical = !advantageOnly ? holder?.GetPower<NextCriticalSuccessPower>() : null;
        int oldStrengthen = strengthen?.ReservedRollCount ?? 0;
        int oldPenalty = penalty?.ReservedRollCount ?? 0;
        int oldCritical = critical?.ReservedRollCount ?? 0;
        ExceptionDispatchInfo failure = null;
        int completedRoll = 0;
        try
        {
            SoulRollResult rollResult = RollD100(
                includeMagicSwordBonus: true,
                cardSource,
                allowMaidBonus: false,
                advantageOnly,
                forceAdvantage);
            int roll = rollResult.Roll;
            bool wasCritical = RollKeywordRules.IsCriticalRoll(Controller, rollResult.WinningRoll);
            RecordRoll(
                rollResult,
                $"{label}，由「{GetCardTitle(cardSource)}」触发");
            RegisterResolvedRoll(rollResult.WinningRoll);
            await ResolveHotaruCriticalRewards(choiceContext);

            int swordheartAmount = ReserveExtraRollSwordheartAmount(cardSource, rollResult.WinningRoll);
            if (swordheartAmount > 0)
                await ApplySwordheart(choiceContext, cardSource, swordheartAmount);
            if (roll > 95) _pendingFragileTriggers++;

            _pendingFateEffects += HbrFateDiceRollHelper.EffectsOf(Controller, rollResult.WinningRoll.BaseRoll, wasCritical);
            await PlayPendingFragileCards(choiceContext);
            await PlayPendingFateEffects(choiceContext);
            if (advantageOnly) await ConsumeReservedRollModifierPowers(choiceContext, cardSource);
            // Reentrant card effects may roll again or change Charge. Publish
            // the completed outer result after those effects finish.
            LastExtraRollWasCritical = wasCritical;
            completedRoll = roll;
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            if (!advantageOnly)
                failure = await CaptureRollCleanupFailure(failure, async () =>
                {
                    int count = (strengthen?.ReservedRollCount ?? 0) - oldStrengthen;
                    if (count > 0) await strengthen.ConsumeReservedRolls(choiceContext, cardSource, count);
                    count = (penalty?.ReservedRollCount ?? 0) - oldPenalty;
                    if (count > 0) await penalty.ConsumeReservedRolls(choiceContext, cardSource, count);
                    count = (critical?.ReservedRollCount ?? 0) - oldCritical;
                    if (count > 0) await critical.ConsumeReservedRolls(choiceContext, cardSource, count);
                });
            // Normal completion may already have drained these tokens. Their
            // idempotent consumption also protects this roll's failure path,
            // without touching tokens that belonged to its enclosing card.
            failure = await CaptureRollCleanupFailure(failure, () =>
                ConsumeNewRollReservations(choiceContext, reservations, reservationStart, cardSource));
        }
        failure?.Throw();
        return completedRoll;
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
            await PlayPendingFateEffects(choiceContext);
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

    internal static Task<(SoulRollBreakdown Winning, IReadOnlyList<SoulRollBreakdown> Discarded)>
        RollForCannon(PlayerChoiceContext context, Player player, CardType type, bool forceAdvantage = false,
            SoulRollBreakdown inheritedRoll = null) => RollForCannonCore(context, player, type, forceAdvantage, inheritedRoll, false);

    internal static Task<(SoulRollBreakdown Winning, IReadOnlyList<SoulRollBreakdown> Discarded)>
        RollForContest(PlayerChoiceContext context, Player player, CardType type) =>
        RollForCannonCore(context, player, type, false, null, true);

    private static async Task<(SoulRollBreakdown Winning, IReadOnlyList<SoulRollBreakdown> Discarded)>
        RollForCannonCore(PlayerChoiceContext context, Player player, CardType type, bool forceAdvantage,
            SoulRollBreakdown inheritedRoll, bool comparisonOnly)
    {
        StrengthenPower strengthen = player.Creature.GetPower<StrengthenPower>();
        ExtremeDodgeRollPenaltyPower penalty = player.Creature.GetPower<ExtremeDodgeRollPenaltyPower>();
        NextCriticalSuccessPower critical = player.Creature.GetPower<NextCriticalSuccessPower>();
        int oldStrengthen = strengthen?.ReservedRollCount ?? 0;
        int oldPenalty = penalty?.ReservedRollCount ?? 0;
        int oldCritical = critical?.ReservedRollCount ?? 0;
        RollGlossaryCapability pipeline = CreateCannonPipeline(player, type);
        SoulRollResult result = default;
        ExceptionDispatchInfo failure = null;
        try
        {
            result = pipeline.RollD100Core(true, null, allowMaidBonus: false, advantageOnly: false,
                forceAdvantage: forceAdvantage, inheritedRoll: inheritedRoll, comparisonOnly: comparisonOnly);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            failure = await CaptureRollCleanupFailure(failure, async () =>
            {
                int newStrengthen = (strengthen?.ReservedRollCount ?? 0) - oldStrengthen;
                int newPenalty = (penalty?.ReservedRollCount ?? 0) - oldPenalty;
                int newCritical = (critical?.ReservedRollCount ?? 0) - oldCritical;
                if (newStrengthen > 0) await strengthen.ConsumeReservedRolls(context, null, newStrengthen);
                if (newPenalty > 0) await penalty.ConsumeReservedRolls(context, null, newPenalty);
                if (newCritical > 0) await critical.ConsumeReservedRolls(context, null, newCritical);
            });
            failure = await CaptureRollCleanupFailure(failure, () =>
                ConsumeNewRollReservations(context, pipeline._pendingRollPowerReservations, 0, null));
        }
        failure?.Throw();
        return (result.WinningRoll, result.DiscardedRolls);
    }

    private static async Task ConsumeNewRollReservations(PlayerChoiceContext context,
        List<RollPowerReservation> reservations, int start, CardModel source)
    {
        if (reservations == null || reservations.Count <= start) return;
        RollPowerReservation[] added = reservations.Skip(start).ToArray();
        reservations.RemoveRange(start, reservations.Count - start);
        await HbrRollPowerHelper.ConsumeReservations(context, added, source);
    }

    private static async Task<ExceptionDispatchInfo> CaptureRollCleanupFailure(
        ExceptionDispatchInfo failure, Func<Task> cleanup)
    {
        try { await cleanup(); }
        catch (Exception error)
        {
            if (failure == null) return ExceptionDispatchInfo.Capture(error);
            // Keep the actual roll/card failure and its original stack. A
            // secondary cleanup failure is diagnostic data, never its replacement.
            try { failure.SourceException.Data["HBRGOSakiMod.RollCleanupFailure"] = error; }
            catch { }
        }
        return failure;
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
        return HoverTipCompatibility.CreateKeyword(
            ModKeywordRegistry.GetTitle(keywordId),
            description, keywordId);
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
        HotaruUncommonCardHelper.RecordResolvedRoll(card, roll);
        HotaruRareRollHelper.RecordResolvedRoll(card, _currentRollDetails);
        SetCurrentRollValueMultipliers(card);

        RecordRoll(
            rollResult,
            $"打出「{GetCardTitle(card)}」");
        RegisterResolvedRoll(_currentRollDetails);
        QueueCardSwordheart(_currentRollDetails, card);
        _pendingFateEffects += HbrFateDiceRollHelper.EffectsOf(Controller, _currentRollDetails.BaseRoll,
            RollKeywordRules.IsCriticalRoll(Controller, _currentRollDetails));
        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }
    }

    // The roll's increase covers the card's damage, block and the power amounts it
    // gives; 镜面's repdigit doubling covers only damage and block.
    private void SetCurrentRollValueMultipliers(CardModel card)
    {
        bool attackOrSkill = card.Type is CardType.Attack or CardType.Skill;
        _currentValueMultiplier = attackOrSkill
            ? RollKeywordRules.GetAttackOrSkillValueMultiplier(
                Controller,
                _currentRollDetails,
                card.Type)
            : 1m;
        _currentMirrorMultiplier = attackOrSkill &&
            HbrFateDiceRollHelper.DoublesValues(Controller, _currentRollDetails.BaseRoll)
                ? 2m
                : 1m;
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
        HotaruUncommonCardHelper.RecordResolvedRoll(card, roll);
        HotaruRareRollHelper.RecordResolvedRoll(card, _currentRollDetails);
        SetCurrentRollValueMultipliers(card);

        RecordRoll(
            rollResult,
            $"「{GetCardTitle(card)}」第{_damageSegmentsStarted + 1}段伤害");
        RegisterResolvedRoll(_currentRollDetails);
        QueueCardSwordheart(_currentRollDetails, card);
        _pendingFateEffects += HbrFateDiceRollHelper.EffectsOf(Controller, _currentRollDetails.BaseRoll,
            RollKeywordRules.IsCriticalRoll(Controller, _currentRollDetails));
        if (roll > 95)
        {
            _pendingFragileTriggers++;
        }
    }

    private SoulRollResult RollD100(bool includeMagicSwordBonus, CardModel cardSource,
        bool allowMaidBonus, bool advantageOnly, bool forceAdvantage = false, SoulRollBreakdown inheritedRoll = null) =>
        RollD100Core(includeMagicSwordBonus, cardSource, allowMaidBonus, advantageOnly, forceAdvantage, inheritedRoll, false);

    private SoulRollResult RollD100Core(
        bool includeMagicSwordBonus,
        CardModel cardSource,
        bool allowMaidBonus,
        bool advantageOnly,
        bool forceAdvantage = false,
        SoulRollBreakdown inheritedRoll = null, bool comparisonOnly = false)
    {
        Player player = Controller;
        if (player?.RunState == null)
        {
            return new SoulRollResult(
                RollKeywordRules.ResolveRollTier(player, SoulRollBreakdown.FromFinalRoll(0)),
                Array.Empty<SoulRollBreakdown>());
        }

        RollPowerReservation countedRoll = HbrRollPowerHelper.Reserve(player.Creature);
        if (countedRoll != null)
            (_pendingRollPowerReservations ??= new()).Add(countedRoll);

        int echoBonus = !advantageOnly
            ? player.Creature.GetPower<HotaruEchoLocationPower>()?.GetRollBonus(cardSource) ?? 0
            : 0;
        int lingeringEchoBonus = !advantageOnly
            ? player.Creature.GetPower<HotaruLingeringEchoPower>()?.GetRollBonus(cardSource) ?? 0
            : 0;
        SoulRollBreakdown AddEchoBonus(SoulRollBreakdown roll)
        {
            if (echoBonus != 0)
                roll = RollModifierHelper.Apply(roll,
                    new[] { new SoulRollBonusDetail(echoBonus, "身体记得") });
            return lingeringEchoBonus == 0 ? roll : RollModifierHelper.Apply(roll,
                new[] { new SoulRollBonusDetail(lingeringEchoBonus, "低处的余响") });
        }

        int thisBeatPercent = HotaruRareRollHelper.GetRollPercent(player);
        SoulRollBreakdown FinishSpecialRoll(SoulRollBreakdown roll, bool applyCountedBonus = true)
        {
            List<SoulRollBonusDetail> bonuses = new();
            if (thisBeatPercent != 0)
                bonuses.Add(new SoulRollBonusDetail(thisBeatPercent, "只为眼前这一拍", isPercentage: true));
            if (applyCountedBonus && countedRoll?.BonusPercent is { } bonus && bonus != 0)
                bonuses.Add(new SoulRollBonusDetail(bonus, "掷骰加成", isPercentage: true));
            roll = RollModifierHelper.Apply(roll, bonuses);
            return RollKeywordRules.ResolveRollTier(player,
                HotaruRareRollHelper.ApplyCertainVoice(cardSource, AddEchoBonus(roll)));
        }

        NextCriticalSuccessPower nextCriticalSuccess =
            player.Creature.GetPower<NextCriticalSuccessPower>();
        if (!comparisonOnly && inheritedRoll == null && nextCriticalSuccess?.TryReserveRoll() == true)
        {
            return new SoulRollResult(
                FinishSpecialRoll(SoulRollBreakdown.FromFinalRoll(
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
        if (inheritedRoll == null && RollKeywordCombatState.RollsAreFixedToOneHundred(player))
        {
            return new SoulRollResult(
                FinishSpecialRoll(SoulRollBreakdown.FromFinalRoll(
                    RollKeywordRules.RollMaximum,
                    "控制台指令：固定出目"), applyCountedBonus: false),
                Array.Empty<SoulRollBreakdown>());
        }

        List<SoulRollBreakdown> rolls = new()
        {
            RollD100Die(inheritedRoll == null ? "基础骰" : "沿用我的答案：继承原始出目",
                advantageOnly, inheritedRoll?.BaseRoll)
        };

        GuideLittleHand guideLittleHand =
            player.GetRelic<GuideLittleHand>();
        bool allowAdvantage = inheritedRoll == null && !HbrFateDiceRollHelper.BlocksAdvantage(player);
        if (allowAdvantage && guideLittleHand != null)
        {
            rolls.Add(RollD100Die(
                "优势骰：导游的小手",
                advantageOnly));
            guideLittleHand.TakeHigherRoll(
                rolls[0].FinalRoll,
                rolls[1].FinalRoll);
        }

        if (allowAdvantage && useStrengthen)
        {
            rolls.Add(RollD100Die(
                "优势骰：强化",
                advantageOnly));
        }

        if (allowAdvantage)
        {
            for (int index = 0; index < (countedRoll?.ExtraDice ?? 0); index++)
                rolls.Add(RollD100Die("优势骰：能力", advantageOnly));
        }

        ProtagonistPrivilegePower protagonistPrivilege =
            player.Creature.GetPower<ProtagonistPrivilegePower>();
        if (allowAdvantage && protagonistPrivilege != null)
        {
            rolls.Add(RollD100Die(
                "优势骰：主人公特典",
                advantageOnly));
        }

        if (allowAdvantage && forceAdvantage)
        {
            rolls.Add(RollD100Die(
                "优势骰：还没有结束",
                advantageOnly));
        }

        if (allowAdvantage && HotaruEndgameEffectsHelper.HasAdvantage(player))
        {
            rolls.Add(RollD100Die("优势骰：救赎／牺牲", advantageOnly));
        }

        // Fate fragments: 主人公特典 always, 先手 on the first roll of each turn; 孤注 takes no
        // advantage die at all (「不能取优势骰」), so only the base die counts.
        bool firstRollOfTurn = HbrFateDiceRollHelper.TakeFirstRollOfTurn(player);
        if (allowAdvantage && HbrFateDiceRollHelper.Has(player, HbrFateDiceCatalog.ProtagonistPrivilege))
        {
            rolls.Add(RollD100Die("优势骰：主人公特典（命运碎片）", advantageOnly));
        }

        if (allowAdvantage && firstRollOfTurn)
        {
            rolls.Add(RollD100Die("优势骰：先手（命运碎片）", advantageOnly));
        }

        if (HbrFateDiceRollHelper.BlocksAdvantage(player) && rolls.Count > 1)
        {
            rolls.RemoveRange(1, rolls.Count - 1);
        }

        CardType? rollType = _standaloneType ?? cardSource?.Type;
        bool isAttackCardRoll = !advantageOnly && rollType == CardType.Attack;
        List<SoulRollBonusDetail> sharedBonuses = new();
        if (countedRoll?.BonusPercent is { } countedBonus && countedBonus != 0)
            sharedBonuses.Add(new SoulRollBonusDetail(countedBonus, "掷骰加成", isPercentage: true));
        if (thisBeatPercent != 0)
            sharedBonuses.Add(new SoulRollBonusDetail(thisBeatPercent, "只为眼前这一拍", isPercentage: true));

        int iridescenceBonus = player.GetRelic<HotaruIridescence>()?.RollBonus ?? 0;
        if (iridescenceBonus != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                iridescenceBonus, "虹光", isPercentage: true));
        }

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

        int equipmentBonus = ElectronicSoldierManual.GetEquipmentRollBonus(player, rollType);
        if (equipmentBonus > 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(equipmentBonus, "饰品"));
        }

        int tourGuidePenalty = TourGuidePower.GetRollPenalty(player.Creature);
        if (tourGuidePenalty != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                tourGuidePenalty,
                "导游之力", isPercentage: true));
        }


        if (useStrengthen && !advantageOnly)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                strengthen.RollBonus,
                "吉兆", isPercentage: true));
        }

        if (useRollPenalty)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                ExtremeDodgeRollPenaltyPower.RollPenalty,
                "间不容发", isPercentage: true));
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

            foreach ((int percent, string source) in HbrFateDiceRollHelper.PercentModifiers(player))
            {
                sharedBonuses.Add(new SoulRollBonusDetail(percent, source, isPercentage: true));
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
                    "祥光"));
            }
        }

        if (isAttackCardRoll &&
            player.Creature.GetPower<NotOverYetPower>() is { } notOverYet)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                notOverYet.AttackRollBonus,
                "还没有结束", isPercentage: true));
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
            .Select(AddEchoBonus)
            .Select(roll => HotaruRareRollHelper.ApplyCertainVoice(cardSource, roll))
            .Select(roll => inheritedRoll == null ? roll : new SoulRollBreakdown(
                roll.BaseRoll, roll.FinalRoll, roll.DiceSource, roll.Bonuses)
            {
                IsForcedCritical = RollKeywordRules.IsCriticalRoll(player, inheritedRoll),
                ResolvedTier = RollKeywordRules.IsCriticalRoll(player, inheritedRoll) ? SoulRollTier.Critical : null
            })
            .Select(roll => RollKeywordRules.ResolveRollTier(player, roll))
            .ToList();
        int winningIndex = comparisonOnly
            ? finalRolls.Select((roll, index) => (roll.FinalRoll, index)).OrderByDescending(x => x.FinalRoll).First().index
            : GetWinningRollIndex(finalRolls);
        SoulRollBreakdown winningRoll = finalRolls[winningIndex];
        SoulRollBreakdown[] discardedRolls = finalRolls
            .Where((_, index) => index != winningIndex)
            .ToArray();

        return new SoulRollResult(winningRoll, discardedRolls);
    }

    private SoulRollBreakdown RollD100Die(
        string diceSource,
        bool advantageOnly,
        int? inheritedRaw = null)
    {
        Player player = Controller;
        int baseRoll = inheritedRaw ?? NextRawRoll(player);
        if (inheritedRaw == null)
        {
            // Fate dice: cracks past a full set lower every raw roll (「每多一道裂痕，原始
            // 出目整局 −3」); 再来 rolls the combat's first low raw roll once more.
            int penalty = HbrFateDiceRollHelper.RawPenalty(player);
            if (penalty > 0)
            {
                int before = baseRoll;
                baseRoll = HbrFateDiceRollHelper.ApplyRawPenalty(player, baseRoll);
                diceSource += $"（裂痕：原始出目{before}−{before - baseRoll}）";
            }

            if (HbrFateDiceRollHelper.TryUseReroll(player, baseRoll))
            {
                diceSource += $"（再来：原始出目{baseRoll}重掷）";
                baseRoll = HbrFateDiceRollHelper.ApplyRawPenalty(player, NextRawRoll(player));
            }
        }

        int finalRoll = baseRoll;
        bool isForcedCritical = false;
        List<SoulRollBonusDetail> bonuses = new();

        // 幸运数字 (moved from the dual swords form to a fragment on 2026-11-09).
        if (!advantageOnly &&
            HbrFateDiceRollHelper.AppliesLuckyNumbers(player, baseRoll))
        {
            int adjustment = RollKeywordRules.RollMaximum - baseRoll;
            bonuses.Add(new SoulRollBonusDetail(
                adjustment,
                "幸运数字（7或77视为100）"));
            finalRoll = RollKeywordRules.RollMaximum;
            isForcedCritical = true;
        }
        else if (inheritedRaw == null && HbrFateDiceRollHelper.AppliesFloor(player, baseRoll))
        {
            diceSource += $"（托底：原始出目{baseRoll}按{HbrFateDiceCatalog.FloorValue}算）";
            baseRoll = HbrFateDiceCatalog.FloorValue;
            finalRoll = baseRoll;
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

    private static int NextRawRoll(Player player) =>
        player.RunState.Rng.Niche.NextInt(
            RollKeywordRules.RollMinimum,
            RollKeywordRules.RollExclusiveMaximum);

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
        ThirtyOneOClassRelicHelper.QueueResolvedRoll(Controller, roll);

        if (!RollKeywordRules.IsCriticalRoll(Controller, roll))
        {
            return;
        }

        if (roll.BaseRoll is >= 1 and <= RollKeywordRules.RollMaximum &&
            roll.BaseRoll >= RollKeywordRules.GetCriticalMinimum(Controller))
            RanaByakkoCombatHelper.QueueRawCritical(Controller);
        RollKeywordCombatState.RecordCriticalSuccess(Controller);
        Controller?.GetRelic<ElectronicSoldierManual>()
            ?.QueueSixSenseReward();
        Controller?.GetRelic<Millsage>()?.QueueCriticalCannonReward();
    }

    private async Task ResolveHotaruCriticalRewards(PlayerChoiceContext context)
    {
        await ThirtyOneOClassRelicHelper.ResolvePendingRollRewards(context, Controller);
        await RanaByakkoCombatHelper.ResolvePendingRawCritical(context, Controller);
        await HotaruRareRollHelper.ResolvePending(context, Controller);
        if (Controller?.GetRelic<Millsage>() is { } millsage)
            await millsage.ResolveCriticalCannonRewards(context);
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

    private void QueueCardSwordheart(SoulRollBreakdown roll, CardModel card)
    {
        Player player = Controller;
        if (!RollKeywordRules.UsesSakiRules(player) ||
            player?.Creature == null ||
            player.Creature.HasPower<MagicSwordModePower>() ||
            RollKeywordRules.IsCopiedCard(card))
        {
            return;
        }

        // A critical success gives its own amount (two, 剑鸣 two more) rather than
        // filling swordheart (design v3, section 2).
        if (RollKeywordRules.IsCriticalRoll(player, roll))
        {
            _pendingSwordheartAmount += RollKeywordRules.GetCriticalSwordheartAmount(player);
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
            player.Creature.HasPower<MagicSwordModePower>() ||
            RollKeywordRules.IsCopiedCard(cardSource))
        {
            return 0;
        }

        if (RollKeywordRules.IsCriticalRoll(player, roll))
        {
            return RollKeywordRules.GetCriticalSwordheartAmount(player);
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

    private async Task PlayPendingFateEffects(PlayerChoiceContext choiceContext)
    {
        HbrFateDicePendingEffects effects = _pendingFateEffects;
        _pendingFateEffects = default;
        await HbrFateDiceRollHelper.Resolve(choiceContext, Controller, effects);
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

        List<RollPowerReservation> reservations = _pendingRollPowerReservations;
        _pendingRollPowerReservations = new();
        if (reservations != null)
            await HbrRollPowerHelper.ConsumeReservations(choiceContext, reservations, cardSource);

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
        return power is CountedRollPower ||
               power is SwordheartPower ||
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

    internal object CaptureRollReservationMarker() => _pendingRollPowerReservations;

    internal async Task CleanupFailedRollReservations(object marker)
    {
        ExceptionDispatchInfo failure = null;
        // A nested play can fail before its AfterCardPlayed hook. Unwind only
        // the states it created. If AfterCardPlayed already restored the parent
        // and native pile cleanup then fails, its original marker is current
        // and the parent's still-pending roll must remain untouched.
        while (_isCardRollActive && !ReferenceEquals(_pendingRollPowerReservations, marker))
        {
            List<RollPowerReservation> reservations = _pendingRollPowerReservations;
            _pendingRollPowerReservations = new();
            try
            {
                failure = await CaptureRollCleanupFailure(failure, () =>
                    HbrRollPowerHelper.ConsumeReservations(new ThrowingPlayerChoiceContext(), reservations, Owner));
            }
            finally { FinishCardRollState(); }
        }
        failure?.Throw();
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
                _currentMirrorMultiplier,
                _pendingSwordheartAmount,
                _pendingFragileTriggers,
                _pendingFateEffects,
                _pendingRollPowerReservations));
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
        _currentMirrorMultiplier = state.CurrentMirrorMultiplier;
        _pendingSwordheartAmount = state.PendingSwordheartAmount;
        _pendingFragileTriggers = state.PendingFragileTriggers;
        _pendingFateEffects = state.PendingFateEffects;
        _pendingRollPowerReservations = state.PendingRollPowerReservations;
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
        _currentMirrorMultiplier = 1m;
        _pendingSwordheartAmount = 0;
        _pendingFragileTriggers = 0;
        _pendingFateEffects = default;
        _pendingRollPowerReservations = new();
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

        internal decimal CurrentMirrorMultiplier { get; }

        internal int PendingSwordheartAmount { get; }

        internal int PendingFragileTriggers { get; }

        internal HbrFateDicePendingEffects PendingFateEffects { get; }

        internal List<RollPowerReservation> PendingRollPowerReservations { get; }

        internal SuspendedRollState(
            bool isCardRollActive,
            bool hasRolledForCurrentPlay,
            int damageSegmentsStarted,
            int currentRoll,
            SoulRollBreakdown currentRollDetails,
            int currentEnergySpent,
            int currentRegularSwordheartAmount,
            decimal currentValueMultiplier,
            decimal currentMirrorMultiplier,
            int pendingSwordheartAmount,
            int pendingFragileTriggers,
            HbrFateDicePendingEffects pendingFateEffects,
            List<RollPowerReservation> pendingRollPowerReservations)
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
            CurrentMirrorMultiplier = currentMirrorMultiplier;
            PendingSwordheartAmount = pendingSwordheartAmount;
            PendingFragileTriggers = pendingFragileTriggers;
            PendingFateEffects = pendingFateEffects;
            PendingRollPowerReservations = pendingRollPowerReservations;
        }
    }
}
