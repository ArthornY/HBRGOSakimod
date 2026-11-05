using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04BWilDay04MilitaryFacility01_BOSS01: an act-three boss.
public sealed class PillarGuardianBoss : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/pillar_guardian";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<PillarGuardian>() };

    public override float GetCameraScaling()
    {
        return 1f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<PillarGuardian>().ToMutable(), null)
        };
    }
}
