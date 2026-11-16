using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpMtKinpu02_R03; each killer hopper starts one dp step after the
// previous one.
public sealed class CrestHopperKillersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CrestHopperKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (CrestHopperKiller)ModelDb.Monster<CrestHopperKiller>().ToMutable();
        second.StartDpTableAt(1);
        var third = (CrestHopperKiller)ModelDb.Monster<CrestHopperKiller>().ToMutable();
        third.StartDpTableAt(2);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CrestHopperKiller>().ToMutable(), null),
            (second, null),
            (third, null)
        };
    }
}
