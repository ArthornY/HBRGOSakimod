using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC02_21_Battle02: the ivory scooper, an act-one boss.
public sealed class IvoryScooperBoss : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Boss;

    public override string BossNodePath =>
        "res://images/map/boss_nodes/ivory_scooper";

    public override MegaSkeletonDataResource BossNodeSpineResource => null;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<IvoryScooper>() };

    public override float GetCameraScaling()
    {
        return 1f;
    }

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<IvoryScooper>().ToMutable(), null)
        };
    }
}
