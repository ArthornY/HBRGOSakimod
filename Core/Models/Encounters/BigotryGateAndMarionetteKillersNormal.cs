using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC04BExpDay01Kisokoma01_R03: the gate is BigotryGateDefault_01 and the
// two dolls are cursed dolls (MarionetteKillerCurse); the second doll starts
// one dp step ahead, its first once-only step spent.
public sealed class BigotryGateAndMarionetteKillersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MarionetteKiller>(),
            ModelDb.Monster<BigotryGate>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        BigotryGate gate = (BigotryGate)ModelDb.Monster<BigotryGate>().ToMutable();
        gate.MoveTableVariant = BigotryGate.DefaultVariant;
        MarionetteKiller second = CursedDoll();
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (CursedDoll(), null),
            (gate, null),
            (second, null)
        };
    }

    private static MarionetteKiller CursedDoll()
    {
        MarionetteKiller doll = (MarionetteKiller)ModelDb.Monster<MarionetteKiller>().ToMutable();
        doll.MoveTableVariant = MarionetteKiller.CurseVariant;
        return doll;
    }
}
