using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomOvicopterHelper
{
    private const int EggsPerLay = 3;
    private const int MaximumMinions = 5;

    internal static bool IsPhantom(Creature creature) =>
        creature?.Monster is Ovicopter && creature.Side == CombatSide.Player &&
        creature.PetOwner != null && creature.HasPower<PhantomPower>();

    private static int LivingMinionCount(Creature leader) =>
        leader.CombatState?.GetTeammatesOf(leader).Count(creature =>
            creature.IsAlive && PhantomHelper.IsMinionOf(leader, creature)) ?? 0;

    internal static bool CanLay(Creature leader) =>
        IsPhantom(leader) && leader.IsAlive && leader.CombatState != null &&
        // Native CanLay includes the leader in its <= 3 teammate check.
        // Apply that threshold only to this phantom's own living minions.
        LivingMinionCount(leader) <= MaximumMinions - EggsPerLay;

    internal static async Task LayEggs(Creature leader)
    {
        if (!CanLay(leader) || !leader.CombatState.IsLiveCombat())
            return;

        var combatState = leader.CombatState;
        SfxCmd.Play("event:/sfx/enemy/enemy_attacks/egg_layer/egg_layer_lay");
        await CreatureCmd.TriggerAnim(leader, "layTrigger", 1f);
        for (int index = 0; index < EggsPerLay; index++)
        {
            if (!IsPhantom(leader) || !leader.IsAlive ||
                leader.CombatState != combatState || !combatState.IsLiveCombat() ||
                LivingMinionCount(leader) >= MaximumMinions)
                break;

            // Pets use allied placement, not markers from the enemy encounter.
            // The active summon scope sets side/owner and later initializes DP.
            Creature egg = await CreatureCmd.Add<ToughEgg>(combatState);
            await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(),
                egg, 1m, leader, null);
        }
    }
}
