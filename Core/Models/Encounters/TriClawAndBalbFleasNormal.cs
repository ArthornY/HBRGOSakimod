using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpMtKumotori01_R02; the second balb flea starts one dp step ahead.
public sealed class TriClawAndBalbFleasNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<BalbFlea>(),
            ModelDb.Monster<TriClaw>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var second = (BalbFlea)ModelDb.Monster<BalbFlea>().ToMutable();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<BalbFlea>().ToMutable(), null),
            (ModelDb.Monster<TriClaw>().ToMutable(), null),
            (second, null)
        };
    }
}
