using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// Player-facing all-party support excludes floating cannons and disposable
// summons. Main phantoms and allied players retain their independent pools.
internal static class HbrPlayerTeamTargets
{
    internal static Creature[] Get(Creature source) => source?.CombatState == null ? Array.Empty<Creature>() :
        source.CombatState.GetTeammatesOf(source).Append(source)
            .Where(unit => unit.IsAlive && (unit.Player != null || PhantomHelper.IsOwnedMainPhantom(unit, unit.PetOwner)))
            .Distinct().ToArray();
}
