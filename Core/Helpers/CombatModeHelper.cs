using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CombatModeHelper
{
    public static bool HasEnemyAttackIntent(Creature owner)
    {
        return owner?.CombatState?.Creatures.Any(creature =>
            creature.IsAlive &&
            creature.Side != owner.Side &&
            creature.Monster?.NextMove?.Intents.Any(intent => intent.IntentType == IntentType.Attack) == true) == true;
    }

    public static bool IsMultiplayer(Creature owner)
    {
        return owner?.CombatState?.RunState?.Players.Count() > 1;
    }
}
