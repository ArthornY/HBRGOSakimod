using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Nodes.RestSite;

internal sealed class HotaruCannonUpgradeRestSiteOption : UpgradePointRestSiteOption
{
    internal const string Id = "HOTARU_CANNON_UPGRADE";
    public override string OptionId => Id;
    public override string CustomIconPath => "res://images/relics/millsage.webp";
    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(
        System.Enum.GetValues<HotaruCannonKind>().Select(HotaruCannonUpgrades.IconPath)).Distinct();
    public override bool IsEnabled => HotaruCannonUpgrades.HasAvailableUpgrade(Owner) && UpgradePoints > 0;
    public override LocString Description
    {
        get
        {
            string suffix = !HotaruCannonUpgrades.CanAccess(Owner) ? "descriptionDisabled"
                : !HotaruCannonUpgrades.HasAvailableUpgrade(Owner) ? "descriptionMaxed"
                : UpgradePoints <= 0 ? "descriptionNoPoints" : "description";
            LocString description = new("rest_site_ui", $"OPTION_{Id}.{suffix}");
            description.Add("points", UpgradePoints);
            return description;
        }
    }

    internal HotaruCannonUpgradeRestSiteOption(Player owner) : base(owner) { }
    public override async Task<bool> OnSelect()
    {
        if (IsEnabled) await HotaruCannonUpgrades.Upgrade(Owner);
        return false;
    }
}
