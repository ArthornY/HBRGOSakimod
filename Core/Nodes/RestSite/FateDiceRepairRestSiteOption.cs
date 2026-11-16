using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Nodes.RestSite;

// 「修骰子」 (design v3, section 4): spends the rest to mend one crack of 31O's fate dice.
// It shows only while the dice has a crack. The rest site runs the choice on every peer.
internal sealed class FateDiceRepairRestSiteOption : ModRestSiteOptionTemplate
{
    internal const string Id = "HBR_FATE_DICE_REPAIR";

    private ElectronicSoldierManual Manual => Owner?.GetRelic<ElectronicSoldierManual>();

    public override string OptionId => Id;

    public override string CustomIconPath => HbrMagicSwordArt.FateFragmentIcon;

    public override bool IsEnabled => Manual?.CanRepairFateDice == true;

    public override LocString Description
    {
        get
        {
            LocString description = new("rest_site_ui", $"OPTION_{Id}.description");
            description.Add("cracks", Manual?.UsesFateDice == true ? Manual.FateDice.Cracks : 0);
            return description;
        }
    }

    internal static bool ShouldOffer(Player player) =>
        player?.GetRelic<ElectronicSoldierManual>()?.CanRepairFateDice == true;

    internal FateDiceRepairRestSiteOption(Player owner) : base(owner) { }

    public override Task<bool> OnSelect() => Task.FromResult(Manual?.RepairFateDiceCrack() == true);
}
