using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class TeamCombatHelper
{
    public static Creature[] GetLivingPlayerCreatures(Creature source)
    {
        if (source?.CombatState == null)
        {
            return Array.Empty<Creature>();
        }

        return source.CombatState.Players
            .Select(player => player.Creature)
            .Where(creature => creature != null && creature.IsAlive && creature.Side == source.Side)
            .ToArray();
    }
}
