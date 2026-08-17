using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class StrongestShieldPower : PowerModel
{
    private const int _rollMin = 1;
    private const int _rollMax = 100;
    private const int _rollExclusiveMax = _rollMax + 1;
    private const decimal _baseThreshold = 33m;
    private const decimal _thresholdMultiplier = 1.5m;
    internal const decimal DamageMultiplier = 0.5m;

    private int _pendingSwordheartAmount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Color AmountLabelColor => new("6ED7FF");

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
        if (strongestShield == null || !strongestShield.RollSucceeds(target, dealer, amount, props, cardSource))
        {
            return null;
        }

        strongestShield.Flash();
        return strongestShield;
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
        RegisterCriticalSuccess(roll);
        PendingSwordheartAmount += GetSwordheartAmount(roll);
        return roll > GetThreshold();
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner)
        {
            return;
        }

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
        GuideLittleHand guideLittleHand = Owner.Player?.GetRelic<GuideLittleHand>();
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
        if (useStrengthen)
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

        if (protagonistPrivilege != null)
        {
            sharedBonuses.Add(new SoulRollBonusDetail(
                protagonistPrivilege.RollPenalty,
                "主人公特典"));
        }

        List<SoulRollBreakdown> finalRolls = rolls
            .Select(roll => ApplyRollBonuses(roll, sharedBonuses))
            .ToList();
        int winningIndex = finalRolls.FindIndex(
            roll => roll.FinalRoll == _rollMax);
        if (winningIndex < 0)
        {
            int highestRoll = finalRolls.Max(roll => roll.FinalRoll);
            winningIndex = finalRolls.FindIndex(
                roll => roll.FinalRoll == highestRoll);
        }

        discardedRolls = finalRolls
            .Where((_, index) => index != winningIndex)
            .ToArray();
        return finalRolls[winningIndex];
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

    private void RecordRollHistory(
        SoulRollBreakdown winningRoll,
        IReadOnlyList<SoulRollBreakdown> discardedRolls,
        CardModel cardSource)
    {
        string trigger = string.IsNullOrWhiteSpace(cardSource?.Title)
            ? "一次攻击"
            : $"「{cardSource.Title}」";
        string source = $"能力「最强之盾」判定，由{trigger}触发";

        foreach (SoulOfAnchor soul in Owner.Player?.Relics.OfType<SoulOfAnchor>() ?? Enumerable.Empty<SoulOfAnchor>())
        {
            soul.RecordAbilityRoll(
                winningRoll,
                discardedRolls,
                source);
        }
    }

    private int RollDeterministicD100(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        return new Rng(BuildRollSeed(target, dealer, amount, props, cardSource, rollIndex)).NextInt(_rollMin, _rollExclusiveMax);
    }

    private uint BuildRollSeed(Creature target, Creature dealer, decimal amount, ValueProp props, CardModel cardSource, int rollIndex)
    {
        unchecked
        {
            int hash = (int)Owner.CombatState.RunState.Rng.Seed;
            hash = AppendHash(hash, Owner.Player?.RunState.GetPlayerSlotIndex(Owner.Player) ?? 0);
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
        decimal threshold = _baseThreshold;
        for (int i = 1; i < Amount; i++)
        {
            threshold *= _thresholdMultiplier;
        }

        return threshold;
    }

    private int GetSwordheartAmount(int roll)
    {
        Player player = Owner.Player;
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

    private void RegisterCriticalSuccess(int roll)
    {
        Player player = Owner.Player;
        if (!RollKeywordRules.IsCriticalRoll(player, roll))
        {
            return;
        }

        RollKeywordCombatState.RecordCriticalSuccess(player);
        player.GetRelic<ElectronicSoldierManual>()?
            .QueueSixSenseReward();
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
