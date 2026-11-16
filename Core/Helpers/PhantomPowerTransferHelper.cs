using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PhantomPowerTransferHelper
{
    // These powers call their original monster's moves or depend on its state.
    // Their counters cannot be transplanted onto a different monster model.
    internal static bool IsCompatibleWithMonster(PowerModel power, MonsterModel monster) =>
        power != null && power switch
        {
            LargeHealthPower => false,
            NararaLayeredHpPower => false,
            AsleepPower => monster is LagavulinMatriarch,
            SlumberPower => monster is SlumberingBeetle,
            FlutterPower => monster is ThievingHopper,
            RavenousPower => monster is CorpseSlug,
            ShriekPower => monster is TerrorEel,
            NararaSleepingPower => monster is NararaShibaBossMonster,
            _ => true
        };
}
