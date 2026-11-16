using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Relics.Visibility;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class Overclock : RelicModel, IModRelicVisibility
{
    public const int BaseChargePerLevel = 100;
    public const int MaxLevel = 3;
    public const int FloorLevelLimit = 12;

    public int ChargePerLevel =>
        BaseChargePerLevel * Math.Max(
            1,
            Owner?.RunState.Players.Count ?? 1);

    public int MaxCharge => ChargePerLevel * MaxLevel;

    private int _charge;
    private int _chargeFractionHundredths;
    private int _activeLevel;
    private int _freeRewardLevels;
    private bool _hasQueuedExtraTurn;
    private int _floorLevelsSpent;
    private Dictionary<CardPlay, decimal> _pendingCardCharge = new();

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => false;

    public bool IsRelicVisible => false;

    public override int DisplayAmount => Charge;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/overclock.svg");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    [SavedProperty]
    public int Charge
    {
        get => _charge;
        private set
        {
            AssertMutable();
            int nextCharge = Math.Clamp(
                value,
                0,
                MaxCharge + MaxLevel * ChargePerLevel);
            if (_charge == nextCharge)
            {
                return;
            }

            int previousLevel = Level;
            _charge = nextCharge;
            InvokeDisplayAmountChanged();
            RefreshEnemyDetection(previousLevel);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ChargeFractionHundredths
    {
        get => _chargeFractionHundredths;
        private set
        {
            AssertMutable();
            int nextFraction = Math.Clamp(value, 0, 99);
            if (_chargeFractionHundredths == nextFraction)
            {
                return;
            }

            _chargeFractionHundredths = nextFraction;
            InvokeDisplayAmountChanged();
        }
    }

    public decimal PreciseCharge
    {
        get => Charge + ChargeFractionHundredths / 100m;
        private set
        {
            AssertMutable();
            decimal nextCharge = Math.Clamp(
                decimal.Round(value, 2, MidpointRounding.AwayFromZero),
                0m,
                ChargeCapacity);
            int nextWhole = (int)Math.Floor(nextCharge);
            int nextFraction =
                (int)((nextCharge - nextWhole) * 100m);
            if (_charge == nextWhole &&
                _chargeFractionHundredths == nextFraction)
            {
                return;
            }

            int previousLevel = Level;
            _charge = nextWhole;
            _chargeFractionHundredths = nextFraction;
            InvokeDisplayAmountChanged();
            RefreshEnemyDetection(previousLevel);
        }
    }

    private void RefreshEnemyDetection(int previousLevel)
    {
        if (previousLevel != Level)
            HbrEnemyFlavorSchedulingHelper.OnOverclockLevelChanged(Owner?.Creature?.CombatState);
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActiveLevel
    {
        get => _activeLevel;
        private set
        {
            AssertMutable();
            int nextLevel = Math.Clamp(value, 0, MaxLevel);
            if (_activeLevel == nextLevel)
            {
                return;
            }

            _activeLevel = nextLevel;
            InvokeDisplayAmountChanged();
            CombatPreviewRefreshHelper.RefreshCombatState(Owner?.Creature, nameof(Overclock));
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasQueuedExtraTurn
    {
        get => _hasQueuedExtraTurn;
        private set
        {
            AssertMutable();
            _hasQueuedExtraTurn = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    // Keep the existing save/API names; this allowance now belongs to an act.
    public string FloorUsageKey { get; private set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FloorLevelsSpent
    {
        get => _floorLevelsSpent;
        private set
        {
            AssertMutable();
            _floorLevelsSpent = Math.Clamp(value, 0, FloorLevelLimit);
        }
    }

    public int UsedFloorLevels
    {
        get
        {
            string key = CurrentFloorUsageKey;
            int spent = GetUsageForAct(key);
            if (Owner?.RunState is not { } run)
            {
                return spent;
            }

            // OD starts a turn for the whole party. Every owner's gauge must
            // therefore use the same allowance, including a newly added relic.
            foreach (Player player in run.Players)
            {
                Overclock relic = player.GetRelic<Overclock>();
                if (relic != null)
                {
                    spent = Math.Max(spent, relic.GetUsageForAct(key));
                }
            }
            return spent;
        }
    }

    public int RemainingFloorLevels => FloorLevelLimit - UsedFloorLevels;

    private string CurrentFloorUsageKey => Owner?.RunState is { } run
        // Floor changes, combat boundaries and subrooms share one act budget.
        ? FormattableString.Invariant($"{run.CurrentActIndex}:act")
        : string.Empty;

    private int GetUsageForAct(string key)
    {
        if (FloorUsageKey == key) return FloorLevelsSpent;
        // Older saves recorded an act plus a floor/entrance suffix. Preserve
        // their known spending in this act before normalizing the key, without
        // accepting another act's budget or allocating parsed key strings.
        int separator = key.IndexOf(':');
        if (separator < 0 || string.IsNullOrEmpty(FloorUsageKey) ||
            FloorUsageKey.Length <= separator || FloorUsageKey[separator] != ':' ||
            string.CompareOrdinal(FloorUsageKey, 0, key, 0, separator) != 0)
            return 0;
        ReadOnlySpan<char> suffix = FloorUsageKey.AsSpan(separator + 1);
        return suffix.SequenceEqual("entrance") || suffix.StartsWith("floor:", StringComparison.Ordinal)
            ? FloorLevelsSpent : 0;
    }

    public int Level => Math.Min(
        MaxLevel,
        Charge / ChargePerLevel);

    public decimal ChargeIntoLevel => PreciseCharge % ChargePerLevel;

    public int DisplayChargeIntoLevel =>
        (int)Math.Floor(ChargeIntoLevel);

    public bool IsMaxLevel => Level >= MaxLevel;

    public bool CanActivate =>
        CanActivateState &&
        CanActivateInput;

    // Ending locally locks ordinary input while waiting for teammates. OD
    // can still suspend that idle turn and restore its ready vote afterwards.
    // Both belong to the local player, who need not own this relic: in co-op
    // anyone opens the party's OD from the gauge (user, 2026-11-09).
    private bool CanActivateInput =>
        !CombatManager.Instance.PlayerActionsDisabled ||
        (Owner?.RunState is { } run && LocalContext.GetMe(run.Players) is { } me &&
            CombatManager.Instance.IsPlayerReadyToEndTurn(me));

    private bool CanActivateState =>
        ActiveLevel == 0 &&
        Level > 0 &&
        RemainingFloorLevels > 0 &&
        OverclockImmediateTurnCoordinator.CanBegin(Owner);

    public bool CanActivateLevel(int level) =>
        CanActivateLevelState(level) &&
        CanActivateInput;

    private bool CanActivateLevelState(int level) =>
        level >= 1 && level <= MaxLevel &&
        level <= Level && level <= RemainingFloorLevels &&
        CanActivateState;

    private decimal ChargeCapacity =>
        MaxCharge + ActiveLevel * ChargePerLevel;

    internal void GainCharge(decimal amount)
    {
        if (amount <= 0m || PreciseCharge >= ChargeCapacity)
        {
            return;
        }

        PreciseCharge += amount;
    }

    internal void GainLevelProgress(decimal percentage)
    {
        GainCharge(ChargePerLevel * percentage / 100m);
    }

    // Enemy OverDrivePointDown: the gauge loses a share of one level.
    internal void LoseLevelProgress(decimal percentage)
    {
        if (percentage <= 0m || PreciseCharge <= 0m)
        {
            return;
        }

        PreciseCharge -= ChargePerLevel * percentage / 100m;
    }

    public bool TryActivate(int requestedLevel)
    {
        // Synchronized actions run while player input is paused. Validate the
        // game state and quota here without using the UI-only input lock.
        if (!CanActivateLevelState(requestedLevel))
        {
            return false;
        }

        _freeRewardLevels = 0;
        ActiveLevel = requestedLevel;
        HasQueuedExtraTurn = false;
        if (!OverclockImmediateTurnCoordinator.TryBegin(this))
        {
            ActiveLevel = 0;
            return false;
        }
        SpendFloorLevels(requestedLevel);
        Status = RelicStatus.Active;
        Flash();
        return true;
    }

    public bool IsSpecialExtraTurnFor(Player player)
    {
        return ActiveLevel > 0 && OverclockImmediateTurnCoordinator.IsActiveFor(player);
    }

    // Resistance completion rewards (More Than One Truth's 1MORE, unused since the
    // six-sense resistances were removed on 2026-11-09) use the same OD turn without consuming
    // charge or the party's shared act allowance.
    internal bool TryActivateRewardLevel(int level)
    {
        if (ActiveLevel != 0 || level < 1 || level > MaxLevel ||
            !OverclockImmediateTurnCoordinator.CanBegin(Owner)) return false;
        _freeRewardLevels = level;
        ActiveLevel = level;
        HasQueuedExtraTurn = false;
        if (!OverclockImmediateTurnCoordinator.TryBegin(this))
        {
            ActiveLevel = 0;
            _freeRewardLevels = 0;
            return false;
        }
        Status = RelicStatus.Active;
        Flash();
        return true;
    }

    internal void PromoteRewardLevel(int level)
    {
        int next = Math.Clamp(level, ActiveLevel, MaxLevel);
        int additional = next - ActiveLevel;
        if (ActiveLevel <= 0 || additional <= 0) return;
        ActiveLevel = next;
        _freeRewardLevels += additional;
        Flash();
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (ActiveLevel <= 0 ||
            amount <= 0m ||
            dealer == null ||
            dealer.Side != Owner.Creature.Side ||
            target?.Side == Owner.Creature.Side)
        {
            return 1m;
        }

        decimal multiplier = GetDamageMultiplier(ActiveLevel);
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }

    public override Task BeforeCombatStart()
    {
        // Also clears active/queued fields from saves made by the old OD rules.
        OverclockImmediateTurnCoordinator.EnsureLifecycle();
        SynchronizeFloorUsage();
        ResetCombatCharge();
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        SynchronizeFloorUsage();
        return Task.CompletedTask;
    }

    private void SynchronizeFloorUsage() =>
        SetFloorUsage(CurrentFloorUsageKey, UsedFloorLevels);

    internal void RestoreFloorLevels(int levels)
    {
        if (levels <= 0)
        {
            return;
        }

        SetSharedFloorUsage(Math.Max(0, UsedFloorLevels - levels));
    }

    private void SpendFloorLevels(int level) =>
        SetSharedFloorUsage(UsedFloorLevels + level);

    private void SetSharedFloorUsage(int spent)
    {
        string key = CurrentFloorUsageKey;
        SetFloorUsage(key, spent);
        if (Owner?.RunState is not { } run)
        {
            return;
        }

        foreach (Player player in run.Players)
        {
            player.GetRelic<Overclock>()?.SetFloorUsage(key, spent);
        }
    }

    private void SetFloorUsage(string key, int spent)
    {
        if (FloorUsageKey == key && FloorLevelsSpent == spent)
        {
            return;
        }

        AssertMutable();
        FloorUsageKey = key;
        FloorLevelsSpent = spent;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        OverclockImmediateTurnCoordinator.Clear();
        ResetCombatCharge();
        return Task.CompletedTask;
    }

    internal void ResetCombatCharge()
    {
        _pendingCardCharge.Clear();
        PreciseCharge = 0m;
        CancelImmediateTurn();
    }

    internal void CancelImmediateTurn()
    {
        _freeRewardLevels = 0;
        ActiveLevel = 0;
        HasQueuedExtraTurn = false;
        Status = RelicStatus.Normal;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CardModel card = cardPlay?.Card;
        Creature dealer = card?.Owner?.Creature;
        Creature ownerCreature = Owner?.Creature;
        if (dealer?.CombatState == null ||
            ownerCreature == null ||
            dealer.Side != ownerCreature.Side ||
            !ReferenceEquals(dealer.CombatState, ownerCreature.CombatState) ||
            IsPassiveChargeSuppressedFor(dealer, card))
        {
            return Task.CompletedTask;
        }

        decimal baseCharge = card.Type switch
        {
            CardType.Attack => 2m,
            CardType.Skill => 5m,
            CardType.Power => 10m,
            _ => 0m
        };
        if (baseCharge > 0m)
        {
            // Powers settle before relics. Snapshot this play's multiplier
            // before MaidCultivation consumes its layer or nested plays do.
            _pendingCardCharge[cardPlay] = baseCharge *
                MaidCultivationPower.GetOverclockMultiplier(dealer, card);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay != null &&
            _pendingCardCharge.Remove(cardPlay, out decimal gainedCharge))
        {
            GainCharge(gainedCharge);
        }

        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingCardCharge = new Dictionary<CardPlay, decimal>();
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel cardSource)
    {
        if (PreciseCharge >= ChargeCapacity ||
            !props.IsPoweredAttack() ||
            dealer == null ||
            dealer.Side != Owner.Creature.Side ||
            target == null ||
            target.Side == Owner.Creature.Side ||
            IsPassiveChargeSuppressedFor(dealer, cardSource) ||
            !CombatAttackHitHelper.TryClaim(this, result, dealer, cardSource))
        {
            return Task.CompletedTask;
        }

        // Each connected attack segment/target is worth the same amount,
        // including ordinary block. Poison, HP supplements and dodges do not count.
        decimal gainMultiplier =
            MaidCultivationPower.GetOverclockMultiplier(
                dealer,
                cardSource) *
            ElectronicSoldierManual.GetEquipmentOverdriveMultiplier(dealer, cardSource);
        decimal gainedCharge = decimal.Round(
            2.5m * gainMultiplier,
            2,
            MidpointRounding.AwayFromZero);
        if (gainedCharge > 0)
        {
            PreciseCharge += gainedCharge;
        }

        return Task.CompletedTask;
    }

    // Only OD itself suspends passive accumulation. Personal/native extra turns
    // charge normally; attribute summons to their owner rather than the relic
    // holder because this relic also listens to teammates' actions.
    internal static bool IsPassiveChargeSuppressedFor(Creature dealer, CardModel cardSource)
    {
        Player actor = dealer?.Player ?? dealer?.PetOwner ?? cardSource?.Owner;
        return actor != null && OverclockImmediateTurnCoordinator.IsActiveFor(actor);
    }

    // The selection screen reads the same values used by the combat hooks.
    internal static decimal GetDamageMultiplier(int level) => level switch
    {
        1 => 1.2m, 2 => 1.5m, 3 => 2m, _ => 1m
    };

    internal static decimal GetExtraTurnEnergy(int level) => level switch
    {
        1 => 2m, 2 => 3m, 3 => 5m, _ => 0m
    };

    internal static decimal GetExtraTurnDraw(int level) => level switch
    {
        1 => 3m, 2 => 5m, 3 => 8m, _ => 0m
    };

    internal void FinishImmediateTurn()
    {
        int consumedLevel = ActiveLevel;
        if (consumedLevel <= 0)
        {
            return;
        }

        PreciseCharge -= Math.Max(0, consumedLevel - _freeRewardLevels) * ChargePerLevel;
        _freeRewardLevels = 0;
        ActiveLevel = 0;
        HasQueuedExtraTurn = false;
        Status = RelicStatus.Normal;
        Flash();
    }
}
