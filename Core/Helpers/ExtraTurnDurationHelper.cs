using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ExtraTurnDurationHelper
{
    public static bool ShouldSuppressDurationTick(Creature owner)
    {
        // Preserve Shadow Dance's original protection, including custom turn-end costs.
        // An owner alone cannot distinguish an OD buff duration from a debuff or cost.
        Player player = owner?.Player ?? owner?.PetOwner;
        return player != null &&
            CombatManager.Instance.PlayersTakingExtraTurn.Contains(player) &&
            player.Creature.HasPower<ShadowDanceExtraTurnPower>();
    }

    public static bool ShouldSuppressDurationTick(PowerModel power)
    {
        if (power == null)
        {
            return false;
        }

        if (ShouldSuppressDurationTick(power.Owner))
        {
            return true;
        }

        // OD protects only positive durations; trigger-based stack consumption still runs.
        // Pets share the protection of their participating summoner.
        Player player = power.Owner?.Player ?? power.Owner?.PetOwner;
        return power.Type == PowerType.Buff &&
            player != null &&
            OverclockImmediateTurnCoordinator.IsActiveFor(player);
    }
}
