using System;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Powers;

public abstract class SixSenseEffectPower : PowerModel, ICustomPower
{
    private decimal _displayedValue;
    protected abstract SixSenseAttribute Attribute { get; }
    protected abstract decimal GetValue(SixSenseCombatPower state);

    public decimal Value => IsMutable && Owner?.GetPower<SixSenseCombatPower>() is { } state
        ? GetValue(state) : 0m;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool ShouldPlayVfx => false;
    public abstract string CustomPackedIconPath { get; }
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
    protected override bool IsVisibleInternal => !IsMutable || Value > 0m;
    public override int DisplayAmount => (int)decimal.Floor(Value);

    internal void RefreshAmount()
    {
        if (_displayedValue == Value) return;
        _displayedValue = Value;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource)
    {
        _displayedValue = Value;
        return Task.CompletedTask;
    }

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("value", Value);
            return text;
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        oldOwner.GetPower<SixSenseCombatPower>()?.ClearEffect(Attribute);
        return Task.CompletedTask;
    }
}

public sealed class SixSenseLeadershipPower : SixSenseEffectPower
{
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/six_sense_leadership_power.tres";
    protected override SixSenseAttribute Attribute => SixSenseAttribute.LeadershipCharm;
    protected override decimal GetValue(SixSenseCombatPower state) => state.PendingLeadershipBonus;
    public override Color AmountLabelColor => new("EF777F");
}

public sealed class SixSenseGentlenessPower : SixSenseEffectPower
{
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/six_sense_gentleness_power.tres";
    protected override SixSenseAttribute Attribute => SixSenseAttribute.Gentleness;
    protected override decimal GetValue(SixSenseCombatPower state) => state.CombatRollBonus;
    public override Color AmountLabelColor => new("78DAA4");
}

public sealed class SixSenseMentalStrengthPower : SixSenseEffectPower
{
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/six_sense_mental_strength_power.tres";
    protected override SixSenseAttribute Attribute => SixSenseAttribute.MentalStrength;
    protected override decimal GetValue(SixSenseCombatPower state) => state.DamageReductionPercent;
    public override Color AmountLabelColor => new("6CDAF5");
}
