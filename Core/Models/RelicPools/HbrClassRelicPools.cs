using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.RelicPools;

// Class drops are registered only with their matching character pool.
public interface IThirtyOneOClassRelic { }
public interface IThirtyAClassRelic { }
public interface IHotaruClassRelic { }

internal static class HbrClassRelicPoolRules
{
    internal static IEnumerable<RelicModel> SharedSakiRelics() =>
        ModelDb.RelicPool<HBRGOSakiRelicPool>().AllRelics.Where(relic =>
            relic is not IThirtyOneOClassRelic && relic is not IThirtyAClassRelic && relic is not IHotaruClassRelic);
}

public sealed class HBRGOSaki31ORelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "regent";
    public override Color LabOutlineColor => StsColors.blue;
    protected override IEnumerable<RelicModel> GenerateAllRelics() => HbrClassRelicPoolRules.SharedSakiRelics();
}

public sealed class HBRGOSaki30ARelicPool : CustomRelicPoolModel
{
    public override string EnergyColorName => "regent";
    public override Color LabOutlineColor => StsColors.blue;
    protected override IEnumerable<RelicModel> GenerateAllRelics() => HbrClassRelicPoolRules.SharedSakiRelics();
}
