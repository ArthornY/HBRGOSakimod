using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

/// <summary>
/// Current-state values for the short top-bar relic texts: the tier a
/// collection is on, the charges, garden points and remaining OD levels. Added
/// only while a compact hover is built, so the full description is unchanged.
/// </summary>
internal static class HbrCompactRelicHoverTips
{
    internal static void AddVariables(RelicModel relic, LocString description)
    {
        if (!HbrCompactText.IsActive || relic?.IsMutable != true || description == null) return;
        switch (relic)
        {
            case ElectronicSoldierManual manual:
                description.Add(HbrCompactTextKeys.GardenPoints, manual.MemoryGardenUpgradePoints);
                break;
            case Overclock overclock:
                description.Add(HbrCompactTextKeys.RemainingLevels, overclock.RemainingFloorLevels);
                break;
            case HotaruIridescence iridescence:
                description.Add(HbrCompactTextKeys.Layers, iridescence.Layers);
                break;
            case TeamCollectionRelic collection:
                description.Add(HbrCompactTextKeys.Count, collection.DisplayAmount);
                break;
        }
    }
}
