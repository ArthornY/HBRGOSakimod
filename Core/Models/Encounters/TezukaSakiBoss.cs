using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

public sealed class TezukaSakiBoss : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/tezuka_saki";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<TezukaSaki>() };

    public override float GetCameraScaling()
    {
        return 0.95f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<TezukaSaki>().ToMutable(), null)
        };
    }
}
