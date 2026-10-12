using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Encounters;

internal static class HbrgoActTwoEncounterRegistry
{
    internal static IReadOnlyList<EncounterModel> GetAll()
    {
        return new EncounterModel[]
        {
            ModelDb.Encounter<DeathSlugBoss>()
        };
    }
}
