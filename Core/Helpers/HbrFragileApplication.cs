using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Keep the established Fragile API while every percentage debuff shares the
// same receiver hooks, replacement policy, copy and transformation lifecycle.
internal static class HbrFragileApplication
{
    internal static FragilePower Replacement(Creature target, decimal percent) =>
        (FragilePower)HbrPercentDebuffApplication.Replacement(ModelDb.Power<FragilePower>(), target, percent);

    internal static Task Apply(PlayerChoiceContext context, FragilePower power, Creature target,
        decimal modifiers, Creature applier, CardModel source, bool silent,
        decimal? resolvedPercent = null, int copiedModifiers = 0) =>
        HbrPercentDebuffApplication.Apply(context, power, target, modifiers, applier, source, silent, resolvedPercent, copiedModifiers);

    internal static Task DoubleExisting(PlayerChoiceContext context, FragilePower power,
        decimal percentOffset, int durationOffset, Creature applier, CardModel source,
        bool doubleDuration = true, bool silent = false) =>
        HbrPercentDebuffApplication.DoubleExisting(context, power, percentOffset, durationOffset, applier, source, doubleDuration, silent);

    internal static Task<int> Copy(PlayerChoiceContext context, FragilePower original,
        Creature applier, CardModel source, bool silent) =>
        HbrPercentDebuffApplication.Copy(context, original, applier, source, silent);
}
