using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ExtraTurnDurationHelper
{
    public static bool ShouldSuppressDurationTick(Creature owner)
    {
        if (owner?.Player == null ||
            !CombatManager.Instance.PlayersTakingExtraTurn.Contains(owner.Player))
        {
            return false;
        }

        if (owner.HasPower<ShadowDanceExtraTurnPower>())
        {
            return true;
        }

        return owner.CombatState?.Players
            .Select(player => player.GetRelic<Overclock>())
            .Any(overclock => overclock?.IsSpecialExtraTurnFor(owner.Player) == true) == true;
    }
}
