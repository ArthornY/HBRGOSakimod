using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

public sealed class GoldOmenWeak : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<GoldHopper>() };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        GoldHopper hopper =
            (GoldHopper)ModelDb.Monster<GoldHopper>().ToMutable();
        hopper.ConfigureVisuals(Rng.NextBool());
        return new (MonsterModel, string)[] { (hopper, null) };
    }
}
