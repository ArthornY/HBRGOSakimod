using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
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

// These are independent HBR abilities. Native Weak/Vulnerable continue to own
// their original duration, stacking, damage hooks and item interactions.
public abstract class HbrPercentDebuffPower : PowerModel, ICustomPower, IHbrEffectInstance
{
    private decimal? _effectPercent;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrModifierCount { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HbrUnlimitedInstances { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrExitOverride { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrLimitOverride { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrDefaultRemainingOverride { get; protected set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HbrWaitForOwnerTurnStart { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrEffectData
    {
        get => _effectPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        protected set => _effectPercent = decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;
    }

    internal int ModifierAmount => HbrModifierCount > 0 ? HbrModifierCount : Math.Max(0, Amount);
    internal decimal EffectPercent => _effectPercent ??
        HbrPercentDebuffHelper.ResolvePercent(this, ModifierAmount, Applier, Owner);

    internal void Configure(decimal percent, int modifiers)
    {
        AssertMutable();
        _effectPercent = percent;
        HbrModifierCount = Math.Max(1, modifiers);
    }
    internal void SetUnlimited(bool unlimited) { AssertMutable(); HbrUnlimitedInstances = unlimited; }
    internal void WaitForNextOwnerTurn() { AssertMutable(); HbrWaitForOwnerTurnStart = true; }
    internal void ConfigureProfile(HbrEffectProfile profile)
    {
        AssertMutable(); HbrExitOverride = (int)profile.Exit + 1; HbrLimitOverride = (int)profile.Limit + 1;
        HbrDefaultRemainingOverride = profile.DefaultRemaining; HbrUnlimitedInstances = profile.Limit == HbrEffectLimit.None;
    }
    internal HbrEffectProfile CurrentEffectProfile => new(
        HbrUnlimitedInstances ? HbrEffectLimit.None : HbrLimitOverride > 0 ? (HbrEffectLimit)(HbrLimitOverride - 1) : HbrEffectLimit.Default,
        HbrExitOverride > 0 ? (HbrEffectExit)(HbrExitOverride - 1) : HbrEffectExit.EnemyTurnEnd,
        HbrDefaultRemainingOverride > 0 ? HbrDefaultRemainingOverride : 1);

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    decimal IHbrEffectInstance.EffectPotency => EffectPercent;
    int IHbrEffectInstance.Remaining => Amount;
    HbrEffectProfile IHbrEffectInstance.EffectProfile => CurrentEffectProfile;
    public abstract string CustomPackedIconPath { get; }
    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;

    public override LocString Description
    {
        get
        {
            if (!IsMutable || Owner == null)
                return new LocString("powers", Id.Entry + ".hbrReferenceDescription");
            LocString text = new("powers", Id.Entry + ".description");
            text.Add("EffectPercent", HotaruUnfadingScarsPower.EnhanceEffect(Owner, EffectPercent)
                .ToString("0.#", CultureInfo.InvariantCulture));
            text.Add("ModifierCount", ModifierAmount);
            text.Add("Turns", Math.Max(0, Amount));
            text.Add("Duration", HbrEffectProfileResolver.Duration(CurrentEffectProfile, Amount));
            text.Add("LifetimeRule", HbrEffectProfileResolver.LifetimeRule(CurrentEffectProfile, Amount));
            text.Add("SlotRule", HbrEffectProfileResolver.SlotRule(CurrentEffectProfile));
            return text;
        }
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner != null && side == Owner.Side &&
            (participants.Contains(Owner) || Owner.PetOwner is { } owner && participants.Contains(owner.Creature)))
            HbrWaitForOwnerTurnStart = false;
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        SkipNextDurationTick = false;
        return HbrWaitForOwnerTurnStart ? Task.CompletedTask :
            HbrEffectRules.Tick(this, ((IHbrEffectInstance)this).EffectProfile, side, participants);
    }
}

[CustomID("HBR_ATTACK_DOWN_POWER")]
public sealed class HbrAttackDownPower : HbrPercentDebuffPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_down_power.png";

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack()
            ? 1m - HotaruUnfadingScarsPower.EnhanceEffect(Owner, EffectPercent) / 100m
            : 1m;
}

[CustomID("HBR_DEFENSE_DOWN_POWER")]
public sealed class HbrDefenseDownPower : HbrPercentDebuffPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_defense_down_power.png";

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && target == Owner && props.IsPoweredAttack()
            ? DamageAmplificationLimits.AddSaturated(1m,
                HotaruUnfadingScarsPower.EnhanceEffect(Owner, EffectPercent) / 100m)
            : 1m;
}
