using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// Mod encounter (no original battle): a crest hopper and two heal hoppers;
// the second heal hopper starts one dp step ahead.
public sealed class CrestHopperWithHealersNormal :
    EncounterModel,
    IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CrestHopper>(),
            ModelDb.Monster<HealHopper>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (HealHopper)ModelDb.Monster<HealHopper>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CrestHopper>().ToMutable(), null),
            (ModelDb.Monster<HealHopper>().ToMutable(), null),
            (second, null)
        };
    }
}
