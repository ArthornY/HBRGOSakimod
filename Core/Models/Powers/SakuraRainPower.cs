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
    private const decimal DefensePercentPerCopy = 20m;
    private object _lastInsertedTurn;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AppliedCopies { get; private set; } = 1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ActiveCopies { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IsDamageReductionActive { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasRetaliation { get; private set; }

    public override LocString Title => HasRetaliation
        ? new LocString("powers", "SAKURA_RAIN_POWER.retaliationTitle")
        : base.Title;

    public override LocString Description => HbrBattleStatsHelper.IsEnabled(IsMutable ? Owner : null)
        ? new LocString("powers", HasRetaliation
            ? "SAKURA_RAIN_POWER.hbrRetaliationDescription" : "SAKURA_RAIN_POWER.hbrDescription")
        : HasRetaliation
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

    internal void AddCopy()
    {
        AssertMutable();
        AppliedCopies = (int)Math.Min(int.MaxValue, (long)Math.Max(1, AppliedCopies) + 1);
        Owner?.InvokePowerModified(this, 0, true);
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

        await TriggerTurnEnd(choiceContext);
    }

    internal static Task TriggerInsertedTurnEnd(PlayerChoiceContext context, Player player, object token)
    {
        SakuraRainPower power = player?.Creature?.GetPower<SakuraRainPower>();
        if (power == null || token == null || ReferenceEquals(power._lastInsertedTurn, token)) return Task.CompletedTask;
        power._lastInsertedTurn = token;
        return power.TriggerTurnEnd(context);
    }

    private async Task TriggerTurnEnd(PlayerChoiceContext context)
    {
        // Capture eligibility before spending: this payment may close magic
        // sword and play its finisher, but the earned defense still applies.
        if (!Owner.HasPower<MagicSwordModePower>() || !CombatModeHelper.HasEnemyAttackIntent(Owner)) return;
        int copies = Math.Max(1, AppliedCopies);
        if (!await SwordheartSpendHelper.TrySpend(context, Owner, 1m)) return;
        Flash();
        bool changed = SetDamageReductionActive(true) || ActiveCopies != copies;
        ActiveCopies = copies;
        Owner.InvokePowerModified(this, 0, true);
        if (changed) await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || !IsDamageReductionActive) return;
        ActiveCopies = 0;
        if (SetDamageReductionActive(false))
        {
            Owner.InvokePowerModified(this, 0, true);
            await CombatPreviewRefreshHelper.RefreshOpponentIntents(Owner);
        }
    }

    internal static decimal GetDefenseBonusPercent(Creature creature) =>
        creature?.GetPower<SakuraRainPower>() is { IsDamageReductionActive: true } power
            ? Math.Max(1, power.ActiveCopies) * DefensePercentPerCopy : 0m;

    public override Task AfterRemoved(Creature oldOwner)
    {
        return IsDamageReductionActive
            ? CombatPreviewRefreshHelper.RefreshOpponentIntents(oldOwner)
            : Task.CompletedTask;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (HbrBattleStatsHelper.IsEnabled(Owner) || !IsDamageReductionActive ||
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
        using var reflectedSkill = HbrBattleStatsHelper.IsEnabled(Owner)
            ? HbrEffectSkillDamageContext.BeginWorld(null, Owner, null, retaliationDamage, ValueProp.Unpowered) : null;
        await CreatureCommandCompatibility.Damage(
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

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _lastInsertedTurn = null;
    }
}
