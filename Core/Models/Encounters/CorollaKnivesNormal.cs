using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpKirigamine02_02_R05: CorollaKnifeSmall_01 and _02.
public sealed class CorollaKnivesNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<CorollaKnife>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        CorollaKnife second = (CorollaKnife)ModelDb.Monster<CorollaKnife>().ToMutable();
        second.MoveTableVariant = CorollaKnife.SecondVariant;
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<CorollaKnife>().ToMutable(), null),
            (second, null)
        };
    }
}
