using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

[CustomID("NIINA_MAKEUP_POWER")]
public sealed class NiinaMakeupPower : HbrEffectPower
{
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_attack_up_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Only, HbrEffectExit.Eternal, 1);
}

[CustomID("NIINA_NEXT_DESTRUCTION_POWER")]
public sealed class NiinaNextDestructionPower : HbrEffectPower
{
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/disabled_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.Count, 1);
}
