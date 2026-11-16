using System.Linq;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HBR_ETERNAL_VOW_POWER")]
public sealed class HbrEternalVowPower : HbrEffectPower
{
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.Eternal, 1);
    internal override decimal LegacyPotency => 25m;
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    public decimal ModifyDamageMultiplicative(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && dealer == Owner && props.IsPoweredAttack() &&
        ReferenceEquals(this, Owner.Powers.OfType<HbrEternalVowPower>().FirstOrDefault(power => power.Amount > 0))
            ? 1m + Owner.Powers.OfType<HbrEternalVowPower>().Where(power => power.Amount > 0)
                .Aggregate(0m, (sum, power) => DamageAmplificationLimits.AddSaturated(sum, power.EffectPotency)) / 100m : 1m;
}
