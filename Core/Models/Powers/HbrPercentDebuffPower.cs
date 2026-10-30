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
public abstract class HbrPercentDebuffPower : PowerModel, ICustomPower
{
    private decimal? _effectPercent;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HbrModifierCount { get; protected set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string HbrEffectData
    {
        get => _effectPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        protected set => _effectPercent = decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out decimal parsed) ? Math.Max(0m, parsed) : null;
    }

    internal int ModifierAmount => HbrModifierCount > 0 ? HbrModifierCount : Math.Max(0, Amount);
    internal decimal EffectPercent => _effectPercent ??
        HbrPercentDebuffHelper.ResolvePercent(this, ModifierAmount, Applier, Owner);

    internal void Configure(decimal percent, int modifiers)
    {
        AssertMutable();
        _effectPercent = Math.Max(0m, percent);
        HbrModifierCount = Math.Max(1, modifiers);
    }

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
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
            return text;
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner == null || side != Owner.Side || !participants.Contains(Owner) ||
            Amount <= 0 || ExtraTurnDurationHelper.ShouldSuppressDurationTick(this)) return;
        SkipNextDurationTick = false;
        Flash();
        await PowerCmd.TickDownDuration(this);
    }
}

[CustomID("HBR_ATTACK_DOWN_POWER")]
public sealed class HbrAttackDownPower : HbrPercentDebuffPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_down_power.png";

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack()
            ? Math.Max(0m, 1m - HotaruUnfadingScarsPower.EnhanceEffect(Owner, EffectPercent) / 100m)
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
