using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
internal static class MemoryGardenRestSitePatch
{
    private static void Postfix(Player player, List<RestSiteOption> __result)
    {
        if (HbrMemoryGarden.CanAccess(player) &&
            !__result.Any(option => option.OptionId == MemoryGardenRestSiteOption.Id))
        {
            // Keep the entry visible at zero points, with its disabled explanation.
            __result.Add(new MemoryGardenRestSiteOption(player));
        }
        if (HotaruCannonUpgrades.CanAccess(player) &&
            !__result.Any(option => option.OptionId == HotaruCannonUpgradeRestSiteOption.Id))
            __result.Add(new HotaruCannonUpgradeRestSiteOption(player));
        // 修骰子 appears while 31O's fate dice has a crack.
        if (FateDiceRepairRestSiteOption.ShouldOffer(player) &&
            !__result.Any(option => option.OptionId == FateDiceRepairRestSiteOption.Id))
            __result.Add(new FateDiceRepairRestSiteOption(player));
    }
}
