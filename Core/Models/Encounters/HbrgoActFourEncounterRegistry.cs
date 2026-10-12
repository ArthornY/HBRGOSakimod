using System.Collections.Generic;
using HBRGOSakiMod.Core.Integration;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Encounters;

internal static class HbrgoActFourEncounterRegistry
{
    internal static IReadOnlyList<EncounterModel> GetAll()
    {
        return new EncounterModel[]
        {
            ModelDb.Encounter<FlatHandBoss>(),
            ModelDb.Encounter<SkullFeatherBoss>()
        };
    }

    internal static IReadOnlyList<EncounterModel> GetUnlocked()
    {
        if (!HbrgoSakiVariantPreference.IsSkullFeatherUnlocked())
        {
            return new EncounterModel[]
            {
                ModelDb.Encounter<FlatHandBoss>()
            };
        }

        return GetAll();
    }
}
