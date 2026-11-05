using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03_19_Battle01.
public sealed class MarionetteKillerAndCatHornsNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CatHornKiller>(),
            ModelDb.Monster<MarionetteKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CatHornKiller>().ToMutable(), null),
            (ModelDb.Monster<MarionetteKiller>().ToMutable(), null),
            (ModelDb.Monster<CatHornKiller>().ToMutable(), null)
        };
    }
}
