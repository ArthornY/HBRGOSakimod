using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpPatrol02_BOSS01: an act-three boss.
public sealed class ArachneLineBoss : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/arachne_line";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<ArachneLine>() };

    public override float GetCameraScaling()
    {
        return 1f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<ArachneLine>().ToMutable(), null)
        };
    }
}
