using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_14_Battle02: a crest hopper and two target wire frames.
public sealed class TargetWireFramesNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CrestHopper>(),
            ModelDb.Monster<TargetWireFrame>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<TargetWireFrame>().ToMutable(), null),
            (ModelDb.Monster<CrestHopper>().ToMutable(), null),
            (ModelDb.Monster<TargetWireFrame>().ToMutable(), null)
        };
    }
}
