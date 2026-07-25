using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ExtraTurnDurationHelper
{
    public static bool ShouldSuppressDurationTick(Creature owner)
    {
        return owner?.Player != null &&
            owner.HasPower<ShadowDanceExtraTurnPower>() &&
            CombatManager.Instance.PlayersTakingExtraTurn.Contains(owner.Player);
    }
}
