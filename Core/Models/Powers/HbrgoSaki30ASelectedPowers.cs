using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class ChargeInReserveNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || CombatManager.Instance.IsOverOrEnding) return;
        Creature owner = Owner;
        int amount = Amount;
        Flash();
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<ChargePower>(new ThrowingPlayerChoiceContext(), owner, amount, owner, null);
    }
}

public sealed class RefuseToLeavePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override LocString Description => HbrSkillBlockDescription.ForPower(this,
        base.Description, "REFUSE_TO_LEAVE_POWER.hbrDescription", IsMutable ? Amount : 0m);
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding ||
            !PhantomHelper.IsOwnedMainPhantom(target, Owner.Player) || !PhantomHelper.CountsAsFinalDefeat(target)) return;
        Flash();
        await CreatureCmd.GainBlock(Owner, HbrSkillBlockHelper.ResolveUnpowered(Owner, Amount), ValueProp.Unpowered, null);
        await PowerCmd.Apply<ChargePower>(choiceContext, Owner, 2m, Owner, null);
    }
}

public sealed class SilentOfferingPower : PowerModel
{
    private bool _triggeredThisTurn;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    internal async Task AfterPhantomNonAttack(Creature phantom)
    {
        if (_triggeredThisTurn || CombatManager.Instance.IsOverOrEnding ||
            !PhantomHelper.IsOwnedMainPhantom(phantom, Owner.Player)) return;
        AssertMutable();
        _triggeredThisTurn = true;
        Flash();
        await PowerCmd.Apply<ChargePower>(new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            AssertMutable();
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeredThisTurn = false;
    }
}

public sealed class AwaitingReturnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || CombatManager.Instance.IsOverOrEnding ||
            PhantomHelper.GetLivingPhantom(Owner.Player) != null) return;
        Flash();
        await PowerCmd.Apply<ChargePower>(new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
    }
}

public sealed class InMyNamePower : PowerModel
{
    private bool _triggeredThisTurn;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    internal async Task AfterPhantomActed(Creature phantom)
    {
        if (_triggeredThisTurn || phantom?.IsAlive != true || CombatManager.Instance.IsOverOrEnding ||
            !PhantomHelper.IsOwnedMainPhantom(phantom, Owner.Player)) return;
        AssertMutable();
        _triggeredThisTurn = true;
        Flash();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, Owner, Amount, Owner, null);
        await PowerCmd.Apply<StrengthPower>(context, phantom, Amount, Owner, null);
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            AssertMutable();
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeredThisTurn = false;
    }
}

public sealed class UnbrokenRemnantPower : PowerModel
{
    internal int StrengthGain { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override LocString Description
    {
        get
        {
            var description = base.Description;
            description.Add("StrengthGain", StrengthGain);
            description.Add("ResourceAmount", HbrResourceValueHelper.Fixed(Owner, Amount));
            return description;
        }
    }
    internal void AddStrengthGain(int amount)
    {
        AssertMutable();
        StrengthGain += amount;
    }
    internal async Task AfterPhantomDpBroken(PlayerChoiceContext choiceContext, Creature phantom)
    {
        if (phantom?.IsAlive != true || CombatManager.Instance.IsOverOrEnding ||
            !PhantomHelper.IsOwnedMainPhantom(phantom, Owner.Player) ||
            Owner.Player.GetRelic<MomentOfReign>()?.TryUsePhantomDpRecovery() != true) return;
        Flash();
        var shell = phantom.GetPower<PhantomHighDimensionalShellPower>();
        if (shell != null)
            shell.Restore(HbrResourceValueHelper.Fixed(Owner, Amount));
        else
            await PowerCmd.Apply<PhantomHighDimensionalShellPower>(choiceContext, phantom,
                Math.Min(HbrResourceValueHelper.Fixed(Owner, Amount), PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom)), Owner, null);
        await PowerCmd.Apply<StrengthPower>(choiceContext, phantom, StrengthGain, Owner, null);
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        StrengthGain = 0;
    }
}

public sealed class EnduringAfterimagePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || CombatManager.Instance.IsOverOrEnding) return Task.CompletedTask;
        Creature phantom = PhantomHelper.GetLivingPhantom(Owner.Player);
        if (phantom?.Monster?.NextMove == null || PhantomHelper.HasCurrentAttackIntent(phantom)) return Task.CompletedTask;
        var power = phantom.GetPower<PhantomPower>();
        bool copied = false;
        for (int i = 0; i < Amount; i++)
        {
            if (!power.QueueCopiedIntent(phantom.Monster.NextMove.Id)) break;
            copied = true;
        }
        if (copied) Flash();
        return Task.CompletedTask;
    }
}

public sealed class DevourRemainingLifePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("F2C15F");
}

public sealed class PainResonancePower : PowerModel
{
    private int _pendingHits;
    private bool _resolving;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    internal void QueuePhantomDamage()
    {
        if (CombatManager.Instance.IsOverOrEnding) return;
        AssertMutable();
        if (_pendingHits < int.MaxValue) _pendingHits++;
    }
    // DP absorption reports zero HP damage; the DP patch queues that hit here.
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature dealer,
        DamageResult result, ValueProp props, Creature target, CardModel cardSource)
    {
        if (Owner.Player == null || CombatManager.Instance.IsOverOrEnding) return;
        if (PhantomHelper.IsOwnedMainPhantom(target, Owner.Player) && result.UnblockedDamage > 0)
            QueuePhantomDamage();
        if (_resolving) return;
        AssertMutable();
        _resolving = true;
        try
        {
            while (_pendingHits > 0 && !CombatManager.Instance.IsOverOrEnding)
            {
                _pendingHits--;
                Creature[] targets = Owner.CombatState.GetOpponentsOf(Owner)
                    .Where(enemy => enemy.IsAlive && enemy.IsHittable).ToArray();
                if (targets.Length == 0) continue;
                Creature enemy = targets[Owner.CombatState.RunState.Rng.CombatTargets.NextInt(0, targets.Length)];
                Flash();
                await CreatureCommandCompatibility.Damage(choiceContext, enemy, Amount, ValueProp.Unpowered, Owner, null);
            }
        }
        finally { _resolving = false; }
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _pendingHits = 0;
        _resolving = false;
    }
}

public sealed class LetMeBearItPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Color AmountLabelColor => new("F2C15F");
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (Owner.Player == null || props.HasFlag(ValueProp.Unpowered) || PhantomHelper.GetLivingPhantom(Owner.Player) != null) return 1m;
        var relic = Owner.Player.GetRelic<MomentOfReign>();
        decimal multiplier = 1m;
        if (dealer == Owner)
        {
            decimal snapshot = Math.Max(0m, relic?.LastPhantomDamageBonusPercent ?? 0m);
            decimal stacks = Math.Max(0, Amount);
            decimal percent = stacks > 0m && snapshot >= DamageAmplificationLimits.MaximumTemporaryPercent / stacks
                ? DamageAmplificationLimits.MaximumTemporaryPercent
                : DamageAmplificationLimits.Clamp(snapshot * stacks);
            multiplier += percent / 100m;
        }
        if (target == Owner) multiplier -= (relic?.LastPhantomDamageReductionPercent ?? 0m) * Amount / 100m;
        return HbrgoSakiDamageMultiplierHelper.Add(Math.Max(0m, multiplier), this);
    }
    internal async Task AfterPhantomRevived(Creature phantom)
    {
        if (phantom?.IsAlive != true || !PhantomHelper.IsOwnedMainPhantom(phantom, Owner.Player) ||
            CombatManager.Instance.IsOverOrEnding) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), phantom, 4m * Amount, Owner, null);
    }
}
