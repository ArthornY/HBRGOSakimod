using System.Linq;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class MindsEyePower : HbrEffectPower
{
    internal const decimal BasePercent = 42m;
    internal object ConsumedByPlay;
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
    internal override decimal NativeApplicationBasis(decimal amount) => BasePercent;
    internal override decimal LegacyPotency => ResolvePotency(BasePercent, Applier ?? Owner, Owner);
    internal override int NativeApplicationRemaining(decimal amount) => 1;
    internal override decimal ResolvePotency(decimal basis, Creature applier, Creature target) =>
        HbrEffectFormula.Buff(basis, applier, HbrEffectFormula.Intelligence);
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => ImageHelper.GetImagePath("powers/tiny_moments_power.png");

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        dealer == Owner && props.IsPoweredAttack() && Amount > 0 &&
        ReferenceEquals(this, Owner.Powers.OfType<MindsEyePower>().FirstOrDefault(power => power.Amount > 0))
            ? 1m + MindsEyeCombatHelper.GetActualPercent(Owner, cardSource) / 100m : 1m;

    protected override void AfterCloned()
    { base.AfterCloned(); ConsumedByPlay = null; }
}
