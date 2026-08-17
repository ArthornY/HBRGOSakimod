using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SakuraRainPower : PowerModel
{
    private const decimal _incomingDamageMultiplier = 0.5m;
    private const decimal _retaliationDamageMultiplier = 0.5m;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsDamageReductionActive { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasRetaliation { get; private set; }

    public override LocString Title => HasRetaliation
        ? new LocString("powers", "SAKURA_RAIN_POWER.retaliationTitle")
        : base.Title;

    public override LocString Description => HasRetaliation
        ? new LocString("powers", "SAKURA_RAIN_POWER.retaliationDescription")
        : base.Description;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        if (cardSource is SakuraRainHbrgoSaki sakuraRain && sakuraRain.HasRetaliationUpgrade)
        {
            EnableRetaliation();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        if (SetDamageReductionActive(false))
        {
            await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
        }
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
        {
            return;
        }

        bool shouldActivate = false;
        if (CombatModeHelper.HasEnemyAttackIntent(Owner) &&
            await SwordheartSpendHelper.TrySpend(choiceContext, Owner, 1m))
        {
            shouldActivate = true;
            Flash();
        }

        if (SetDamageReductionActive(shouldActivate))
        {
            await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        return IsDamageReductionActive
            ? CombatPreviewRefreshHelper.RefreshOpponentIntents(oldOwner)
            : Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (!IsDamageReductionActive ||
            amount <= 0m ||
            target != Owner ||
            dealer == null ||
            dealer.Side == Owner.Side ||
            !props.IsPoweredAttack())
        {
            return 1m;
        }

        return HbrgoSakiDamageMultiplierHelper.Add(
            _incomingDamageMultiplier,
            this);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (!HasRetaliation ||
            !IsDamageReductionActive ||
            target != Owner ||
            result.TotalDamage <= 0 ||
            dealer == null ||
            dealer.Side == Owner.Side ||
            !props.IsPoweredAttack())
        {
            return;
        }

        Creature[] targets = Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature => creature.IsHittable)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        decimal retaliationDamage =
            result.TotalDamage * _retaliationDamageMultiplier;
        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            targets,
            retaliationDamage,
            ValueProp.Unpowered,
            Owner,
            null);
    }

    private bool SetDamageReductionActive(bool active)
    {
        if (IsDamageReductionActive == active)
        {
            return false;
        }

        AssertMutable();
        IsDamageReductionActive = active;
        return true;
    }

    internal void EnableRetaliation()
    {
        AssertMutable();
        HasRetaliation = true;
    }
}
