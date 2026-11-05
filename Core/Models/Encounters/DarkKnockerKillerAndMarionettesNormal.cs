using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpMtKinpu02_R05.
public sealed class DarkKnockerKillerAndMarionettesNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MarionetteKiller>(),
            ModelDb.Monster<DarkKnockerKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<MarionetteKiller>().ToMutable(), null),
            (ModelDb.Monster<DarkKnockerKiller>().ToMutable(), null),
            (ModelDb.Monster<MarionetteKiller>().ToMutable(), null)
        };
    }
}
