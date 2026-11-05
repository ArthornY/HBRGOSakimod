using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03_17_Battle01.
public sealed class OctopusTailKillersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<OctopusTailKiller>(),
            ModelDb.Monster<CrestHopperKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<OctopusTailKiller>().ToMutable(), null),
            (ModelDb.Monster<CrestHopperKiller>().ToMutable(), null),
            (ModelDb.Monster<OctopusTailKiller>().ToMutable(), null)
        };
    }
}
