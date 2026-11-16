using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04BExpDay03Desert01_R05: the claw is 三角镖 (TriClawDefault).
public sealed class MolArmsAndTriClawNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MolArms>(),
            ModelDb.Monster<TriClaw>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        TriClaw claw = (TriClaw)ModelDb.Monster<TriClaw>().ToMutable();
        claw.MoveTableVariant = TriClaw.DefaultVariant;
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<MolArms>().ToMutable(), null),
            (claw, null)
        };
    }
}
