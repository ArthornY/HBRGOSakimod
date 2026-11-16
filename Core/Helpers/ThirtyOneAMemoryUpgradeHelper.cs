using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// The six garden styles share the same original-game critical grant. Keep its
// basis, duration and source attribution identical to Shooting Star Song.
internal static class ThirtyOneAMemoryUpgradeHelper
{
    internal static async Task GrantCriticals(PlayerChoiceContext context, Creature target,
        Creature applier, CardModel source)
    {
        if (target?.IsAlive != true) return;
        await HbrEffectApplication.Apply<HbrCriticalRateUpPower>(context, target, 50m, applier, source, 3);
        await HbrEffectApplication.Apply<HbrCriticalDamageUpPower>(context, target, 50m, applier, source, 3);
    }
}
