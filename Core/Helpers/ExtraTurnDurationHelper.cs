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
        // Custom costs/trigger consumption are not durations. The two authored
        // sources now use personal inserted turns instead of native extra turns.
        return false;
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
        return power.TypeForCurrentAmount == PowerType.Buff &&
            player != null &&
            (OverclockImmediateTurnCoordinator.IsActiveFor(player) ||
             PersonalImmediateTurnCoordinator.IsActiveFor(player));
    }
}
