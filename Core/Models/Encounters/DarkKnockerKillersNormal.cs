using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpTraining02Virtual_R05; the second starts one dp step ahead.
public sealed class DarkKnockerKillersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<DarkKnockerKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (DarkKnockerKiller)ModelDb.Monster<DarkKnockerKiller>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<DarkKnockerKiller>().ToMutable(), null),
            (second, null)
        };
    }
}
