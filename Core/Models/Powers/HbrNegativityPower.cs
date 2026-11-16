using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("HBR_NEGATIVITY_POWER")]
public sealed class HbrNegativityPower : HbrEffectPower
{
    public override PowerType Type => PowerType.Debuff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_calamity_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Only, HbrEffectExit.EnemyTurnEnd, 5);
}
