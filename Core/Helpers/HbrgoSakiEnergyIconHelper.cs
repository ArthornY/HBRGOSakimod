using HBRGOSakiMod.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrgoSakiEnergyIconHelper
{
    internal const string DescriptionVariable = "hbrgoSakiEnergyPrefix";

    internal static LocString Add31OPrefix(LocString description)
    {
        description.Add(
            DescriptionVariable,
            EnergyIconHelper.GetPrefix(
                ModelDb.CardPool<HBRGOSaki31OCardPool>()));
        return description;
    }
}
