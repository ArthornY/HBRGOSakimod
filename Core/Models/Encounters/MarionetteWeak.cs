using System.Collections.Generic;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Models.Encounters;

// HBR MC01_01_Battle01: a lone puppet, the tutorial's
// MarionetteSmallVirtual_first.
public sealed class MarionetteWeak : EncounterModel, IHbrgoEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Marionette>()
        };

    protected override IReadOnlyList<(MonsterModel, string)>
        GenerateMonsters()
    {
        Marionette puppet = (Marionette)ModelDb.Monster<Marionette>().ToMutable();
        puppet.MoveTableVariant = Marionette.TutorialVariant;
        return new (MonsterModel, string)[]
        {
            (puppet, null)
        };
    }
}
