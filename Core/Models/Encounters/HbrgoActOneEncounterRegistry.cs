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
        return new EncounterModel[]
        {
            ModelDb.Encounter<SmallHoppersWeak>(),
            ModelDb.Encounter<CrestHopperWeak>(),
            ModelDb.Encounter<GoldOmenWeak>(),
            ModelDb.Encounter<CrestHopperWithHealersNormal>(),
            ModelDb.Encounter<KnockersElite>(),
            ModelDb.Encounter<AbyssKnockerElite>(),
            ModelDb.Encounter<TezukaSakiBoss>()
        };
    }
}
