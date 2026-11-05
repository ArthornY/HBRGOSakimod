using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpMtKinpu02_E02.
public sealed class TargetWireFrameKillerElite : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Elite;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CrestHopperKiller>(),
            ModelDb.Monster<TargetWireFrameKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CrestHopperKiller>().ToMutable(), null),
            (ModelDb.Monster<TargetWireFrameKiller>().ToMutable(), null),
            (ModelDb.Monster<CrestHopperKiller>().ToMutable(), null)
        };
    }
}
