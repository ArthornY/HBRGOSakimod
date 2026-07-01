using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Relics;

public sealed class GuideLittleHand : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override string PackedIconPath => ImageHelper.GetImagePath("relics/guide_little_hand.png");

    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override string BigIconPath => PackedIconPath;

    public int TakeHigherRoll(int firstRoll, int secondRoll)
    {
        Flash();
        return int.Max(firstRoll, secondRoll);
    }
}
