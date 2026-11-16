using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// Mod encounter (no original battle): a SmallHopper with a HealHopper or a
// second SmallHopper; two SmallHoppers start one dp step apart.
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
        var second = (SmallHopper)ModelDb.Monster<SmallHopper>().ToMutable();
        if (first is SmallHopper)
        {
            second.StartDpTableAt(1);
        }

        return new (MonsterModel, string)[]
        {
            (first, null),
            (second, null)
        };
    }
}
