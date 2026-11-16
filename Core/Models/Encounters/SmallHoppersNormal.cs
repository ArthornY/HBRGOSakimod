using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC01_12_Battle01b: three small hoppers, each one dp step after the
// previous one.
public sealed class SmallHoppersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<SmallHopper>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return Enumerable.Range(0, 3)
            .Select(index =>
            {
                var hopper = (SmallHopper)ModelDb.Monster<SmallHopper>().ToMutable();
                hopper.StartDpTableAt(index);
                return ((MonsterModel)hopper, (string)null);
            })
            .ToArray();
    }
}
