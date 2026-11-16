using BaseLib.Utils.Attributes;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Powers;

// A separate application type gives this DefenseDown variant its own two
// independent slots. Its active DP-only offset joins ordinary DefenseDown.
[CustomID("HBR_DP_DEFENSE_DOWN_POWER")]
public sealed class HbrDpDefenseDownPower : HbrPercentDebuffPower
{
    public override string CustomPackedIconPath => "res://images/powers/hbr_defense_down_power.png";

    public decimal ModifyDamageMultiplicative(Creature target, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource) =>
        Amount > 0 && target == Owner && props.IsPoweredAttack() &&
        !HpOnlyDamageContext.BypassesDpForBlockReceiver(target) &&
        (target.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m ||
         target.Player?.GetRelic<DeflectorProtection>()?.Dp > 0)
            ? DamageAmplificationLimits.AddSaturated(1m,
                HotaruUnfadingScarsPower.EnhanceEffect(Owner, EffectPercent) / 100m)
            : 1m;
}
