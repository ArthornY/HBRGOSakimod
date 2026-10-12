using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SkullFeatherPower :
    PowerModel,
    IHighDimensionalShellObserver
{
    private const int ThresholdCount = 4;
    private const int StrengthPerThreshold = 2;
    private const int PlayerStatLossPerThreshold = 1;
    internal const int BreakDuration = 99;
    private const int IntangibleAmount = 99;
    private const int SlipperyAmount = 50;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F3E7D0");

    private decimal _exactShellMaximum;
    private bool _hasExactShellMaximum;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ShellMaximum
    {
        get => DpAmountCodec.ToNative(ExactShellMaximum);
        private set
        {
            _exactShellMaximum = Math.Max(0m, value);
            _hasExactShellMaximum = true;
        }
    }

    internal decimal ExactShellMaximum
    {
        get => _hasExactShellMaximum ? _exactShellMaximum : 0m;
        private set
        {
            AssertMutable();
            _exactShellMaximum = BigCombatNumbers.ClampStat(value);
            _hasExactShellMaximum = true;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ShellMaximumData
    {
        get => _hasExactShellMaximum ? DpAmountCodec.Format(_exactShellMaximum) : string.Empty;
        private set
        {
            if (string.IsNullOrEmpty(value)) return;
            _exactShellMaximum = DpAmountCodec.Parse(value);
            _hasExactShellMaximum = true;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HighestReachedThreshold { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingThresholds { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ObservedCriticalSuccessCount { get; private set; }

    public override Task AfterApplied(
        Creature applier,
        CardModel cardSource)
    {
        ObservedCriticalSuccessCount = GetTeamCriticalSuccessCount();
        HighDimensionalShellPower shell =
            Owner.GetPower<HighDimensionalShellPower>();
        if (shell != null)
        {
            OnHighDimensionalShellApplied(shell);
        }

        return Task.CompletedTask;
    }

    void IHighDimensionalShellObserver.OnHighDimensionalShellApplied(
        HighDimensionalShellPower shell)
    {
        OnHighDimensionalShellApplied(shell);
    }

    public void OnHighDimensionalShellApplied(
        HighDimensionalShellPower shell)
    {
        AssertMutable();
        ExactShellMaximum = Math.Max(
            1m,
            HighDimensionalShellPower.CalculateAmount(Owner));
        HighestReachedThreshold = CalculateReachedThresholds(
            shell.CurrentDp,
            ExactShellMaximum);
        PendingThresholds = 0;
    }

    public void OnHighDimensionalShellAmountChanged(
        decimal previousAmount,
        decimal currentAmount,
        decimal maximumAmount)
    {
        if (currentAmount >= previousAmount)
        {
            return;
        }

        AssertMutable();
        ExactShellMaximum = Math.Max(
            1m,
            ExactShellMaximum > 0m ? ExactShellMaximum : maximumAmount);
        int reachedThresholds = CalculateReachedThresholds(
            currentAmount,
            ExactShellMaximum);
        int newlyReached = Math.Max(
            0,
            reachedThresholds - HighestReachedThreshold);
        if (newlyReached <= 0)
        {
            return;
        }

        HighestReachedThreshold = reachedThresholds;
        PendingThresholds += newlyReached;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (target == Owner)
        {
            await ResolvePendingThresholds(choiceContext);
        }
    }

    public async Task AfterHighDimensionalShellBreak(
        PlayerChoiceContext choiceContext)
    {
        await ResolvePendingThresholds(choiceContext);

        BreakPower breakPower = Owner.GetPower<BreakPower>();
        int breakIncrease = Math.Max(
            0,
            BreakDuration + HelloWorldCombatHelper.GetInitialBreakBonus(Owner) -
            (breakPower?.Amount ?? 0));
        if (breakIncrease > 0)
        {
            await PowerCmd.Apply<BreakPower>(
                choiceContext,
                Owner,
                breakIncrease,
                Owner,
                null);
        }

        bool teamCanRoll = CombatState.Players.Any(
            RollKeywordRules.IsRollEnabled);
        if (teamCanRoll)
        {
            await PowerCmd.Apply<IntangiblePower>(
                choiceContext,
                Owner,
                IntangibleAmount,
                Owner,
                null);
        }
        else
        {
            decimal unscaledAmount = SlipperyAmount /
                (decimal)Math.Max(1, CombatState.Players.Count);
            await PowerCmd.Apply<SlipperyPower>(
                choiceContext,
                Owner,
                unscaledAmount,
                Owner,
                null);
        }
    }

    public override async Task AfterCardPlayedLate(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        int criticalSuccessCount = GetTeamCriticalSuccessCount();
        bool gainedCriticalSuccess =
            criticalSuccessCount > ObservedCriticalSuccessCount;
        ObservedCriticalSuccessCount = criticalSuccessCount;

        if (!HasProtection() ||
            !gainedCriticalSuccess &&
            cardPlay?.Card is not MimicSaviorStrikeHbrgoSaki)
        {
            return;
        }

        Flash();
        await RemoveProtection();
    }

    public override bool ShouldPowerBeRemovedAfterOwnerDeath()
    {
        return false;
    }

    private static int CalculateReachedThresholds(
        decimal currentAmount,
        decimal maximumAmount)
    {
        if (maximumAmount <= 0m) return 0;
        decimal lostAmount = Math.Max(0m, maximumAmount - currentAmount);
        return (int)Math.Clamp(decimal.Floor(
            lostAmount * ThresholdCount / maximumAmount),
            0,
            ThresholdCount);
    }

    private async Task ResolvePendingThresholds(
        PlayerChoiceContext choiceContext)
    {
        int thresholdCount = PendingThresholds;
        if (thresholdCount <= 0)
        {
            return;
        }

        PendingThresholds = 0;
        Flash();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            thresholdCount * StrengthPerThreshold,
            Owner,
            null);

        Creature[] livingPlayers = CombatState.PlayerCreatures
            .Where(creature => creature.IsAlive)
            .ToArray();
        if (livingPlayers.Length == 0)
        {
            return;
        }

        decimal statLoss =
            -thresholdCount * PlayerStatLossPerThreshold;
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            livingPlayers,
            statLoss,
            Owner,
            null);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            livingPlayers,
            statLoss,
            Owner,
            null);
    }

    private int GetTeamCriticalSuccessCount()
    {
        return CombatState?.Players.Sum(
            RollKeywordCombatState.GetCriticalSuccessCount) ?? 0;
    }

    private bool HasProtection()
    {
        return Owner.HasPower<IntangiblePower>() ||
            Owner.HasPower<SlipperyPower>();
    }

    private async Task RemoveProtection()
    {
        PowerModel[] protectionPowers = Owner.Powers
            .Where(power =>
                power is IntangiblePower or SlipperyPower)
            .ToArray();
        foreach (PowerModel power in protectionPowers)
        {
            await PowerCmd.Remove(power);
        }
    }
}
