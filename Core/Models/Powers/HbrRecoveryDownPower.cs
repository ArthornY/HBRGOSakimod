using System;
using System.Linq;
using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HBR_RECOVERY_DOWN_POWER")]
public sealed class HbrRecoveryDownPower : HbrEffectPower
{
    public override PowerType Type => PowerType.Debuff;
    public override string CustomPackedIconPath => "res://images/powers/disabled_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.None, HbrEffectExit.Eternal, 1);
    internal override bool CanApplyTo(Creature target) => EnemyStatScalingHelper.IsEnemy(target);
    internal static decimal Factor(Creature target) => target?.Powers.OfType<HbrRecoveryDownPower>()
        .Where(power => power.Amount > 0).Aggregate(1m, (factor, power) => factor * Math.Max(0m, 1m - power.EffectPotency / 100m)) ?? 1m;
}
