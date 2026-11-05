using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Encounters;

public interface IHbrgoEncounter
{
}

internal static class HbrgoActOneEncounterRegistry
{
    internal static IReadOnlyList<EncounterModel> GetAll()
    {
        // Sized like the native act-one pools: 4 weak, 12 normal, 3 elite and
        // 3 boss encounters. They join, rather than replace, the native pools.
        return new EncounterModel[]
        {
            ModelDb.Encounter<SmallHoppersWeak>(),
            ModelDb.Encounter<CrestHopperWeak>(),
            ModelDb.Encounter<GoldOmenWeak>(),
            ModelDb.Encounter<MarionetteWeak>(),
            ModelDb.Encounter<CrestHopperWithHealersNormal>(),
            ModelDb.Encounter<SmallHoppersNormal>(),
            ModelDb.Encounter<CrestHopperSwarmNormal>(),
            ModelDb.Encounter<CatHornsNormal>(),
            ModelDb.Encounter<MarionettesWithHealerNormal>(),
            ModelDb.Encounter<OctopusTailSquadNormal>(),
            ModelDb.Encounter<TargetWireFrameSquadNormal>(),
            ModelDb.Encounter<KnockersWithHealerNormal>(),
            ModelDb.Encounter<MarionetteAndCatHornsNormal>(),
            ModelDb.Encounter<OctopusTailsNormal>(),
            ModelDb.Encounter<TargetWireFramesNormal>(),
            ModelDb.Encounter<WireSlashersNormal>(),
            ModelDb.Encounter<KnockersElite>(),
            ModelDb.Encounter<AbyssKnockerElite>(),
            ModelDb.Encounter<NeedleBirdElite>(),
            ModelDb.Encounter<TezukaSakiBoss>(),
            ModelDb.Encounter<CrowHorseBoss>(),
            ModelDb.Encounter<IvoryScooperBoss>()
        };
    }
}
