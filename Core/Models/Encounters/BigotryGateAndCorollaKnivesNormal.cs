using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpKirigamine02_05_R04.
public sealed class BigotryGateAndCorollaKnivesNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CorollaKnife>(),
            ModelDb.Monster<BigotryGate>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CorollaKnife>().ToMutable(), null),
            (ModelDb.Monster<BigotryGate>().ToMutable(), null),
            (ModelDb.Monster<CorollaKnife>().ToMutable(), null)
        };
    }
}
