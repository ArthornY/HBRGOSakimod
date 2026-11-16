using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpKirigamine02_01_R05: BigotryGateSmall_01 and BigotryGateSmall_02,
// whose tables are offset so the two Strong Impulses fall on different turns.
public sealed class BigotryGatesNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<BigotryGate>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        BigotryGate second = (BigotryGate)ModelDb.Monster<BigotryGate>().ToMutable();
        second.MoveTableVariant = BigotryGate.SecondSmallVariant;
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<BigotryGate>().ToMutable(), null),
            (second, null)
        };
    }
}
