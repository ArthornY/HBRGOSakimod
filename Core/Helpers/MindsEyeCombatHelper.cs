using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MindsEyeCombatHelper
{
    internal static bool HasEffectiveMindsEye(Creature creature) =>
        creature != null && (creature.GetPower<MindsEyePower>()?.Amount > 0 ||
            MoreThanOneTruthCombatHelper.IsVirtualMindsEye(creature));

    internal static int GetRollEffectMultiplier(Creature creature, CardType cardType) =>
        cardType == CardType.Attack && HasEffectiveMindsEye(creature) ? 2 : 1;
}
