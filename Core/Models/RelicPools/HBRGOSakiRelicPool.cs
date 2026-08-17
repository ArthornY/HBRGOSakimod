using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace HBRGOSakiMod.Core.Models.RelicPools;

public sealed class HBRGOSakiRelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "regent";

    public override Color LabOutlineColor => StsColors.blue;
}
