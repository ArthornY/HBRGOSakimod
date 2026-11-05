using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_13_Battle01: a puppet and two cat horns.
public sealed class MarionetteAndCatHornsNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Marionette>(),
            ModelDb.Monster<CatHorn>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CatHorn>().ToMutable(), null),
            (ModelDb.Monster<Marionette>().ToMutable(), null),
            (ModelDb.Monster<CatHorn>().ToMutable(), null)
        };
    }
}
