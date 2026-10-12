using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Models.Potions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Models.PotionPools;

public sealed class HBRGOSaki31OPotionPool : CustomPotionPoolModel
{
    private static IEnumerable<PotionModel> CharacterPotions =>
        new PotionModel[]
        {
            ModelDb.Potion<DpPotionHbrgoSaki>(),
            ModelDb.Potion<FragilePotionHbrgoSaki>()
        };

    public override string EnergyColorName => "regent";

    public override Color LabOutlineColor => StsColors.blue;

    protected override IEnumerable<PotionModel> GenerateAllPotions()
    {
        return ModelDb.PotionPool<SharedPotionPool>()
            .AllPotions
            .Concat(CharacterPotions)
            .DistinctBy(potion => potion.Id);
    }

    public override IEnumerable<PotionModel> GetUnlockedPotions(
        UnlockState unlockState)
    {
        return ModelDb.PotionPool<SharedPotionPool>()
            .GetUnlockedPotions(unlockState)
            .Concat(CharacterPotions)
            .DistinctBy(potion => potion.Id);
    }
}
