using System.Collections.Generic;
using Godot;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.RelicPools;

public sealed class HBRGOSakiRelicPool : RelicPoolModel
{
    public override string EnergyColorName => "regent";

    public override Color LabOutlineColor => StsColors.blue;

    protected override IEnumerable<RelicModel> GenerateAllRelics()
    {
        return new RelicModel[]
        {
            ModelDb.Relic<ElectronicSoldierManual>(),
            ModelDb.Relic<SoulOfAnchor>(),
            ModelDb.Relic<DeflectorProtection>(),
            ModelDb.Relic<Overclock>(),
            ModelDb.Relic<GuideLittleHand>(),
            ModelDb.Relic<FeintLikeWind>()
        };
    }
}
