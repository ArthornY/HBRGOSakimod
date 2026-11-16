using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HBRGOSakiMod.Core.Helpers;

// Called once by a real DP bar/layer exhaustion, never by costs, maximum-DP
// clamps, shell removal or an ordinary hit against an already broken target.
internal static class HbrDpBreakStatusHelper
{
    internal static async Task Apply(PlayerChoiceContext context, Creature target)
    {
        if (target?.IsAlive != true || target.CombatState == null) return;
        await HbrEnemyEffects.RemoveCharge(target);
        await HbrPercentDebuffApplication.ApplyDpBreakAttackDown(context, target);
    }
}
