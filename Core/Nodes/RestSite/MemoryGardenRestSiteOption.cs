using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Nodes.RestSite;

internal sealed class MemoryGardenRestSiteOption : UpgradePointRestSiteOption
{
    internal const string Id = "HBR_MEMORY_GARDEN";

    public override string OptionId => Id;
    public override string CustomIconPath => HbrMemoryGardenIconHelper.IconPath;
    public override bool IsEnabled => HbrMemoryGarden.CanAccess(Owner) && UpgradePoints > 0;
    public override IEnumerable<string> AssetPaths => base.AssetPaths
        .Concat(NMemoryGardenScreen.AssetPaths)
        .Concat(NMemoryGardenUpgradeScreen.AssetPaths).Distinct();

    public override LocString Description
    {
        get
        {
            string suffix = !HbrMemoryGarden.CanAccess(Owner) ? "descriptionDisabled"
                : UpgradePoints > 0 ? "description" : "descriptionNoPoints";
            LocString description = new("rest_site_ui", $"OPTION_{Id}.{suffix}");
            description.Add("points", UpgradePoints);
            return description;
        }
    }

    internal MemoryGardenRestSiteOption(Player owner) : base(owner) { }

    public override async Task<bool> OnSelect()
    {
        if (IsEnabled) await HbrMemoryGarden.Recall(Owner);
        // A successful recall spends its own point in Claim. Native false
        // preserves all rest actions/options and follows the normal cancel
        // path that re-enables the room after the synchronized choice ends.
        return false;
    }
}
