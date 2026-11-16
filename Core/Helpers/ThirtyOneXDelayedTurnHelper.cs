using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyOneXDelayedTurnHelper
{
    // Native ordinary turns call each power.AfterEnergyReset. Inserted turns
    // call only this narrow list, after SetEnergy, without replaying all hooks.
    internal static async Task AfterEnergyReset(PlayerChoiceContext context, Player owner)
    {
        if (owner?.Creature.IsAlive != true || !HbrBattleStatsHelper.IsEnabled(owner)) return;
        foreach (var power in owner.Creature.Powers.OfType<ThirtyOneXDelayedTurnPower>().ToArray())
            await power.Advance(context);
    }
}
