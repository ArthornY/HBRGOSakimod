using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public class StrongestShieldPower : PowerModel
{
    private const int _rollMin = 1;
    private const int _rollMax = 100;
    private const int _rollExclusiveMax = _rollMax + 1;
    private const decimal _baseThresholdReduction = 33m;
    private const decimal _thresholdReductionMultiplier = 1.5m;
    internal const decimal DamageMultiplier = 0.5m;

    internal decimal IncomingDamageMultiplier => GetType() == typeof(StrongestShieldPower) &&
        Owner?.GetPower<ItsMygoProtectionPower>() is { Amount: > 0, EnhancedShield: true }
            ? 0.25m : DamageMultiplier;

    private int _pendingSwordheartAmount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

    public override LocString Description => IsMutable && IncomingDamageMultiplier == 0.25m
        ? new LocString("powers", "STRONGEST_SHIELD_POWER.enhancedDescription") : base.Description;

    protected virtual Player RollOwner => Owner.Player;

    protected virtual string RollSourceName => "最强之盾";

    private int PendingSwordheartAmount
    {
        get => _pendingSwordheartAmount;
        set
        {
            AssertMutable();
            _pendingSwordheartAmount = value;
        }
    }

    public static StrongestShieldPower TryTriggerIncomingAttackDamageReduction(
        Creature target,
        Creature dealer,
        decimal amount,
        ValueProp props,
        CardModel cardSource)
    {
        if (amount <= 0m || !props.IsPoweredAttack())
        {
            return null;
        }

        StrongestShieldPower strongestShield = target.GetPower<StrongestShieldPower>();
        return strongestShield?.TryReduceIncomingAttack(
            target, dealer, amount, props, cardSource) == true
                ? strongestShield
                : null;
    }

    internal bool TryReduceIncomingAttack(
        Creature target,
        Creature dealer,
        decimal amount,
        ValueProp props,
        CardModel cardSource)
    {
        if (target != Owner || Amount <= 0 || amount <= 0m ||
            !props.IsPoweredAttack() ||
            !RollSucceeds(target, dealer, amount, props, cardSource))
        {
            return false;
        }

        Flash();
        return true;
    }

    private bool RollSucceeds(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource)
    {
        SoulRollBreakdown winningRoll = RollD100(
            target,
            dealer,
            amount,
            props,
            cardSource,
            0,
            out IReadOnlyList<SoulRollBreakdown> discardedRolls);
        int roll = winningRoll.FinalRoll;
        RecordRollHistory(winningRoll, discardedRolls, cardSource);
        HbrgoSakiPresentationBridge.NotifyRollResolved(RollOwner, roll);
        RegisterCriticalSuccess(winningRoll);
        PendingSwordheartAmount += GetSwordheartAmount(winningRoll);
        return roll > GetThreshold();
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        return target == Owner ? CompleteIncomingRoll(choiceContext, cardSource) : Task.CompletedTask;
    }

    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature dealer,
        DamageResult result, ValueProp props, Creature target, CardModel cardSource)
    {
        // AfterDamageReceived is skipped when a hit kills the phantom. The living
        // summoner must still spend the reserved dice and receive their swordheart.
        return PhantomIncomingDamageHelper.GetReductionOwner(target) == Owner
            ? CompleteIncomingRoll(choiceContext, cardSource) : Task.CompletedTask;
    }

    private async Task CompleteIncomingRoll(PlayerChoiceContext choiceContext, CardModel cardSource)
    {
        if (RollOwner?.GetRelic<Millsage>() is { } millsage)
            await millsage.ResolveCriticalCannonRewards(choiceContext);
        StrengthenPower strengthen = Owner.GetPower<StrengthenPower>();
        if (strengthen != null)
        {
            await strengthen.ConsumeReservedRolls(
                choiceContext,
                cardSource);
        }

        ExtremeDodgeRollPenaltyPower rollPenalty =
            Owner.GetPower<ExtremeDodgeRollPenaltyPower>();
        if (rollPenalty != null)
        {
            await rollPenalty.ConsumeReservedRolls(
                choiceContext,
                cardSource);
        }

        if (PendingSwordheartAmount <= 0)
        {
            return;
        }

        int swordheartAmount = GetSwordheartAmountToApply(PendingSwordheartAmount);
        PendingSwordheartAmount = 0;
        if (swordheartAmount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<SwordheartPower>(choiceContext, Owner, swordheartAmount, Owner, null);
    }

    private SoulRollBreakdown RollD100(
        Creature target,
        Creature dealer,
        decimal amount,
        ValueProp props,
        CardModel cardSource,
        int rollIndex,
        out IReadOnlyList<SoulRollBreakdown> discardedRolls)
    {
        List<SoulRollBreakdown> rolls = new()
        {
            SoulRollBreakdown.FromFinalRoll(
                RollDeterministicD100(
                    target,
                    dealer,
                    amount,
                    props,
                    cardSource,
                    rollIndex),
                "基础骰")
        };
        GuideLittleHand guideLittleHand = RollOwner?.GetRelic<GuideLittleHand>();
        if (guideLittleHand != null)
        {
            rolls.Add(SoulRollBreakdown.FromFinalRoll(
                RollDeterministicD100(
                    target,
                    dealer,
                    amount,
                    props,
                    cardSource,
                    ++rollIndex),
                "优势骰：导游的小手"));
            guideLittleHand.TakeHigherRoll(
                rolls[0].FinalRoll,
                rolls[1].FinalRoll);
        }

        StrengthenPower strengthen = Owner.GetPower<StrengthenPower>();
        bool useStrengthen = strengthen?.TryReserveRoll() == true;
        if (useStrengthen)
        {
            rolls.Add(SoulRollBreakdown.FromFinalRoll(
                RollDeterministicD100(
                    target,
                    dealer,
                    amount,
                    props,
                    cardSource,
                    ++rollIndex),
                "优势骰：强化"));
        }

        ProtagonistPrivilegePower protagonistPrivilege =
            Owner.GetPower<ProtagonistPrivilegePower>();
        if (protagonistPrivilege != null)
        {
            rolls.Add(SoulRollBreakdown.FromFinalRoll(
                RollDeterministicD100(
                    target,
                    dealer,
                    amount,
                    props,
                    cardSource,
                    ++rollIndex),
                "优势骰：主人公特典"));
        }

        ExtremeDodgeRollPenaltyPower rollPenalty =
            Owner.GetPower<ExtremeDodgeRollPenaltyPower>();
        bool useRollPenalty =
            rollPenalty?.TryReserveRoll() == true;
        List<SoulRollBonusDetail> sharedBonuses = new();
        int tourGuidePenalty = TourGuidePower.GetRollPenalty(Owner);
        if (tourGuidePenalty != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                tourGuidePenalty, "导游之力", isPercentage: true));
        }
        // Incoming attacks may carry another player's card source. Only a roll
        // belonging to the affected card's owner inherits its attribute penalty.
        int nullificationPenalty = NararaNullificationPenaltyHelper.GetRollPenalty(cardSource, RollOwner);
        if (nullificationPenalty != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                nullificationPenalty, "无效化", isPercentage: true));
        }
        int highSongBonus = LostStarSongSupportPower.GetRollBonus(Owner);
        if (highSongBonus != 0)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                highSongBonus, "迷途之星的歌", isPercentage: true));
        }
        if (useStrengthen)
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

        if (protagonistPrivilege != null)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                protagonistPrivilege.RollPenalty,
                "主人公特典", isPercentage: true));
        }

        List<SoulRollBreakdown> finalRolls = rolls
            .Select(roll => RollModifierHelper.Apply(roll, sharedBonuses))
            .Select(roll => RollKeywordRules.ResolveRollTier(RollOwner, roll))
            .ToList();
        int winningIndex = 0;
        bool hasCritical = RollKeywordRules.IsCriticalRoll(RollOwner, finalRolls[0]);
        for (int index = 1; index < finalRolls.Count; index++)
        {
            bool isCritical = RollKeywordRules.IsCriticalRoll(RollOwner, finalRolls[index]);
            if (isCritical && !hasCritical ||
                isCritical == hasCritical &&
                finalRolls[index].FinalRoll > finalRolls[winningIndex].FinalRoll)
            {
                winningIndex = index;
                hasCritical = isCritical;
            }
        }

        discardedRolls = finalRolls
            .Where((_, index) => index != winningIndex)
            .ToArray();
        return finalRolls[winningIndex];
    }

    private void RecordRollHistory(
        SoulRollBreakdown winningRoll,
        IReadOnlyList<SoulRollBreakdown> discardedRolls,
        CardModel cardSource)
    {
        string trigger = string.IsNullOrWhiteSpace(cardSource?.Title)
            ? "一次攻击"
            : $"「{cardSource.Title}」";
        string source = $"能力「{RollSourceName}」判定，由{trigger}触发";

        foreach (SoulOfAnchor soul in RollOwner?.Relics.OfType<SoulOfAnchor>() ?? Enumerable.Empty<SoulOfAnchor>())
        {
            soul.RecordAbilityRoll(
                winningRoll,
                discardedRolls,
                source);
        }
    }

    private int RollDeterministicD100(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        return RngCompatibility.Create(
            BuildRollSeed(target, dealer, amount, props, cardSource, rollIndex))
            .NextInt(_rollMin, _rollExclusiveMax);
    }

    private uint BuildRollSeed(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        unchecked
        {
            int hash = (int)RngCompatibility.GetRunSeed(Owner.CombatState.RunState.Rng);
            hash = AppendHash(hash, RollOwner?.RunState.GetPlayerSlotIndex(RollOwner) ?? 0);
            hash = AppendHash(hash, RunManager.Instance.ActionExecutor.CurrentlyRunningAction?.Id ?? 0u);
            hash = AppendHash(hash, CombatManager.Instance.History.Entries.Count());
            hash = AppendHash(hash, Owner.CombatState.RoundNumber);
            hash = AppendHash(hash, (int)Owner.CombatState.CurrentSide);
            hash = AppendHash(hash, target.CombatId ?? 0u);
            hash = AppendHash(hash, dealer?.CombatId ?? 0u);
            hash = AppendHash(hash, HashString(target.ModelId.Entry));
            hash = AppendHash(hash, HashString(dealer?.ModelId.Entry ?? string.Empty));
            hash = AppendHash(hash, HashString(cardSource?.Id.Entry ?? string.Empty));
            hash = AppendHash(hash, (int)(amount * 100m));
            hash = AppendHash(hash, (int)props);
            hash = AppendHash(hash, rollIndex);
            return (uint)hash;
        }
    }

    private static int AppendHash(int hash, int value)
    {
        unchecked
        {
            return hash * 397 ^ value;
        }
    }

    private static int AppendHash(int hash, uint value)
    {
        unchecked
        {
            return AppendHash(hash, (int)value);
        }
    }

    private static int HashString(string value)
    {
        unchecked
        {
            int hash = 352654597;
            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 397 ^ value[i];
            }

            return hash;
        }
    }

    private decimal GetThreshold()
    {
        decimal thresholdReduction = _baseThresholdReduction;
        for (int i = 1;
             i < Amount && thresholdReduction < _rollMax;
             i++)
        {
            thresholdReduction *= _thresholdReductionMultiplier;
        }

        return Math.Max(0m, _rollMax - thresholdReduction);
    }

    private int GetSwordheartAmount(SoulRollBreakdown roll)
    {
        Player player = RollOwner;
        if (!RollKeywordRules.UsesSakiRules(player) ||
            Owner.HasPower<MagicSwordModePower>())
        {
            return 0;
        }

        if (RollKeywordRules.IsCriticalRoll(player, roll))
        {
            return SwordheartPower.GetMaxAmount(Owner);
        }

        return RollKeywordRules.GetRegularSwordheartAmount(player, roll);
    }

    private void RegisterCriticalSuccess(SoulRollBreakdown roll)
    {
        Player player = RollOwner;
        if (player == null || !RollKeywordRules.IsCriticalRoll(player, roll))
        {
            return;
        }

        RollKeywordCombatState.RecordCriticalSuccess(player);
        player.GetRelic<ElectronicSoldierManual>()?
            .QueueSixSenseReward();
        player.GetRelic<Millsage>()?.QueueCriticalCannonReward();
    }

    private int GetSwordheartAmountToApply(int amount)
    {
        SwordheartPower swordheart = Owner.GetPower<SwordheartPower>();
        int current = swordheart?.Amount ?? 0;
        int remaining = Math.Max(0, SwordheartPower.GetMaxAmount(Owner) - current);
        return Math.Min(amount, remaining);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingSwordheartAmount = 0;
    }
}
