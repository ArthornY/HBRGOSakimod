using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_25: a pack of wire slashers, each one dp step after the previous
// one.
public sealed class WireSlashersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<WireSlasher>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (WireSlasher)ModelDb.Monster<WireSlasher>().ToMutable();
        second.StartDpTableAt(1);
        var third = (WireSlasher)ModelDb.Monster<WireSlasher>().ToMutable();
        third.StartDpTableAt(2);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<WireSlasher>().ToMutable(), null),
            (second, null),
            (third, null)
        };
    }
}
