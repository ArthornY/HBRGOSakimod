using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PillarOverdriveDetectionPower : PowerModel, ICustomPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override int DisplayAmount => 0;
    public string CustomPackedIconPath => "res://images/powers/pillar_overdrive_detection_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource) => dealer == Owner && props.IsPoweredAttack() &&
        Owner?.Monster is PillarGuardian && EnemyStatScalingHelper.IsEnemy(Owner) && HbrBattleStatsHelper.IsEnabled(Owner) &&
        target?.IsPlayer == true && target.Side != Owner.Side
            ? 1m + .5m * (target.Player.GetRelic<Overclock>()?.Level ?? 0) : 1m;
    public override LocString Description => new("powers", Id.Entry + ".description");
}

public sealed class BarrelDemiseCountdownPower : PowerModel, ICustomPower, IHighDimensionalShellObserver
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool SkipTurnEnd { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/powers/barrel_demise_countdown_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    private bool Active => Owner?.Monster is BarrelWalker && EnemyStatScalingHelper.IsEnemy(Owner) && HbrBattleStatsHelper.IsEnabled(Owner);
    internal bool IsDue => Active && !Owner.HasPower<BreakPower>() && Amount <= 1;
    public override LocString Description
    {
        get { LocString text = new("powers", Id.Entry + (!IsMutable || Owner == null ? ".referenceDescription" : ".description")); text.Add("Remaining", IsMutable ? Math.Clamp(Amount, 1, 4) : 4); return text; }
    }
    internal void ResetAfterCannon() { AssertMutable(); SetAmount(4); SkipTurnEnd = true; Owner?.InvokePowerModified(this, 0, true); }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!Active || side != CombatSide.Enemy || Owner.HasPower<BreakPower>()) return Task.CompletedTask;
        if (SkipTurnEnd) SkipTurnEnd = false; else SetAmount(Amount > 1 ? Amount - 1 : 4);
        Owner.InvokePowerModified(this, 0, true); HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); return Task.CompletedTask;
    }
    public void OnHighDimensionalShellApplied(HighDimensionalShellPower shell) { if (Active) HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); }
    public void OnHighDimensionalShellAmountChanged(decimal before, decimal current, decimal maximum) { }
    public Task AfterHighDimensionalShellBreak(PlayerChoiceContext context)
    {
        if (Active) { SetAmount(4); SkipTurnEnd = false; Owner.InvokePowerModified(this, 0, true); HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); }
        return Task.CompletedTask;
    }
}

public sealed class DiamondDpCheckPower : PowerModel, ICustomPower, IHighDimensionalShellObserver
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ExplosionPending { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public string CustomPackedIconPath => "res://images/powers/diamond_dp_check_power.png";
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    private bool Active => Owner?.Monster is DiamondEyeball && EnemyStatScalingHelper.IsEnemy(Owner) && HbrBattleStatsHelper.IsEnabled(Owner);
    internal bool CanCheck => Active && !Owner.HasPower<BreakPower>() && Owner.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m;
    public override LocString Description
    {
        get { LocString text = new("powers", Id.Entry + (!IsMutable || Owner == null ? ".referenceDescription" : ".description")); text.Add("Remaining", IsMutable ? Math.Clamp(Amount, 1, 4) : 3); return text; }
    }
    internal void BeginExplosion() { AssertMutable(); ExplosionPending = true; Owner?.InvokePowerModified(this, 0, true); }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!Active || side != CombatSide.Enemy) return Task.CompletedTask;
        if (Amount == 4) ExplosionPending = false;
        SetAmount(Amount > 1 ? Amount - 1 : 4);
        Owner.InvokePowerModified(this, 0, true); HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); return Task.CompletedTask;
    }
    public void OnHighDimensionalShellApplied(HighDimensionalShellPower shell) { if (Active) HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); }
    public void OnHighDimensionalShellAmountChanged(decimal before, decimal current, decimal maximum) { if (Active) HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); }
    public Task AfterHighDimensionalShellBreak(PlayerChoiceContext context)
    {
        if (Active) { ExplosionPending = false; Owner.InvokePowerModified(this, 0, true); HbrEnemyFlavorSchedulingHelper.RefreshOwner(Owner); }
        return Task.CompletedTask;
    }
}

[CustomID("SHRIMP_RUNAWAY_POWER")]
public sealed class ShrimpRunawayPower : HbrAttackUpPower
{
    public override string CustomPackedIconPath => "res://images/powers/shrimp_runaway_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Only, HbrEffectExit.Eternal, 1);
}

public sealed class HbrProvokePower : HbrEffectPower
{
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_provoke_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2);
    internal override bool CanApplyTo(Creature target) => target != null && EnemyStatScalingHelper.IsEnemy(target) && HbrBattleStatsHelper.IsEnabled(target);
}
