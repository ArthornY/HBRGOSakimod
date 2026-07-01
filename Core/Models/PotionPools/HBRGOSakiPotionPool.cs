using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Models.PotionPools;

public sealed class HBRGOSakiPotionPool : PotionPoolModel
{
    public override string EnergyColorName => "regent";

    public override Color LabOutlineColor => StsColors.blue;

    protected override IEnumerable<PotionModel> GenerateAllPotions()
    {
        return ModelDb.PotionPool<SharedPotionPool>().AllPotions.ToArray();
    }

    public override IEnumerable<PotionModel> GetUnlockedPotions(UnlockState unlockState)
    {
        return ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(unlockState);
    }
}
