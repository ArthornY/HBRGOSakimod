using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpKirigamine01_01_R05.
public sealed class CorollaKnifeSquadNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CorollaKnife>(),
            ModelDb.Monster<TriClaw>(),
            ModelDb.Monster<BalbFlea>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CorollaKnife>().ToMutable(), null),
            (ModelDb.Monster<TriClaw>().ToMutable(), null),
            (ModelDb.Monster<BalbFlea>().ToMutable(), null)
        };
    }
}
