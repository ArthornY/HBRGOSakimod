using System.Linq;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HBR_CRITICAL_RATE_UP_POWER")]
public sealed class HbrCriticalRateUpPower : HbrEffectPower
{
    internal override HbrEffectProfile EffectProfile =>
        new(HbrEffectLimit.Only, HbrEffectExit.PlayerTurnEnd, 3);
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    internal override decimal ResolvePotency(decimal basis, Creature applier, Creature target) =>
        CalculatePercent(basis, applier);
    internal static decimal CalculatePercent(decimal basis, Creature source) =>
        HbrEffectFormula.Buff(basis, source, HbrEffectFormula.Intelligence);
    internal static decimal GetTotalPercent(Creature holder) => holder?.Powers
        .OfType<HbrCriticalRateUpPower>().Where(power => power.Amount > 0)
        .Aggregate(0m, (total, power) => DamageAmplificationLimits.AddSaturated(total, power.EffectPotency)) ?? 0m;
}

[CustomID("HBR_CRITICAL_DAMAGE_UP_POWER")]
public sealed class HbrCriticalDamageUpPower : HbrEffectPower
{
    internal override HbrEffectProfile EffectProfile =>
        new(HbrEffectLimit.Only, HbrEffectExit.PlayerTurnEnd, 3);
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    internal static decimal GetTotalPercent(Creature holder) => holder?.Powers
        .OfType<HbrCriticalDamageUpPower>().Where(power => power.Amount > 0)
        .Aggregate(0m, (total, power) => DamageAmplificationLimits.AddSaturated(total, power.EffectPotency)) ?? 0m;
    internal static decimal GetMultiplier(Creature holder) =>
        HbrOriginalNumericFormula.CriticalMultiplier(GetTotalPercent(holder)) /
        HbrOriginalNumericFormula.CriticalMultiplier();
}
