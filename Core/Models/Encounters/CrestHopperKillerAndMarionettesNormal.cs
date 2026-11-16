using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpMtKinpu01_R04; the second doll starts one dp step ahead.
public sealed class CrestHopperKillerAndMarionettesNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MarionetteKiller>(),
            ModelDb.Monster<CrestHopperKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (MarionetteKiller)ModelDb.Monster<MarionetteKiller>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<MarionetteKiller>().ToMutable(), null),
            (ModelDb.Monster<CrestHopperKiller>().ToMutable(), null),
            (second, null)
        };
    }
}
