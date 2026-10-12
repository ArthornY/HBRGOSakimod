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

    internal int GetReservedApplications(AttackCommand attack) =>
        ReferenceEquals(_reservedAttack, attack) ? _reservedAmount : 0;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public abstract string CustomPackedIconPath { get; }
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override Task BeforeAttack(AttackCommand command)
    {
        if (_reservedAttack == null && IsOwnerAttack(command) && Amount > 0)
        {
            _reservedAttack = command;
            _reservedAmount = Amount;
        }

        return Task.CompletedTask;
    }

    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        if (!IsOwnerAttack(attack) ||
            (_reservedAttack != null && !ReferenceEquals(_reservedAttack, attack)) ||
            !ReferenceEquals(Owner.Powers.OfType<MyLegendNextAttackBasePower>().FirstOrDefault(), this))
        {
            return hitCount;
        }

        // As with PhantomHelper, add extra segments before applying doublings,
        // regardless of which type of buff was granted first.
        foreach (MyLegendNextAttackBasePower power in Owner.Powers
                     .OfType<MyLegendNextAttackBasePower>()
                     .OrderBy(power => power is MyLegendNextAttackDoublePower ? 1 : 0))
        {
            int applications = power._reservedAttack == null ? power.Amount :
                ReferenceEquals(power._reservedAttack, attack) ? power._reservedAmount : 0;
            hitCount = power.TransformHitCount(hitCount, applications);
        }

        return hitCount;
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (!ReferenceEquals(command, _reservedAttack)) return;

        int consumed = _reservedAmount;
        _reservedAttack = null;
        _reservedAmount = 0;
        if (consumed <= 0 || !Owner.Powers.Contains(this)) return;

        Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -consumed, null, null);
    }

    internal abstract int TransformHitCount(int hitCount, int applications);

    private bool IsOwnerAttack(AttackCommand command) =>
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
