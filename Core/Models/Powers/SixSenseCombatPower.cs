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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class SixSenseCombatPower : PowerModel
{
    internal const decimal MaximumMentalStrengthDamageReductionPercent = 50m;

    // Keep the saved accumulator; each active effect has its own visible power.
    protected override bool IsVisibleInternal => false;
    public override bool ShouldPlayVfx => false;
    // Fixed-point storage preserves fractional levels through saves and repeated gains.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LeadershipMilli { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int GentlenessMilli { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MentalStrengthMilli { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ResistanceBreaks { get; private set; }

    internal void RegisterResistanceBreaks(int count)
    {
        AssertMutable();
        ResistanceBreaks = (int)Math.Min(int.MaxValue, (long)ResistanceBreaks + Math.Max(0, count));
    }

    public decimal CombatRollBonus => GentlenessMilli / 1000m;
    public decimal PendingLeadershipBonus => LeadershipMilli / 1000m;
    public decimal DamageReductionPercent => Math.Min(
        MaximumMentalStrengthDamageReductionPercent, MentalStrengthMilli / 1000m);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Color AmountLabelColor => new("EF87AD");

    public override LocString Description
    {
        get
        {
            LocString text = base.Description;
            text.Add("leadership", PendingLeadershipBonus);
            text.Add("gentleness", CombatRollBonus);
            text.Add("mentalStrength", DamageReductionPercent);
            return text;
        }
    }

    internal void AddEffect(SixSenseAttribute attribute, decimal value)
    {
        AssertMutable();
        int Add(int current) => (int)Math.Clamp(current + decimal.Floor(value * 1000m), 0m, int.MaxValue);
        switch (attribute)
        {
            case SixSenseAttribute.LeadershipCharm: LeadershipMilli = Add(LeadershipMilli); break;
            case SixSenseAttribute.Gentleness: GentlenessMilli = Add(GentlenessMilli); break;
            case SixSenseAttribute.MentalStrength: MentalStrengthMilli = Add(MentalStrengthMilli); break;
        }
        InvokeDisplayAmountChanged();
        foreach (SixSenseEffectPower power in Owner?.Powers.OfType<SixSenseEffectPower>() ?? Enumerable.Empty<SixSenseEffectPower>())
            power.RefreshAmount();
    }

    internal void ClearEffect(SixSenseAttribute attribute)
    {
        AssertMutable();
        switch (attribute)
        {
            case SixSenseAttribute.LeadershipCharm: LeadershipMilli = 0; break;
            case SixSenseAttribute.Gentleness: GentlenessMilli = 0; break;
            case SixSenseAttribute.MentalStrength: MentalStrengthMilli = 0; break;
        }
    }

    internal async Task SyncEffectPowers(PlayerChoiceContext context)
    {
        await Sync<SixSenseLeadershipPower>(context, PendingLeadershipBonus);
        await Sync<SixSenseGentlenessPower>(context, CombatRollBonus);
        await Sync<SixSenseMentalStrengthPower>(context, DamageReductionPercent);
    }

    private async Task Sync<T>(PlayerChoiceContext context, decimal value) where T : SixSenseEffectPower
    {
        T power = Owner.GetPower<T>();
        if (value <= 0m)
        {
            if (power != null) await PowerCmd.Remove(power);
        }
        else if (power == null)
        {
            await PowerCmd.Apply<T>(context, Owner, 1m, Owner, null);
        }
        else power.RefreshAmount();
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        foreach (SixSenseEffectPower power in oldOwner.Powers.OfType<SixSenseEffectPower>().ToArray())
            await PowerCmd.Remove(power);
    }

    public override Task AfterCreatureAddedToCombat(Creature creature) => creature == Owner
        ? SyncEffectPowers(new ThrowingPlayerChoiceContext()) : Task.CompletedTask;

    internal decimal TakeLeadershipBonus()
    {
        AssertMutable();
        decimal result = PendingLeadershipBonus;
        LeadershipMilli = 0;
        InvokeDisplayAmountChanged();
        return result;
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        target == Owner && !props.HasFlag(ValueProp.Unblockable)
            ? Math.Max(0m, 1m - DamageReductionPercent / 100m) : 1m;

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            AssertMutable();
            MentalStrengthMilli = 0;
            InvokeDisplayAmountChanged();
        }
        await SyncEffectPowers(context);
    }
}
