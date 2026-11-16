using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace HBRGOSakiMod.Core.Models.Powers;

// Potency is the flat stat gain snapshotted from current vitality at grant.
// It is intentionally not re-evaluated from INT or the holder's later stats.
[CustomID("ICHIKO_VITALITY_UP_POWER")]
public sealed class IchikoVitalityUpPower : HbrEffectPower
{
    public override PowerType Type => PowerType.Buff;
    public override string CustomPackedIconPath => "res://images/powers/hbr_defense_up_power.png";
    internal override HbrEffectProfile EffectProfile => new(HbrEffectLimit.Default, HbrEffectExit.EnemyTurnEnd, 2);
}
