using System;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Models.Powers;

// One native instance per distinct value. Amount is V; the separately saved N
// is never fed through roll-based power-amount modifiers or doubled as a stack.
public abstract class CountedRollPower : PowerModel, ICustomPower
{
    private int _reservedRolls;
    private CountedRollPower _mergedInto;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RemainingUses { get; protected set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override bool AllowNegative => true;
    public override Color AmountLabelColor => Amount < 0 ? new Color("FF7070") : new Color("F8D36A");
    public abstract string CustomPackedIconPath { get; }
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    internal int AvailableUses => Math.Max(0, RemainingUses - _reservedRolls);
    internal int ReservedRollCount => _reservedRolls;

    public override LocString Description => Describe(".description");
    internal LocString CompactDescription => Describe(".compactDescription");

    private LocString Describe(string suffix)
    {
        if (!IsMutable || Owner == null)
            return new LocString("powers", Id.Entry + ".referenceDescription");
        LocString text = LocString.GetIfExists("powers", Id.Entry + suffix) ??
            new LocString("powers", Id.Entry + ".description");
        text.Add("Value", Amount);
        // Reservations are spent in the logical roll, even when native card
        // cleanup is the safe place to remove an exhausted model afterwards.
        text.Add("RemainingUses", IsMutable ? AvailableUses : 0);
        return text;
    }

    internal void ConfigureUses(int uses)
    {
        AssertMutable();
        RemainingUses = Math.Max(0, uses);
        _reservedRolls = 0;
        _mergedInto = null;
    }

    internal bool TryReserveRoll()
    {
        CountedRollPower current = ResolveMergedInstance();
        if (!ReferenceEquals(current, this)) return current.TryReserveRoll();
        if (Amount == 0 || AvailableUses <= 0 || Owner == null || !Owner.Powers.Contains(this)) return false;
        AssertMutable();
        _reservedRolls++;
        NotifyUsesChanged();
        return true;
    }

    internal Task ConsumeReservedRolls(PlayerChoiceContext context, CardModel source) =>
        ConsumeReservedRolls(context, source, ResolveMergedInstance()._reservedRolls);

    internal async Task ConsumeReservedRolls(PlayerChoiceContext context, CardModel source, int requestedCount)
    {
        CountedRollPower current = ResolveMergedInstance();
        if (!ReferenceEquals(current, this))
        {
            await current.ConsumeReservedRolls(context, source, requestedCount);
            return;
        }
        if (requestedCount <= 0 || _reservedRolls <= 0 || Owner == null || !Owner.Powers.Contains(this)) return;
        int spent = Math.Min(requestedCount, Math.Min(RemainingUses, _reservedRolls));
        if (spent <= 0) return;
        AssertMutable();
        _reservedRolls -= spent;
        RemainingUses -= spent;
        NotifyUsesChanged();
        if (RemainingUses <= 0) await PowerCmd.Remove(this);
    }

    internal CountedRollPower ResolveMergedInstance()
    {
        CountedRollPower current = this;
        while (current._mergedInto != null) current = current._mergedInto;
        return current;
    }

    internal void Absorb(CountedRollPower other)
    {
        AssertMutable();
        other.AssertMutable();
        RemainingUses = (int)Math.Min(int.MaxValue, (long)RemainingUses + other.RemainingUses);
        _reservedRolls = (int)Math.Min(RemainingUses, (long)_reservedRolls + other._reservedRolls);
        // A token reserved before an instance merge still consumes exactly its
        // own use from the surviving group, rather than silently disappearing.
        other._mergedInto = this;
        other._reservedRolls = 0;
        NotifyUsesChanged();
    }

    private void NotifyUsesChanged()
    {
        InvokeDisplayAmountChanged();
        Owner?.InvokePowerModified(this, 0, silent: true);
    }

    public override Task AfterApplied(Creature applier, CardModel cardSource) =>
        HbrRollPowerHelper.MergeSameValues(new ThrowingPlayerChoiceContext(), Owner);

    public override Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power,
        decimal amount, Creature applier, CardModel cardSource) =>
        ReferenceEquals(power, this)
            ? HbrRollPowerHelper.MergeSameValues(context, Owner)
            : Task.CompletedTask;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _reservedRolls = 0;
        _mergedInto = null;
    }
}

public sealed class RollBonusPower : CountedRollPower
{
    public override string CustomPackedIconPath => "res://images/powers/next_roll_bonus_power.png";

    public static Task Apply(PlayerChoiceContext context, Creature target, int value, int uses,
        Creature applier, CardModel cardSource, bool silent = false) =>
        HbrRollPowerHelper.Apply<RollBonusPower>(context, target, value, uses, applier, cardSource, silent);
}

public sealed class AdvantageDicePower : CountedRollPower
{
    public override string CustomPackedIconPath => "res://images/powers/strengthen_power.png";

    public static Task Apply(PlayerChoiceContext context, Creature target, int value, int uses,
        Creature applier, CardModel cardSource, bool silent = false) =>
        value <= 0 ? Task.CompletedTask :
            HbrRollPowerHelper.Apply<AdvantageDicePower>(context, target, value, uses, applier, cardSource, silent);
}
