using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

public sealed class SmallHoppersWeak : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<SmallHopper>(),
            ModelDb.Monster<HealHopper>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        MonsterModel first = Rng.NextBool()
            ? ModelDb.Monster<HealHopper>().ToMutable()
            : ModelDb.Monster<SmallHopper>().ToMutable();
        return new (MonsterModel, string)[]
        {
            (first, null),
            (ModelDb.Monster<SmallHopper>().ToMutable(), null)
        };
    }
}
