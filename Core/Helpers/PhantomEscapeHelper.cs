using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomEscapeHelper
{
    internal static bool DiesInsteadOfEscaping(Creature creature) =>
        creature?.Monster is ThievingHopper &&
        creature.Side == CombatSide.Player &&
        creature.PetOwner != null &&
        creature.HasPower<PhantomPower>();
}
