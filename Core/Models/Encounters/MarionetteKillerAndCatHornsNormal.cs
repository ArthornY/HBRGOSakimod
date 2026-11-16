using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03_19_Battle01; the second cat horn starts one dp step ahead.
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
        var second = (CatHornKiller)ModelDb.Monster<CatHornKiller>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CatHornKiller>().ToMutable(), null),
            (ModelDb.Monster<MarionetteKiller>().ToMutable(), null),
            (second, null)
        };
    }
}
