using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpKirigamine02_01_BOSS01: ShrimpCrabDefault_01 and _02, which use
// different tables (one strikes while the other casts Mirage Pulse).
public sealed class ShrimpCrabsElite : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Elite;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<ShrimpCrab>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        ShrimpCrab second = (ShrimpCrab)ModelDb.Monster<ShrimpCrab>().ToMutable();
        second.MoveTableVariant = ShrimpCrab.SecondVariant;
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<ShrimpCrab>().ToMutable(), null),
            (second, null)
        };
    }
}
