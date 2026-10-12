using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class LegendNextAttackHelper
{
    internal static async Task Grant(PlayerChoiceContext context, Creature target,
        Creature source, CardModel card, bool doubleHits)
    {
        if (target?.IsAlive != true) return;

        // Phantom intents use a segment list rather than AttackCommand. Reuse their
        // existing buffs so the intent preview and end-of-attack consumption agree.
        if (target.HasPower<PhantomPower>())
        {
            if (doubleHits)
                await PowerCmd.Apply<PhantomNextAttackDoublePower>(context, target, 1m, source, card);
            else
                await PowerCmd.Apply<PhantomNextAttackBonusSegmentsPower>(context, target, 1m, source, card);
        }
        else if (doubleHits)
        {
            await PowerCmd.Apply<MyLegendNextAttackDoublePower>(context, target, 1m, source, card);
        }
        else
        {
            await PowerCmd.Apply<MyLegendNextAttackPower>(context, target, 1m, source, card);
        }
    }
}
