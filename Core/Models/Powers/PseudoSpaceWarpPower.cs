using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Models.Powers;

public sealed class PseudoSpaceWarpPower : StrongestShieldPower, ICustomPower
{
    protected override Player RollOwner => Owner.PetOwner ?? Owner.Player;

    protected override string RollSourceName => "空间扭曲-伪";

    public string CustomPackedIconPath =>
        "res://images/atlases/power_atlas.sprites/phase_warp_power.tres";

    public string CustomBigIconPath => CustomPackedIconPath;
    public string CustomBigBetaIconPath => CustomPackedIconPath;
}
