using System;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Scaffolding.Content;

namespace HBRGOSakiMod.Core.Nodes.RestSite;

internal abstract class UpgradePointRestSiteOption : ModRestSiteOptionTemplate
{
    internal ElectronicSoldierManual PointManual => Owner?.GetRelic<ElectronicSoldierManual>();
    internal int UpgradePoints
    {
        get
        {
            ElectronicSoldierManual manual = PointManual;
            manual?.SynchronizeMemoryGardenUpgradePoints();
            return Math.Max(0, manual?.MemoryGardenUpgradePoints ?? 0);
        }
    }

    protected UpgradePointRestSiteOption(Player owner) : base(owner) { }
}
