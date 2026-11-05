using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Encounters;

internal static class HbrgoActThreeEncounterRegistry
{
    internal static IReadOnlyList<EncounterModel> GetAll()
    {
        // Sized like the native Glory pools: 3 weak, 9 normal, 3 elite and 3 boss encounters.
        // They join, rather than replace, the native pools.
        return new EncounterModel[]
        {
            ModelDb.Encounter<BalbFleaAndTriClawWeak>(),
            ModelDb.Encounter<BigotryGateWeak>(),
            ModelDb.Encounter<CorollaKnifeAndTriClawWeak>(),
            ModelDb.Encounter<TriClawAndBalbFleasNormal>(),
            ModelDb.Encounter<BigotryGateSquadNormal>(),
            ModelDb.Encounter<CorollaKnifeSquadNormal>(),
            ModelDb.Encounter<BigotryGatesNormal>(),
            ModelDb.Encounter<CorollaKnivesNormal>(),
            ModelDb.Encounter<BigotryGateAndCorollaKnivesNormal>(),
            ModelDb.Encounter<MolArmsAndTriClawNormal>(),
            ModelDb.Encounter<MolArmsPairNormal>(),
            ModelDb.Encounter<BigotryGateAndMarionetteKillersNormal>(),
            ModelDb.Encounter<ShrimpCrabsElite>(),
            ModelDb.Encounter<SlimySerpentElite>(),
            ModelDb.Encounter<GiraffeHaloElite>(),
            ModelDb.Encounter<RedCrimsonBoss>(),
            ModelDb.Encounter<ArachneLineBoss>(),
            ModelDb.Encounter<PillarGuardianBoss>()
        };
    }
}
