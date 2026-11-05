using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpTraining01Virtual_S01.
public sealed class CatHornKillerWithHealersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<HealHopper>(),
            ModelDb.Monster<CatHornKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<HealHopper>().ToMutable(), null),
            (ModelDb.Monster<CatHornKiller>().ToMutable(), null),
            (ModelDb.Monster<HealHopper>().ToMutable(), null)
        };
    }
}
