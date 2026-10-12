using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SniperModePower : PowerModel
{
    private const decimal MultiTargetDamageMultiplier = 0.75m;

    private static readonly FieldInfo SingleTargetField =
        PrivateAccess.Field<AttackCommand>("_singleTarget");
    private static readonly FieldInfo CombatStateField =
        PrivateAccess.Field<AttackCommand>("_combatState");
    private static readonly FieldInfo IsRandomlyTargetedField =
        PrivateAccess.Field<AttackCommand>(
            "<IsRandomlyTargeted>k__BackingField");

    private Dictionary<AttackCommand, ConcentratedAttackState>
        _concentratedAttacks = new();
    private List<AttackCommand> _activeAttacks = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Color AmountLabelColor => new("F8D36A");

    public override Task BeforeAttack(AttackCommand command)
    {
        bool wasNaturallyAllTarget =
            command.IsMultiTargeted && !command.IsRandomlyTargeted;
        TryConcentrateBowAttack(command, wasNaturallyAllTarget);
        return Task.CompletedTask;
    }

    public override Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        _concentratedAttacks.Remove(command);
        _activeAttacks.Remove(command);
        return Task.CompletedTask;
    }

    public decimal ModifyDamageMultiplicative(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource)
    {
        if (dealer != Owner ||
            target?.Side == Owner.Side ||
            !Owner.HasPower<ReptureBowPower>())
        {
            return 1m;
        }

        AttackCommand activeAttack = _activeAttacks.LastOrDefault();
        int targetCount = activeAttack != null &&
                          _concentratedAttacks.TryGetValue(
                              activeAttack,
                              out ConcentratedAttackState state)
            ? state.TargetCount
            : GetIntendedTargetCount();
        return targetCount <= 1
            ? 1m
            : targetCount * MultiTargetDamageMultiplier;
    }

    internal bool TryConcentrateBowAttack(
        AttackCommand command,
        bool wasNaturallyAllTarget)
    {
        if (command?.Attacker != Owner ||
            !Owner.HasPower<ReptureBowPower>())
        {
            return false;
        }

        if (_concentratedAttacks.ContainsKey(command))
        {
            return true;
        }

        List<Creature> targets = Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature => creature.IsHittable)
            .ToList();
        Creature mainTarget =
            SingleTargetField.GetValue(command) as Creature;
        if (mainTarget?.IsHittable != true ||
            mainTarget.Side == Owner.Side)
        {
            mainTarget = targets.FirstOrDefault();
        }

        if (mainTarget == null)
        {
            return false;
        }

        int targetCount = targets.Count;
        _concentratedAttacks.Add(
            command,
            new ConcentratedAttackState(
                targetCount,
                wasNaturallyAllTarget));
        _activeAttacks.Add(command);
        RetargetTo(command, mainTarget);
        Flash();
        return true;
    }

    internal bool WasNaturallyAllTarget(
        AttackCommand command,
        bool fallback)
    {
        return _concentratedAttacks.TryGetValue(
            command,
            out ConcentratedAttackState state)
                ? state.WasNaturallyAllTarget
                : fallback;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _concentratedAttacks = new();
        _activeAttacks = new();
    }

    private int GetIntendedTargetCount()
    {
        return Owner.CombatState?.GetOpponentsOf(Owner)
            .Count(creature => creature.IsHittable) ?? 0;
    }

    private static void RetargetTo(
        AttackCommand command,
        Creature target)
    {
        SingleTargetField.SetValue(command, null);
        CombatStateField.SetValue(command, null);
        IsRandomlyTargetedField.SetValue(command, false);
        command.Targeting(target);
    }

    private readonly record struct ConcentratedAttackState(
        int TargetCount,
        bool WasNaturallyAllTarget);
}
