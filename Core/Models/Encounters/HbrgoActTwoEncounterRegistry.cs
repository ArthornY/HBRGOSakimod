using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Encounters;

internal static class HbrgoActTwoEncounterRegistry
{
    internal static IReadOnlyList<EncounterModel> GetAll()
    {
        // Sized like the native Hive pools: 4 weak, 10 normal, 3 elite and 3 boss encounters.
        // They join, rather than replace, the native pools.
        return new EncounterModel[]
        {
            ModelDb.Encounter<MarionetteKillerWeak>(),
            ModelDb.Encounter<CrestHopperKillerWeak>(),
            ModelDb.Encounter<DarkKnockerKillerWeak>(),
            ModelDb.Encounter<MarionetteKillersWeak>(),
            ModelDb.Encounter<CrestHopperKillerAndMarionettesNormal>(),
            ModelDb.Encounter<KillerTrainingSquadNormal>(),
            ModelDb.Encounter<DarkKnockerKillerSquadNormal>(),
            ModelDb.Encounter<CatHornKillerWithHealersNormal>(),
            ModelDb.Encounter<CrestHopperKillersNormal>(),
            ModelDb.Encounter<DarkKnockerKillersNormal>(),
            ModelDb.Encounter<DarkKnockerKillerAndMarionettesNormal>(),
            ModelDb.Encounter<CatHornKillersNormal>(),
            ModelDb.Encounter<OctopusTailKillersNormal>(),
            ModelDb.Encounter<MarionetteKillerAndCatHornsNormal>(),
            ModelDb.Encounter<AbyssKnockerKillerElite>(),
            ModelDb.Encounter<TargetWireFrameKillerElite>(),
            ModelDb.Encounter<PileGolemEdgeElite>(),
            ModelDb.Encounter<DeathSlugBoss>(),
            ModelDb.Encounter<DiamondEyeballBoss>(),
            ModelDb.Encounter<BarrelWalkerBoss>()
        };
    }
}
