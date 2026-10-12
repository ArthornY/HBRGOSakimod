using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomSleepHelper
{
    internal static async Task AdvanceAfterImmediateAction(Creature phantom)
    {
        if (phantom?.Monster is not LagavulinMatriarch ||
            phantom.Side != CombatSide.Player || phantom.PetOwner == null ||
            !phantom.IsAlive || phantom.CombatState?.ContainsCreature(phantom) != true ||
            !phantom.HasPower<PhantomPower>()) return;

        AsleepPower sleep = phantom.GetPower<AsleepPower>();
        if (sleep?.Owner != phantom || sleep.Amount <= 0) return;

        Creature[] participants = { phantom };
        var context = new ThrowingPlayerChoiceContext();
        // Reuse the native final-sleep plating cleanup and wake-up behavior,
        // without dispatching a side turn or ticking any other power.
        await sleep.BeforeSideTurnEndVeryEarly(context, phantom.Side, participants);
        if (phantom.IsAlive && phantom.CombatState?.ContainsCreature(phantom) == true &&
            ReferenceEquals(phantom.GetPower<AsleepPower>(), sleep))
            await sleep.AfterSideTurnEnd(context, phantom.Side, participants);
    }
}
