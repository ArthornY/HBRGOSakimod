using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomRosterCandidateHelper
{
    // Canonical models have no Creature to inspect for MinionPower. These are
    // dedicated summons (including Fabricator's four bots) or shared boss arms.
    // Ordinary enemies that can also be summoned, such as TwoTailedRat, remain
    // eligible. Only All Things in Nature uses this filter; existing rosters and
    // other acquisition routes retain their original rules.
    internal static bool IsMinionOnlyModel(MonsterModel monster) => monster is
        GasBomb or KinFollower or TorchHeadAmalgam or
        EyeWithTeeth or Parafright or ToughEgg or
        Zapbot or Stabbot or Guardbot or Noisebot or
        Rocket or Crusher or Osty or RedCrimson;
}
