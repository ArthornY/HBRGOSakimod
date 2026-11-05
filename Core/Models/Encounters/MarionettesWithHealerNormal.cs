using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_10_Battle01: a heal hopper and two puppets.
public sealed class MarionettesWithHealerNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<HealHopper>(),
            ModelDb.Monster<Marionette>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<Marionette>().ToMutable(), null),
            (ModelDb.Monster<HealHopper>().ToMutable(), null),
            (ModelDb.Monster<Marionette>().ToMutable(), null)
        };
    }
}
