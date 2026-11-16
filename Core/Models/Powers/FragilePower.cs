using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
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

public sealed class FragilePower : PowerModel, IHbrEffectInstance
{
    private decimal? _hbrEffectPercent;

    // Amount is remaining turns; original modifier strength is stored separately.
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrModifierCount { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HbrUnlimitedInstances { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrExitOverride { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrLimitOverride { get; private set; }
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrDefaultRemainingOverride { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrEffectData
    {
        get => _hbrEffectPercent.HasValue
            ? _hbrEffectPercent.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        private set => _hbrEffectPercent = decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out decimal parsed) ? parsed : null;
    }

    internal int ModifierAmount => HbrModifierCount > 0 ? HbrModifierCount : Math.Max(0, Amount);
    internal decimal HbrEffectPercent => _hbrEffectPercent ??
        HbrFragilePowerHelper.ResolvePercent(ModifierAmount, Applier, Owner);

    internal void ConfigureHbr(decimal percent, int modifiers)
    {
        AssertMutable();
        _hbrEffectPercent = percent;
        HbrModifierCount = Math.Max(1, modifiers);
    }
    internal void SetUnlimited(bool unlimited) { AssertMutable(); HbrUnlimitedInstances = unlimited; }
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
    decimal IHbrEffectInstance.EffectPotency => HbrEffectPercent;
    int IHbrEffectInstance.Remaining => Amount;
    HbrEffectProfile IHbrEffectInstance.EffectProfile => CurrentEffectProfile;
    public override Color AmountLabelColor => new("DDA0DD");

    public override LocString Description
    {
        get
        {
            if (!IsMutable || Owner == null)
                return new LocString("powers", Id.Entry + ".hbrReferenceDescription");
            if (!HbrBattleStatsHelper.IsEnabled(Owner)) return base.Description;
            LocString text = new("powers", Id.Entry + ".hbrDescription");
            text.Add("FragilePercent", HotaruUnfadingScarsPower.EnhanceEffect(Owner, HbrEffectPercent)
                .ToString("0.#", CultureInfo.InvariantCulture));
            text.Add("Turns", Math.Max(0, Amount));
            text.Add("ModifierCount", ModifierAmount);
            text.Add("Duration", HbrEffectProfileResolver.Duration(CurrentEffectProfile, Amount));
            text.Add("LifetimeRule", HbrEffectProfileResolver.LifetimeRule(CurrentEffectProfile, Amount));
            text.Add("SlotRule", HbrEffectProfileResolver.SlotRule(CurrentEffectProfile));
            return text;
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (HbrBattleStatsHelper.IsEnabled(Owner))
        {
            if (!_hbrEffectPercent.HasValue)
            {
                ConfigureHbr(HbrEffectPercent, ModifierAmount);
                SetAmount(Math.Min(2, Amount), silent: true);
            }
            SkipNextDurationTick = false;
            await HbrEffectRules.Tick(this, ((IHbrEffectInstance)this).EffectProfile, side);
            return;
        }
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0 ||
            ExtraTurnDurationHelper.ShouldSuppressDurationTick(this)) return;
        if (HbrBattleStatsHelper.IsEnabled(Owner) && !_hbrEffectPercent.HasValue)
        {
            // Legacy counters migrate during lifecycle work, never preview reads.
            ConfigureHbr(HbrEffectPercent, ModifierAmount);
            SetAmount(Math.Min(2, Amount), silent: true);
        }
        if (HbrBattleStatsHelper.IsEnabled(Owner)) SkipNextDurationTick = false;
        Flash();
        await PowerCmd.TickDownDuration(this);
    }

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        if (target != Owner || amount <= 0m || !Owner.HasPower<BreakPower>()) return 1m;
        bool hbr = HbrBattleStatsHelper.IsEnabled(Owner);
        if (hbr && Amount <= 0) return 1m;
        decimal effect = hbr ? HbrEffectPercent / 100m : 0.5m;
        decimal multiplier = DamageAmplificationLimits.AddSaturated(1m,
            HotaruUnfadingScarsPower.EnhanceEffect(Owner, effect));
        return HbrgoSakiDamageMultiplierHelper.Add(multiplier, this);
    }
}
