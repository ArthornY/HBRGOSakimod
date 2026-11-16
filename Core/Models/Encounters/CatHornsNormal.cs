using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC01_05_Battle01: two cat horns, both α (CatHornSmallVirtual_first);
// the second starts one dp step ahead.
public sealed class CatHornsNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CatHorn>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        CatHorn second = First();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (First(), null),
            (second, null)
        };
    }

    private static CatHorn First()
    {
        CatHorn horn = (CatHorn)ModelDb.Monster<CatHorn>().ToMutable();
        horn.MoveTableVariant = CatHorn.FirstVariant;
        return horn;
    }
}
