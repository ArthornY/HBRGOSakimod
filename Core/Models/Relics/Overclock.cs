using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
    public const int ChargePerLevel = 100;
    public const int MaxLevel = 3;
    public const int MaxCharge = ChargePerLevel * MaxLevel;

    private int _charge;
    private int _chargeFractionHundredths;
    private int _activeLevel;
    private bool _hasQueuedExtraTurn;

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

            _charge = nextCharge;
            InvokeDisplayAmountChanged();
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

            _charge = nextWhole;
            _chargeFractionHundredths = nextFraction;
            InvokeDisplayAmountChanged();
        }
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

    public int Level => Math.Min(
        MaxLevel,
        Charge / ChargePerLevel);

    public decimal ChargeIntoLevel => PreciseCharge % ChargePerLevel;

    public int DisplayChargeIntoLevel =>
        (int)Math.Floor(ChargeIntoLevel);

    public bool IsMaxLevel => Level >= MaxLevel;

    public bool CanActivate =>
        CanActivateState &&
        !CombatManager.Instance.PlayerActionsDisabled;

    private bool CanActivateState =>
        ActiveLevel == 0 &&
        Level > 0 &&
        CombatManager.Instance.IsInProgress &&
        CombatManager.Instance.PlayersTakingExtraTurn.Count == 0 &&
        Owner.Creature.CombatState?.CurrentSide == CombatSide.Player &&
        Owner.Creature.IsAlive;

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

    public bool TryActivate(int requestedLevel)
    {
        if (!CanActivateState || requestedLevel != Level)
        {
            return false;
        }

        ActiveLevel = requestedLevel;
        HasQueuedExtraTurn = false;
        Status = RelicStatus.Active;
        Flash();
        return true;
    }

    public bool IsSpecialExtraTurnFor(Player player)
    {
        return player?.Creature != null &&
            ActiveLevel > 0 &&
            HasQueuedExtraTurn &&
            player.Creature.IsAlive &&
            player.Creature.Side == Owner.Creature.Side &&
            CombatManager.Instance.PlayersTakingExtraTurn.Contains(player);
    }

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player?.Creature != null &&
            ActiveLevel > 0 &&
            HasQueuedExtraTurn &&
            player.Creature.IsAlive &&
            player.Creature.Side == Owner.Creature.Side;
    }

    public override decimal ModifyDamageMultiplicative(
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

        decimal multiplier = ActiveLevel switch
        {
            1 => 1.2m,
            2 => 1.5m,
            _ => 2m
        };
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }

    public override async Task AfterEnergyResetLate(Player player)
    {
        if (!IsSpecialExtraTurnFor(player))
        {
            return;
        }

        await PlayerCmd.SetEnergy(GetExtraTurnEnergy(), player);
    }

    public override decimal ModifyHandDrawLate(Player player, decimal count)
    {
        return IsSpecialExtraTurnFor(player)
            ? GetExtraTurnDraw()
            : count;
    }

    public override bool ShouldFlush(Player player)
    {
        bool isEnteringOverclockTurn =
            ActiveLevel > 0 &&
            CombatManager.Instance.PlayersTakingExtraTurn.Count == 0 &&
            player?.Creature != null &&
            player.Creature.IsAlive &&
            player.Creature.Side == Owner.Creature.Side;
        return !isEnteringOverclockTurn;
    }

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (ActiveLevel <= 0 ||
            side != Owner.Creature.Side ||
            !participants.Any(creature => creature?.Side == Owner.Creature.Side))
        {
            return Task.CompletedTask;
        }

        if (CombatManager.Instance.PlayersTakingExtraTurn.Count == 0 &&
            !HasQueuedExtraTurn)
        {
            HasQueuedExtraTurn = true;
            Flash();
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (ActiveLevel > 0 &&
            HasQueuedExtraTurn &&
            side == Owner.Creature.Side &&
            CombatManager.Instance.PlayersTakingExtraTurn.Count > 0 &&
            participants.Any(creature => creature?.Side == Owner.Creature.Side))
        {
            FinishSpecialExtraTurn();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        if (PreciseCharge > MaxCharge)
        {
            PreciseCharge = MaxCharge;
        }

        ActiveLevel = 0;
        HasQueuedExtraTurn = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
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
            result.TotalDamage <= 0 ||
            dealer == null ||
            dealer.Side != Owner.Creature.Side ||
            target == null ||
            target.Side == Owner.Creature.Side)
        {
            return Task.CompletedTask;
        }

        decimal gainMultiplier =
            MaidCultivationPower.GetOverclockMultiplier(
                dealer,
                cardSource);
        decimal gainedCharge = decimal.Round(
            result.TotalDamage / 10m * gainMultiplier,
            2,
            MidpointRounding.AwayFromZero);
        if (gainedCharge > 0)
        {
            PreciseCharge += gainedCharge;
        }

        return Task.CompletedTask;
    }

    private decimal GetExtraTurnEnergy()
    {
        return ActiveLevel switch
        {
            1 => 2m,
            2 => 3m,
            _ => 5m
        };
    }

    private decimal GetExtraTurnDraw()
    {
        return ActiveLevel switch
        {
            1 => 3m,
            2 => 5m,
            _ => 8m
        };
    }

    private void FinishSpecialExtraTurn()
    {
        int consumedLevel = ActiveLevel;
        if (consumedLevel <= 0)
        {
            return;
        }

        PreciseCharge -= consumedLevel * ChargePerLevel;
        ActiveLevel = 0;
        HasQueuedExtraTurn = false;
        Status = RelicStatus.Normal;
        Flash();
    }
}
