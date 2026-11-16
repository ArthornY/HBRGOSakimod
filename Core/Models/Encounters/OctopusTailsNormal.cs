using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_14_Battle01: two octopus tails; the second starts one dp step
// ahead.
public sealed class OctopusTailsNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<OctopusTail>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (OctopusTail)ModelDb.Monster<OctopusTail>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<OctopusTail>().ToMutable(), null),
            (second, null)
        };
    }
}
