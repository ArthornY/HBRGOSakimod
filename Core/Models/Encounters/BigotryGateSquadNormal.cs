using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpMtKumotori01_R05.
public sealed class BigotryGateSquadNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<BalbFlea>(),
            ModelDb.Monster<BigotryGate>(),
            ModelDb.Monster<TriClaw>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<BalbFlea>().ToMutable(), null),
            (ModelDb.Monster<BigotryGate>().ToMutable(), null),
            (ModelDb.Monster<TriClaw>().ToMutable(), null)
        };
    }
}
