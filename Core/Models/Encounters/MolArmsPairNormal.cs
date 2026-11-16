using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04BExpDay05Desert01_R05: both use MolArmsSmall's table; the second
// starts one step ahead so the boosted all-unit attacks do not coincide.
public sealed class MolArmsPairNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MolArms>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        MolArms second = (MolArms)ModelDb.Monster<MolArms>().ToMutable();
        second.StartDpTableAt(MolArms.PairOffsetStep);
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<MolArms>().ToMutable(), null),
            (second, null)
        };
    }
}
