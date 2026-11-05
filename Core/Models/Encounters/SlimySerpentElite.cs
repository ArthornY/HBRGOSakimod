using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04BExpDay04Desert01_S01.
public sealed class SlimySerpentElite : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Elite;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<SlimySerpent>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<SlimySerpent>().ToMutable(), null)
        };
    }
}
