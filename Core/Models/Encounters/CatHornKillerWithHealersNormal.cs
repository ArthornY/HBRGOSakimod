using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC03ExpTraining01Virtual_S01: both heal hoppers copy
// CrestHopperHealVirtual_MC03; the second starts one dp step ahead.
public sealed class CatHornKillerWithHealersNormal : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<HealHopper>(),
            ModelDb.Monster<CatHornKiller>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        var first = (HealHopper)ModelDb.Monster<HealHopper>().ToMutable();
        first.MoveTableVariant = HealHopper.OmegaVariant;
        var second = (HealHopper)ModelDb.Monster<HealHopper>().ToMutable();
        second.MoveTableVariant = HealHopper.OmegaVariant;
        second.StartDpTableAt(1);
        return new (MonsterModel, string)[]
        {
            (first, null),
            (ModelDb.Monster<CatHornKiller>().ToMutable(), null),
            (second, null)
        };
    }
}
