using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

/// <summary>Reserves all existing stacks for one attack, including all its targets.</summary>
public abstract class MyLegendNextAttackBasePower : PowerModel, ICustomPower
{
    internal const int MaximumHits = 1000;

    private AttackCommand _reservedAttack;
    private int _reservedAmount;

    internal AttackCommand ReservedAttack => _reservedAttack;

    internal virtual int ApplicationsPerAttack => Math.Max(0, Amount);
    internal virtual bool DoublesHitCount => false;
    internal virtual bool RequiresActualHit => false;

    internal int GetReservedApplications(AttackCommand attack) =>
        ReferenceEquals(_reservedAttack, attack) ? _reservedAmount : 0;

    internal void ReleaseReservation(AttackCommand attack)
    {
        if (!ReferenceEquals(_reservedAttack, attack)) return;
        _reservedAttack = null;
        _reservedAmount = 0;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public abstract string CustomPackedIconPath { get; }
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override Task BeforeAttack(AttackCommand command)
    {
        ReserveForAttack(command);
        return Task.CompletedTask;
    }

    internal void ReserveForAttack(AttackCommand command)
    {
        if (_reservedAttack == null && IsOwnerAttack(command) && Amount > 0)
        {
            _reservedAttack = command;
            _reservedAmount = ApplicationsPerAttack;
        }
    }

    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        if (!IsOwnerAttack(attack) ||
            (_reservedAttack != null && !ReferenceEquals(_reservedAttack, attack)) ||
            !ReferenceEquals(Owner.Powers.OfType<MyLegendNextAttackBasePower>()
                .FirstOrDefault(power => power.IsOwnerAttack(attack) &&
                    (power._reservedAttack == null || ReferenceEquals(power._reservedAttack, attack))), this))
        {
            return hitCount;
        }

        // As with PhantomHelper, add extra segments before applying doublings,
        // regardless of which type of buff was granted first.
        foreach (MyLegendNextAttackBasePower power in Owner.Powers
                     .OfType<MyLegendNextAttackBasePower>()
                     .Where(power => power.IsOwnerAttack(attack))
                     .OrderBy(power => power.DoublesHitCount)
                     .ThenBy(power => power is HbrComboPower ? 1 : 0))
        {
            int applications = power._reservedAttack == null ? power.ApplicationsPerAttack :
                ReferenceEquals(power._reservedAttack, attack) ? power._reservedAmount : 0;
            hitCount = power.TransformHitCount(hitCount, applications);
        }

        return hitCount;
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command) =>
        CompleteReservation(choiceContext, command, command.Results.Any(results => results.Count > 0));

    internal async Task CompleteReservation(PlayerChoiceContext choiceContext, AttackCommand command, bool hadHit)
    {
        if (!ReferenceEquals(command, _reservedAttack)) return;

        int consumed = _reservedAmount;
        ReleaseReservation(command);
        if (consumed <= 0 || RequiresActualHit && !hadHit || !Owner.Powers.Contains(this)) return;

        Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -consumed, null, null);
    }

    internal abstract int TransformHitCount(int hitCount, int applications);

    protected virtual bool IsOwnerAttack(AttackCommand command) =>
        command?.Attacker == Owner && command.DamageProps.HasFlag(ValueProp.Move);

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedAttack = null;
        _reservedAmount = 0;
    }
}

public sealed class MyLegendNextAttackPower : MyLegendNextAttackBasePower
{
    public override string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_next_attack_bonus_segments_power.tres";

    internal override int TransformHitCount(int hitCount, int applications) =>
        applications <= 0 ? hitCount : (int)Math.Min(
            Math.Max(MaximumHits, hitCount), (long)Math.Max(0, hitCount) + applications);
}

public sealed class MyLegendNextAttackDoublePower : MyLegendNextAttackBasePower
{
    internal override bool DoublesHitCount => true;

    public override string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phantom_next_attack_double_power.tres";

    internal override int TransformHitCount(int hitCount, int applications)
    {
        if (applications <= 0 || hitCount <= 0) return hitCount;
        int limit = Math.Max(MaximumHits, hitCount);
        for (int index = 0; index < applications && hitCount < limit; index++)
            hitCount = (int)Math.Min(limit, (long)hitCount * 2);
        return hitCount;
    }
}
