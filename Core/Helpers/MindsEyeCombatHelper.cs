using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MindsEyeCombatHelper
{
    internal static decimal GetActualPercent(Creature owner, CardModel card) => owner?.Powers
        .OfType<MindsEyePower>().Where(power => power.Amount > 0 &&
            (power.ConsumedByPlay == null || ReferenceEquals(power.ConsumedByPlay,
                CardPlayContextHelper.GetCurrentPlayToken(card))))
        .Aggregate(0m, (total, power) => DamageAmplificationLimits.AddSaturated(total, power.EffectPotency)) ?? 0m;

    internal static decimal GetVirtualPercent(Creature owner) =>
        MoreThanOneTruthCombatHelper.IsVirtualMindsEye(owner)
            ? HbrEffectFormula.Buff(MindsEyePower.BasePercent, owner, HbrEffectFormula.Intelligence) : 0m;

    internal static bool DeferConsumption(MindsEyePower power, object command)
    {
        CardModel card = command switch
        {
            AttackCommand attack => attack.ModelSource as CardModel,
            HbrEffectSkillDamageContext.CommandScope effect => effect.Intent?.Card,
            _ => null
        };
        if (CardPlayContextHelper.GetCurrentPlayToken(card) is not CardPlayContextHelper.PlayScope play)
            return false;
        if (power.ConsumedByPlay != null && !ReferenceEquals(power.ConsumedByPlay, play)) return true;
        power.ConsumedByPlay = play;
        play.MindsEyeUses.Add(power);
        return true;
    }
}
