using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_11_Battle01: target wire frame, heal hopper, crest hopper.
public sealed class TargetWireFrameSquadNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<TargetWireFrame>(),
            ModelDb.Monster<HealHopper>(),
            ModelDb.Monster<CrestHopper>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CrestHopper>().ToMutable(), null),
            (ModelDb.Monster<TargetWireFrame>().ToMutable(), null),
            (ModelDb.Monster<HealHopper>().ToMutable(), null)
        };
    }
}
