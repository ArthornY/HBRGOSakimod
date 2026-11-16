using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC01_12_Battle01event: three crest hoppers. Members use the swarm HP
// range so the trio stays an ordinary act-one fight, and each starts one dp
// step after the previous one.
public sealed class CrestHopperSwarmNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CrestHopper>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return Enumerable.Range(0, 3)
            .Select(index =>
            {
                var hopper = (CrestHopper)ModelDb.Monster<CrestHopper>().ToMutable();
                hopper.IsSwarmMember = true;
                hopper.StartDpTableAt(index);
                return ((MonsterModel)hopper, (string)null);
            })
            .ToArray();
    }
}
