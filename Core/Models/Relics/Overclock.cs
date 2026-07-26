using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class Overclock : RelicModel
{
    public const int ChargePerLevel = 100;
    public const int MaxLevel = 3;
    public const int MaxCharge = ChargePerLevel * MaxLevel;

    private int _charge;
    private int _activeLevel;
    private bool _hasQueuedExtraTurn;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => false;

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
            int nextCharge = Math.Clamp(value, 0, ChargeCapacity);
            if (_charge == nextCharge)
            {
                return;
            }

            _charge = nextCharge;
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

    public int Level => Math.Min(MaxLevel, Charge / ChargePerLevel);

    public int ChargeIntoLevel => Charge % ChargePerLevel;

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

    private int ChargeCapacity => MaxCharge + ActiveLevel * ChargePerLevel;

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
            target == null ||
            dealer.Side != Owner.Creature.Side ||
            target.Side == Owner.Creature.Side)
        {
            return 1m;
        }

        return ActiveLevel switch
        {
            1 => 1.2m,
            2 => 1.5m,
            _ => 2m
        };
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
        if (Charge > MaxCharge)
        {
            Charge = MaxCharge;
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
        if (Charge >= ChargeCapacity ||
            result.TotalDamage <= 0 ||
            dealer == null ||
            dealer.Side != Owner.Creature.Side ||
            target == null ||
            target.Side == Owner.Creature.Side)
        {
            return Task.CompletedTask;
        }

        int gainedCharge = (int)Math.Ceiling(result.TotalDamage / 10m);
        if (gainedCharge > 0)
        {
            Charge += gainedCharge;
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

        Charge -= consumedLevel * ChargePerLevel;
        ActiveLevel = 0;
        HasQueuedExtraTurn = false;
        Status = RelicStatus.Normal;
        Flash();
    }
}
