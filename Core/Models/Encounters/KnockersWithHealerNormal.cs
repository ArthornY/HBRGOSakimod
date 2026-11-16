using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_12_Battle02: a heal hopper and two knockers (the original's two
// clay knockers); the second knocker starts one dp step ahead.
public sealed class KnockersWithHealerNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<HealHopper>(),
            ModelDb.Monster<Knocker>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (Knocker)ModelDb.Monster<Knocker>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<Knocker>().ToMutable(), null),
            (ModelDb.Monster<HealHopper>().ToMutable(), null),
            (second, null)
        };
    }
}
