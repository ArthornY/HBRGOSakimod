using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpTraining01Virtual_R02: the heal hopper copies
// CrestHopperHealVirtual_MC03.
public sealed class KillerTrainingSquadNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MarionetteKiller>(),
            ModelDb.Monster<HealHopper>(),
            ModelDb.Monster<CrestHopperKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var healer = (HealHopper)ModelDb.Monster<HealHopper>().ToMutable();
        healer.MoveTableVariant = HealHopper.OmegaVariant;
        return new (MonsterModel, string)[]
        {
            (ModelDb.Monster<MarionetteKiller>().ToMutable(), null),
            (healer, null),
            (ModelDb.Monster<CrestHopperKiller>().ToMutable(), null)
        };
    }
}
