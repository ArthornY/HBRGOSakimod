using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04ExpMtKumotori01_BOSS01: an act-two boss.
public sealed class BarrelWalkerBoss : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/barrel_walker";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<BarrelWalker>() };

    public override float GetCameraScaling()
    {
        return 1f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<BarrelWalker>().ToMutable(), null)
        };
    }
}
